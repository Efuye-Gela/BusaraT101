import { constants } from "node:fs";
import { lstat, open, readFile, realpath, stat } from "node:fs/promises";
import path from "node:path";
import { z } from "zod";

const discoverySchema = z.object({
  protocolVersion: z.literal(1),
  projectPath: z.string(),
  port: z.number().int().min(1024).max(65535),
  token: z.string().regex(/^[a-f0-9]{64}$/i)
});
const jobIdSchema = z.string().regex(/^[a-f0-9]{32}$/);
const jobSchema = z.object({
  jobId: jobIdSchema,
  kind: z.enum(["compile", "refresh", "tests", "build"]),
  status: z.enum(["queued", "running", "succeeded", "failed", "interrupted"]),
  message: z.string(),
  output: z.string().nullish(),
  startedUtc: z.string(),
  finishedUtc: z.string().nullish(),
  processId: z.number().int().positive(),
  passed: z.number().int().nonnegative(),
  failed: z.number().int().nonnegative(),
  skipped: z.number().int().nonnegative(),
  inconclusive: z.number().int().nonnegative().default(0),
  compilationObserved: z.boolean().optional(),
  errors: z.array(z.string()).max(100)
});

export function within(root: string, candidate: string): boolean {
  const relative = path.relative(root, candidate);
  return relative !== "" && relative !== ".." && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative);
}

export class BridgeClient {
  constructor(readonly projectPath: string, readonly timeoutMs = 15000) {}

  async call(op: string, args: Record<string, unknown>): Promise<unknown> {
    const project = await realpath(this.projectPath);
    if (op === "job") return this.readJob(project, args.jobId);
    const discoveryPath = await realpath(path.join(project, "Library", "BusaraMcp", "discovery.json"));
    if (!within(project, discoveryPath)) throw new Error("Bridge discovery must reside inside this project (no external symlinks).");
    if ((await stat(discoveryPath)).size > 16384) throw new Error("Invalid bridge discovery size.");
    const discovery = discoverySchema.parse(JSON.parse(await readFile(discoveryPath, "utf8")));
    if (await realpath(discovery.projectPath) !== project) throw new Error("Bridge belongs to another project.");
    const body = JSON.stringify({ ...args, op });
    if (Buffer.byteLength(body) > 1024 * 1024) throw new Error("Command exceeds 1 MiB.");
    let response: Response;
    try {
      response = await fetch(`http://127.0.0.1:${discovery.port}/command`, {
        method: "POST",
        headers: { "Authorization": `Bearer ${discovery.token}`, "Content-Type": "application/json" },
        body,
        redirect: "error",
        signal: AbortSignal.timeout(this.timeoutMs)
      });
      const reader = response.body?.getReader();
      if (!reader) throw new Error("Empty bridge response.");
      const chunks: Uint8Array[] = [];
      let size = 0;
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        size += value.length;
        if (size > 4 * 1024 * 1024) {
          await reader.cancel();
          throw new Error("Bridge response exceeds 4 MiB; narrow your query.");
        }
        chunks.push(value);
      }
      const text = Buffer.concat(chunks).toString("utf8");
      if (!response.ok) throw new Error(`Bridge HTTP ${response.status}: ${text.slice(0, 1000)}`);
      const result = JSON.parse(text) as { ok?: boolean; data?: unknown; error?: string };
      if (result.ok !== true) throw new Error(result.error || "Bridge returned an invalid/error response.");
      return result.data;
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      throw new Error(`Unity command ${op} failed: ${message}. No retry was attempted. If this was a mutation, inspect Unity state before retrying; its outcome may be unknown.`);
    }
  }

  private async readJob(project: string, requestedId: unknown): Promise<unknown> {
    const jobId = jobIdSchema.parse(requestedId);
    let file = project;
    for (const segment of ["Library", "BusaraMcp", "Jobs", `${jobId}.json`]) {
      file = path.join(file, segment);
      if ((await lstat(file)).isSymbolicLink()) throw new Error("Job paths must not contain symbolic links or junctions.");
    }
    const canonicalFile = await realpath(file);
    if (!within(project, canonicalFile)) throw new Error("Job file escapes this project.");
    const handle = await open(file, constants.O_RDONLY | (constants.O_NOFOLLOW ?? 0));
    try {
      const metadata = await handle.stat();
      const maxBytes = 1024 * 1024;
      if (!metadata.isFile() || metadata.size > maxBytes) throw new Error("Job record must be a regular file no larger than 1 MiB.");
      const buffer = Buffer.alloc(maxBytes + 1);
      let count = 0;
      while (count < buffer.length) {
        const { bytesRead } = await handle.read(buffer, count, buffer.length - count, count);
        if (bytesRead === 0) break;
        count += bytesRead;
      }
      if (count > maxBytes) throw new Error("Job record exceeds 1 MiB.");
      const job = jobSchema.parse(JSON.parse(buffer.subarray(0, count).toString("utf8")));
      if (job.jobId !== jobId) throw new Error("Job record ID does not match the requested job.");
      return job;
    } finally {
      await handle.close();
    }
  }
}
