# Working on Busara

This is repository-wide guidance for coding agents. Start with [README.md](README.md)
and use [CONTRIBUTING.md](CONTRIBUTING.md) for setup and validation recipes.

## Scope and source map

Busara has two deliberately separate modes: the offline Unity game supports all
15 kingdom powers; the online milestone supports two invited humans and only
Egolica/Abundance, Mask of Light/Retraction and N'evulandis/Infinite Knowledge.
Do not imply full offline feature parity online.

| Location | Responsibility |
| --- | --- |
| `Busara` | Nested Unity project; open this directory, not the repository root |
| `Busara/Assets/Busara/script` | Offline Core, Managers, Actions, PowerEffect, UI, bots and Editor tests/tools |
| `Busara/Assets/Busara/Online/Shared/Runtime` | Unity-independent contracts, domain rules and per-seat projections |
| `Busara/Assets/Busara/Online/Client` | Unity online presentation/session transport |
| `Busara/Assets/Busara/Online/Editor` | Online scene/build tooling and client tests |
| `Busara/Assets/Plugins/WebGL/BusaraOnline.jslib` | Credentialed browser transport, reconnect and immutable outboxes |
| `online/src/Busara.Ugs`, `online/ugs` | Default Cloud Code module, private Cloud Save CAS and deployment policy |
| `Busara/Assets/WebGLTemplates/BusaraOnline` | Public UGS routing and browser Authentication/Cloud Code REST adapter |
| `online/src/Busara.Server`, `online/db` | Explicit legacy ASP.NET Core/PostgreSQL backend |
| `online/tests`, `online/scripts` | Adapter/domain/browser checks, packaging and local runners |
| `tools/unity-mcp` | Optional local Editor MCP; not the multiplayer backend |

## Invariants

- Authored assets and existing offline rules/tests are the rule source of truth.
  Reuse shared pure helpers where possible; do not invent inventory grants, stock
  limits, turn skips or simplified payment rules to make a scenario work.
- Online state belongs to the server. Authenticate guest-to-seat membership, not
  a client-supplied player ID. Use stable domain IDs, never Unity instance IDs,
  GameObjects or serialized callbacks.
- Preserve command ID/body on uncertain retries. Check exact duplicates before
  staleness, enforce decision ownership/version, and commit state, receipt and
  append-only events atomically. Concurrency must work across backend workers.
- Keep deck order, snapshots, hidden kingdoms/virtues and private payment choices
  out of unauthorized projections, receipts, errors and logs. UI hiding is not
  an authorization boundary.
- Retraction must preserve exact owned payment, snapshot affordability,
  already-revealed/acquired knowledge and continued turn order; undo advances the
  version and never erases history. Pending decisions survive reload/restart.
- Preserve fixed guest/invite expiry and explicit recovery errors. Disconnects
  never silently Pass, forfeit, replace a human with a bot or start offline play.
  See [online rules and protocol](docs/online-multiplayer.md).
- UGS is the default, legacy is opt-in. Never expose raw Cloud Save data to
  players or initialize existing room/guest keys without CAS. Guest, create and
  join ledgers live in each player's published guest document. Preprovisioned
  registration shards publish immutable actor-to-document mappings using CAS;
  missing shards fail closed. Never initialize deterministic mutable guest keys
  with an unlocked write, or reintroduce a global writable directory. Old writers
  must be drained in maintenance before migration. Preserve state/receipt/history together atomically.
  Keep compact receipts for every command and join; never evict a pending
  client's receipt because another seat submitted more commands. Build private
  projections for replies, not for durable receipt storage. Capacity exhaustion
  is explicit; neither receipts nor append-only event history are evicted.
  See [UGS setup and limits](docs/ugs-setup.md). No automatic cloud deployment.

## Editing safeguards

- Check status/diff first and preserve unrelated edits. Work only in the intended
  checkout; verify Editor/MCP project identity before sending operations.
- Keep Unity **6000.3.6f1**, its matching WebGL module and the approved package
  lock resolution. .NET SDK **10.0.401** is selected by `online/global.json`
  when running from `online`. Do not casually upgrade packages or platform settings.
- Preserve `.meta` GUIDs, asset references, scene contents and existing build scene
  indices. Add metadata for new Unity assets. Review import/build serialization
  changes separately; never blindly restore files containing someone else's work.
- Do not edit source during a coordinated Unity import/test/build. Save scene
  changes explicitly; never silently save/discard another person's dirty scene.
- Keep generated `Library`, `Temp`, `online/web`, `bin`, `obj`, test artifacts,
  databases and credentials out of commits. Never log cookies, CSRF/invite tokens,
  connection strings, discovery credentials or hidden server state.
- Use loopback HTTPS locally. Certificate exceptions belong only to isolated test
  clients and their local origin; do not disable production TLS or modify global
  trust as a workaround. No public listeners/deployment without explicit approval.

## Smallest useful validation

Run commands from the repository root unless stated otherwise; prerequisites and
exit-code handling are in [CONTRIBUTING.md](CONTRIBUTING.md).

| Change | First relevant check |
| --- | --- |
| Shared rules | From `online`: `dotnet test .\tests\Busara.Domain.Tests\Busara.Domain.Tests.csproj`; also Unity `BusaraOnlineParityTests` when rules/assets change |
| Browser bridge | `node .\Busara\Assets\Busara\Online\Client\Tests\bridge.test.cjs` |
| UGS module | From `online`: `dotnet test .\tests\Busara.Ugs.Tests\Busara.Ugs.Tests.csproj` |
| UGS browser adapter | `node .\online\tests\ugs-transport.test.cjs` |
| Offline/client Unity code | EditMode Test Runner; select affected fixtures, then related power/setup tests |
| Legacy server/persistence | `.\online\scripts\test-server.ps1` with real disposable loopback PostgreSQL configured |
| Web integration | UGS: [cloud/browser smoke](docs/ugs-setup.md); legacy: [two-browser suite](online/tests/Busara.Browser.Tests/README.md), with explicit build backend |
| MCP | From `tools\unity-mcp`: `npm test`; live smoke is a separate Editor integration check |
| Documentation only | Check links, paths and commands against source; no game build required |

Missing infrastructure is not a passing test. Distinguish simulated CAS, actual
UGS, domain, PostgreSQL, Unity build and browser evidence; report unexecuted gates.
Update directly related documentation when behavior or setup changes.
