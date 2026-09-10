import test from "node:test";
import assert from "node:assert/strict";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { InMemoryTransport } from "@modelcontextprotocol/sdk/inMemory.js";
import { createServer } from "../dist/server.js";
import { tools } from "../dist/tools.js";

test("official MCP initialize, list, call, schema validation and errors", async () => {
  const calls = [];
  const server = createServer({ async call(op, args) {
    calls.push({ op, args });
    if (op === "inspect") throw new Error("Object ID is stale");
    return { project: "test-project", op };
  } });
  const client = new Client({ name: "protocol-test", version: "1.0.0" });
  const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();
  await server.connect(serverTransport);
  await client.connect(clientTransport);
  try {
    assert.equal(client.getServerVersion().name, "busara-unity");
    const listing = await client.listTools();
    assert.equal(listing.tools.length, tools.length);
    assert.ok(listing.tools.length >= 25);
    assert.equal(new Set(listing.tools.map(tool => tool.name)).size, tools.length);
    const status = await client.callTool({ name: "unity_status", arguments: {} });
    assert.equal(status.isError, undefined);
    assert.equal(JSON.parse(status.content[0].text).op, "status");
    const invalid = await client.callTool({ name: "unity_object_delete", arguments: { id: 5, confirm: "yes" } });
    assert.equal(invalid.isError, true);
    assert.equal(calls.length, 1, "invalid destructive confirmation never reaches bridge");
    const traversal = await client.callTool({ name: "unity_script_read", arguments: { path: "Assets/../ProjectSettings/a.cs" } });
    assert.equal(traversal.isError, true);
    assert.equal(calls.length, 1);
    const extra = await client.callTool({ name: "unity_status", arguments: { unexpected: true } });
    assert.equal(extra.isError, true);
    assert.equal(calls.length, 1);
    const failure = await client.callTool({ name: "unity_inspect", arguments: { id: 123 } });
    assert.equal(failure.isError, true);
    assert.match(failure.content[0].text, /stale/);
  } finally { await client.close(); await server.close(); }
});

test("all tool schemas reject traversal/rooted paths and invalid IDs", () => {
  const script = tools.find(tool => tool.name === "unity_script_read").schema;
  for (const path of ["../Assets/a.cs", "Assets/../a.cs", "Assets/./a.cs", "C:\\Assets\\a.cs", "Assets\\a.cs", "Assets//a.cs"]) {
    assert.equal(script.safeParse({ path }).success, false, path);
  }
  const inspect = tools.find(tool => tool.name === "unity_inspect").schema;
  assert.equal(inspect.safeParse({ id: 0 }).success, false);
  assert.equal(inspect.safeParse({ id: -10 }).success, true);
});
