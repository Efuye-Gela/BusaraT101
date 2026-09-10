import assert from "node:assert/strict";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { realpath } from "node:fs/promises";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const project = process.argv[2] || process.env.UNITY_PROJECT_PATH;
if (!project) throw new Error("Supply absolute Unity project path: npm run smoke -- C:\\path\\Busara");
const expectedProject = await realpath(path.resolve(project));
const transport = new StdioClientTransport({
  command: process.execPath,
  args: [path.join(root, "dist", "index.js"), "--project", expectedProject],
  stderr: "inherit"
});
const client = new Client({ name: "busara-live-verification", version: "1.0.0" });
await client.connect(transport);
try {
  assert.equal(client.getServerVersion().name, "busara-unity");
  const listing = await client.listTools();
  assert.ok(listing.tools.length >= 25);
  for (const [name, args] of [
    ["unity_status", {}],
    ["unity_hierarchy", { limit: 5 }],
    ["unity_assets_find", { query: "t:Scene", limit: 5 }],
    ["unity_console", { limit: 5 }]
  ]) {
    const result = await client.callTool({ name, arguments: args });
    assert.notEqual(result.isError, true, `${name}: ${JSON.stringify(result)}`);
    if (name === "unity_status") {
      const status = JSON.parse(result.content[0].text);
      assert.equal(await realpath(status.project), expectedProject, "Connected Editor must identify this exact worktree project.");
    }
    console.log(`${name}: ${result.content[0].text}`);
  }
  console.log(`PASS: real MCP stdio initialize, tools/list (${listing.tools.length}), and four tools/call requests against Unity Editor.`);
} finally { await client.close(); }
