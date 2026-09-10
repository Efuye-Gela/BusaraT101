import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { BridgeClient } from "./bridge.js";
import { createServer } from "./server.js";

const projectIndex = process.argv.indexOf("--project");
const projectPath = projectIndex >= 0 ? process.argv[projectIndex + 1] : process.env.UNITY_PROJECT_PATH;
if (!projectPath) {
  console.error("Usage: node dist/index.js --project <absolute Unity project path> (or set UNITY_PROJECT_PATH)");
  process.exit(1);
}
const server = createServer(new BridgeClient(projectPath));
await server.connect(new StdioServerTransport());
