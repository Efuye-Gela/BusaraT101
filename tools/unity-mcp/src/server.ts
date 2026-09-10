import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { BridgeClient } from "./bridge.js";
import { tools } from "./tools.js";
import { z } from "zod";

export function createServer(bridge: Pick<BridgeClient, "call">): McpServer {
  const server = new McpServer({ name: "busara-unity", version: "1.0.0" });
  for (const tool of tools) {
    server.registerTool(tool.name, {
      description: tool.description,
      inputSchema: tool.schema as z.AnyZodObject,
      annotations: { readOnlyHint: "readOnly" in tool && tool.readOnly, destructiveHint: !("readOnly" in tool), openWorldHint: false }
    }, async (args) => {
      try {
        const parsed = tool.schema.parse(args);
        const data = await bridge.call(tool.op, parsed);
        return { content: [{ type: "text" as const, text: JSON.stringify(data, null, 2) }] };
      } catch (error) {
        return { isError: true, content: [{ type: "text" as const, text: error instanceof Error ? error.message : String(error) }] };
      }
    });
  }
  return server;
}
