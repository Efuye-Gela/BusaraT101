# Busara custom local Unity MCP

This is a project-owned implementation, not a wrapper around an installed Unity MCP package. The official TypeScript MCP SDK exposes typed tools over **stdio**. An Editor-only C# bridge executes their Unity operations on the Editor main thread through an authenticated, loopback-only HTTP listener.

It is a useful development foundation, **not every Unity API**. It deliberately has no arbitrary shell, reflection invocation, or `eval` endpoint.

## Requirements and installation

- Node.js 20+ and npm.
- A licensed Unity **6000.3.6f1** Editor, this project's existing Unity Test Framework package, and an opened project. No runtime packages or `ProjectSettings` modifications are required.
- Use the version pinned in `Busara\ProjectSettings\ProjectVersion.txt`. Verify the Editor owns the intended checkout before connecting; review incidental import changes without discarding unrelated edits.

From this worktree, in PowerShell:

```powershell
Set-Location .\tools\unity-mcp
npm ci
npm test
```

The included lockfile pins dependencies. `npm test` builds TypeScript and runs Node's built-in tests; it does not launch Unity.

Open `Busara` in Unity and allow script compilation to finish. The bridge starts automatically from an explicit Editor load callback, with an update-loop fallback rather than relying on a one-shot `delayCall`. Listener setup does not need imported assets or loaded scenes. **Tools > Busara MCP > Start bridge / Stop bridge** controls it for the current domain. If stopped, a later domain reload restarts it. Startup failures are logged and are not retried continuously.

The bridge generates a fresh random port and token in:

```text
Busara\Library\BusaraMcp\discovery.json
```

`Library` is ignored by the project. Never copy discovery files, tokens, build outputs, or job state into version control or MCP configuration. Do not print discovery contents into logs.

## Connect an MCP client

Only the main Editor publishes discovery and runs jobs. Asset import workers never start a bridge or modify job records. Discovery and `unity_status` include the owning process ID; shutdown removes discovery only when it still belongs to that bridge.

Configure a local stdio server in your MCP client (the exact outer configuration format varies by client):

```json
{
  "mcpServers": {
    "busara-unity": {
      "command": "node",
      "args": [
        "C:\\path\\to\\checkout\\tools\\unity-mcp\\dist\\index.js",
        "--project",
        "C:\\path\\to\\checkout\\Busara"
      ]
    }
  }
}
```

Replace `C:\path\to\checkout` with your checkout's absolute path. Build first with `npm run build`. Alternatively set `UNITY_PROJECT_PATH`. Keep stdout exclusively for MCP messages; diagnostics use stderr. The server rereads discovery for each command so port/token rotation on domain reload is handled without retaining credentials in client configuration.

Verify a **real** stdio MCP initialize, tools/list, and four tools/call requests against the running Editor:

```powershell
npm run smoke -- "C:\path\to\checkout\Busara"
```

The smoke test is read-only. Its PASS line is evidence of live integration, whereas `npm test` alone uses controlled fixtures and does **not** prove Unity integration.

## Tool surface

All input schemas are advertised by MCP `tools/list`; unknown/invalid arguments are rejected. Unity instance IDs are ephemeral: reacquire them after reloads, scene changes, or destroyed objects.

| Area | Tools |
| --- | --- |
| Editor | `unity_status`, `unity_editor` (play/stop/pause/unpause/step), `unity_console`, `unity_undo` |
| Objects | `unity_hierarchy`, `unity_selection`, `unity_object_create`, `unity_object_delete`, `unity_object_reparent` |
| Components | `unity_component_add`, `unity_component_remove`, `unity_inspect`, `unity_property_set` |
| Scenes | `unity_scene_create`, `unity_scene_open`, `unity_scene_save`, `unity_scene_close` |
| Assets | `unity_assets_find`, `unity_assets_import`, `unity_assets_refresh`, `unity_assets_save`, `unity_assets_folder` |
| Creation | `unity_prefab_create`, `unity_prefab_instantiate`, `unity_material_create`, `unity_scriptable_create` |
| Scripts | `unity_script_read`, `unity_script_write`, `unity_compile` |
| Capture | `unity_screenshot` (Scene view only; Game view explicitly unsupported) |
| Automation | `unity_tests_start`, `unity_build_start`, `unity_job` |

Common workflow: status → hierarchy → inspect the returned object/component ID → edit a serialized property → save scene or assets. Use full CLR type names such as `UnityEngine.BoxCollider` for component creation. Component removal takes the **component** ID, not its GameObject ID.

Serialized property values are strings:

```json
{ "id": 1234, "property": "m_Name", "value": "New name" }
```

Supported setters: strings, booleans, integers, finite floating point values, enum indices, Vector2/3/4 and Color JSON objects, and object-reference instance IDs (`"0"` clears a reference). For example, a Vector3 value is `"{\"x\":1,\"y\":2,\"z\":3}"`. Inspect components to obtain paths and types. Internal parenting/component/prefab links cannot be edited through generic properties. Arrays, managed references, curves, gradients, quaternions, and other unsupported property types return errors.

`unity_assets_save` writes **all dirty assets** in the Editor. `unity_undo` operates on the global Editor Undo stack, including manual edits; coordinate with anyone using the same Editor.

## Destructive confirmation and persistence

Deletion, component removal, build start, active-scene close, and script overwrite require exactly:

```json
{ "confirm": "CONFIRM_DESTRUCTIVE" }
```

This is an intentional-action gate, not a second security credential. Scene root GameObjects and Transform components cannot be deleted directly. Scene close discards unsaved changes and requires another loaded scene. Scene creation/opening is additive and never discards the current scene. New assets and screenshots refuse overwrites. Saving a scene to a different already-existing path is rejected.

Scene/object/component/property changes use Unity Undo with separate command groups. **Filesystem operations are not Undoable**: script writes, asset creation/import/saving, screenshots and builds require version control/backups. Prefab-instance property overrides are recorded. Assets and scenes are not silently saved after ordinary property edits.

## Async jobs and reloads

Compilation, refresh/import, tests and builds return a `jobId`. Poll `unity_job` for `queued`, `running`, `succeeded`, `failed`, or `interrupted`. Job records and diagnostics live under ignored `Library\BusaraMcp\Jobs`. One MCP async job is allowed at a time. Compilation diagnostics and test counts/failures are persisted; callbacks are reinstalled after reload. An Editor process restart marks unfinished jobs interrupted rather than claiming success.

Test runs refuse dirty open scenes both when requested and immediately before execution; they never silently save or discard user edits. A successful test job requires a completed result, at least one passed test, and no failures or inconclusive outcomes. Mixed passed/ignored runs succeed with their skipped count reported; skipped-only runs, cancellations and inconclusive runs do not report success. Results include an `inconclusive` count. Bridge tests create a fresh working scene per test, following Unity Test Framework's scene-test pattern; the framework restores the user's saved scene setup after the run.

Builds use enabled saved Build Settings scenes and write to `Library\BusaraMcp\Builds\<jobId>`. They require an installed build module and the requested target to already be the Editor's active target. Automatic target switching is deliberately unsupported. Unity's build API blocks its main thread, but **`unity_job` remains available**: Node reads the confined per-project job record directly without an HTTP request, discovery file, or Unity API call. Writes use atomic file replacement, so polling sees either the previous complete record or the next complete record, never an in-progress write. Reads reject symlinks/junctions, mismatched IDs, invalid records and files exceeding 1 MiB.

These are persisted, last-known job states, not an Editor liveness guarantee: a crashed Editor can leave `running` until its next startup marks the job interrupted. Other tools—including `unity_status`, which asks about the Editor itself—still need a responsive Editor main thread.

**No mutation is retried automatically**, even after a timeout or disconnected domain reload. A transport error can mean that a mutation completed but its reply was lost. Inspect actual scene/assets/job state before retrying. The HTTP main-thread wait is 10 seconds; the stdio forwarder allows 15 seconds. Compilation completion may precede final domain reload: poll status before the next mutation.

## Security and boundaries

- Bind only `127.0.0.1`, use a cryptographically random 256-bit bearer token, validate exact Host/endpoint, reject browser Origin and Fetch Metadata headers, and emit no CORS permissions.
- The forwarder validates the discovery protocol/project/port/token, refuses discovery symlinks escaping the project, connects only to loopback, and does not follow redirects.
- The bridge limits bodies to 1 MiB, responses to 4 MiB, concurrent HTTP handlers to 16, queued commands to 32, body-read time to 5 seconds, console retention to 500 entries, and ordinary result sets to 500 entries.
- Asset paths must use `Assets/` with forward slashes even on Windows. Rooted paths, traversal, backslashes, alternate streams, trailing-dot/space segments, and existing symlinks/reparse points are rejected. Build/job/discovery paths are also checked.
- Paths are checked immediately before operations where practical, but this is **not an OS sandbox against a malicious local user racing filesystem changes**. Protect the project and `Library` using your OS account permissions.
- A client that can read this user's discovery file has privileged Editor access. **Script writes and adding existing scripted components can execute code with the Editor user's privileges.** Only connect trusted MCP clients and review generated code. Loopback authentication protects against unauthenticated network/browser callers; it does not isolate mutually untrusted processes under the same account.
- There is no remote/HTTP MCP listener: MCP itself is stdio; the HTTP service is a private local bridge.

## Tests and limitations

Node tests cover official MCP initialize/list/call/schema errors, discovery validation, HTTP forwarding, error propagation, timeout no-retry behavior, and path checks. Focused job tests exercise MCP polling without an Editor/discovery file, atomic status transitions, invalid IDs, bounded records and linked-directory rejection. `BusaraMcpTests` covers path/confirmation safeguards and real Unity object/component/serialized-property/Undo behavior.

Run the Editor tests through Test Runner, via `unity_tests_start` with `{"mode":"EditMode","tests":["BusaraMcpTests"]}`, or close the interactive Editor and run:

```powershell
$project = 'C:\path\to\checkout\Busara'
$results = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force -Path $results | Out-Null
& "C:\path\to\6000.3.6f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath $project `
  -runTests -testPlatform EditMode -testFilter BusaraMcpTests `
  -testResults (Join-Path $results 'mcp-editmode-results.xml') `
  -logFile (Join-Path $results 'mcp-editmode.log')
```

Do not open two Editors on the same project. To include gameplay and menu-flow regressions, combine `KingdomPowerTests;PlayerSetupTests;PlayerSetupSceneTests;BusaraMcpTests` in one `-testFilter`.

Limitations are explicit rather than simulated successes: no Game view capture, headless screenshots, arbitrary API invocation, package installation, asset deletion, automatic platform switching, historical Console scraping, or full custom-inspector/serialized-type support. Scene capture requires an open graphical Scene view and caps dimensions at 2048. Test/build execution can invoke project code. Instance IDs and the in-memory console ring do not survive reload; jobs do. A bridge compilation failure prevents startup—check Unity's Console directly.

References: [official MCP TypeScript SDK](https://github.com/modelcontextprotocol/typescript-sdk/tree/v1.x), [MCP stdio transport](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports), [Unity TestRunnerApi](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/api/UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.html).
