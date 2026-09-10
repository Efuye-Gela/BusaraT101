import test from "node:test";
import assert from "node:assert/strict";
import http from "node:http";
import path from "node:path";
import { mkdir, rm, writeFile } from "node:fs/promises";
import { randomBytes, randomUUID } from "node:crypto";
import { BridgeClient, within } from "../dist/bridge.js";

test("bridge uses per-project bearer discovery, propagates errors, and never retries", async () => {
  const project = path.resolve(".test-project", randomUUID());
  const directory = path.join(project, "Library", "BusaraMcp");
  await mkdir(directory, { recursive: true });
  const token = randomBytes(32).toString("hex");
  let calls = 0;
  const listener = http.createServer(async (request, response) => {
    calls++;
    assert.equal(request.headers.authorization, `Bearer ${token}`);
    assert.equal(request.url, "/command");
    assert.equal(request.headers.origin, undefined);
    const chunks = [];
    for await (const chunk of request) chunks.push(chunk);
    const args = JSON.parse(Buffer.concat(chunks).toString());
    response.setHeader("Content-Type", "application/json");
    if (args.op === "status") response.end(JSON.stringify({ ok: true, data: { playing: false } }));
    else if (args.op === "slow") setTimeout(() => response.end('{"ok":true,"data":{}}'), 100);
    else response.end(JSON.stringify({ ok: false, error: "Confirmation required" }));
  });
  await new Promise(resolve => listener.listen(0, "127.0.0.1", resolve));
  const discovery = { protocolVersion: 1, projectPath: project, port: listener.address().port, token };
  const file = path.join(directory, "discovery.json");
  await writeFile(file, JSON.stringify(discovery));
  try {
    const bridge = new BridgeClient(project);
    assert.deepEqual(await bridge.call("status", {}), { playing: false });
    await assert.rejects(bridge.call("object.delete", { id: 1 }), /Confirmation required/);
    assert.equal(calls, 2);
    await assert.rejects(new BridgeClient(project, 20).call("slow", {}), /No retry was attempted/);
    assert.equal(calls, 3);
    await writeFile(file, JSON.stringify({ ...discovery, port: 80 }));
    await assert.rejects(bridge.call("status", {}));
    assert.equal(calls, 3, "invalid discovery must not cause a network request");
  } finally {
    listener.closeAllConnections();
    await new Promise(resolve => listener.close(resolve));
    await rm(project, { recursive: true, force: true });
  }
});

test("project containment rejects siblings, roots and equality", () => {
  const root = path.resolve("project");
  assert.equal(within(root, path.join(root, "Library", "discovery.json")), true);
  assert.equal(within(root, root), false);
  assert.equal(within(root, path.resolve("project-other", "file")), false);
  assert.equal(within(root, path.dirname(root)), false);
});
