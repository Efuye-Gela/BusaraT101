import test from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import { randomUUID } from "node:crypto";
import { mkdir, rename, rm, symlink, writeFile } from "node:fs/promises";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { InMemoryTransport } from "@modelcontextprotocol/sdk/inMemory.js";
import { BridgeClient } from "../dist/bridge.js";
import { createServer } from "../dist/server.js";

const jobId = "a".repeat(32);
function record(status = "running") {
  return {
    jobId, kind: "build", status, message: "Building", processId: process.pid,
    passed: 0, failed: 0, skipped: 0, inconclusive: 0, errors: [],
    startedUtc: new Date().toISOString(), output: "", finishedUtc: ""
  };
}

test("MCP job polling reads atomic persisted results without a responsive Editor or discovery", async () => {
  const project = path.resolve(".test-project", randomUUID());
  const directory = path.join(project, "Library", "BusaraMcp", "Jobs");
  await mkdir(directory, { recursive: true });
  const file = path.join(directory, `${jobId}.json`);
  await writeFile(file, JSON.stringify(record()));
  const server = createServer(new BridgeClient(project, 10));
  const client = new Client({ name: "persisted-job-test", version: "1.0.0" });
  const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();
  await server.connect(serverTransport);
  await client.connect(clientTransport);
  try {
    const running = await client.callTool({ name: "unity_job", arguments: { jobId } });
    assert.notEqual(running.isError, true, JSON.stringify(running));
    assert.equal(JSON.parse(running.content[0].text).status, "running");
    const pending = `${file}.pending`;
    await writeFile(pending, JSON.stringify(record("succeeded")));
    await rename(pending, file);
    const completed = await client.callTool({ name: "unity_job", arguments: { jobId } });
    assert.notEqual(completed.isError, true, JSON.stringify(completed));
    assert.equal(JSON.parse(completed.content[0].text).status, "succeeded");
    const invalid = await client.callTool({ name: "unity_job", arguments: { jobId: "../../discovery" } });
    assert.equal(invalid.isError, true);
    await writeFile(file, JSON.stringify({ ...record(), jobId: "b".repeat(32) }));
    const mismatch = await client.callTool({ name: "unity_job", arguments: { jobId } });
    assert.equal(mismatch.isError, true);
    assert.match(mismatch.content[0].text, /does not match/);
    await writeFile(file, Buffer.alloc(1024 * 1024 + 1));
    const oversized = await client.callTool({ name: "unity_job", arguments: { jobId } });
    assert.equal(oversized.isError, true);
    assert.match(oversized.content[0].text, /1 MiB/);
  } finally {
    await client.close();
    await server.close();
    await rm(project, { recursive: true, force: true });
  }
});

test("job polling rejects linked directories even when they point inside the project", async t => {
  const project = path.resolve(".test-project", randomUUID());
  const state = path.join(project, "Library", "BusaraMcp");
  const actual = path.join(state, "ActualJobs");
  await mkdir(actual, { recursive: true });
  await writeFile(path.join(actual, `${jobId}.json`), JSON.stringify(record()));
  try {
    try {
      await symlink(actual, path.join(state, "Jobs"), process.platform === "win32" ? "junction" : "dir");
    } catch (error) {
      if (["EPERM", "EACCES", "ENOSYS"].includes(error.code)) {
        t.skip(`Host cannot create test links: ${error.code}`);
        return;
      }
      throw error;
    }
    await assert.rejects(new BridgeClient(project).call("job", { jobId }), /symbolic links or junctions/);
  } finally { await rm(project, { recursive: true, force: true }); }
});
