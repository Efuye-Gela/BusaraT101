# Reusable UGS multiplayer setup playbook

Based on the Busara setup and troubleshooting session, September 19, 2026.
Use this as a reference for future games, not as a claim that every acceptance
check passed. [Busara's detailed setup guide](ugs-setup.md) remains the
repository-specific runbook.

## 1. Choose the architecture before configuring services

For Busara, we used a server-authoritative, turn-based design:

```text
Unity Web player
  -> Unity Authentication: anonymous player identity
  -> Cloud Code C# module: validate commands and run game rules
  -> Cloud Save Private Game Data: durable matches and decisions

Local HTTPS server -> serves the built Unity files only
```

There is no VPS, PostgreSQL server or dedicated Unity game server to start for
this path. The "UGS server" is the deployed Cloud Code module plus its storage.
The local Node server does not run the game rules. The client polls Cloud Code
over HTTPS; it is not a Relay or Netcode connection.

This fits bounded, turn-based games. Do not assume it fits an action game
requiring continuous low-latency simulation. Evaluate networking, authority,
hosting and update frequency separately for each new game.

UGS does not automatically turn an offline Unity project into multiplayer.
Before this setup, we implemented shared Unity-independent rules, authenticated
commands, per-player projections, persistent decisions, a Cloud Save adapter
and the Unity online client. The scripts below are **Busara scripts**, not
commands supplied by Unity; another game needs equivalent implementation.

### Design rules worth carrying into the next game

| Concern | Pattern used here |
| --- | --- |
| Authority | Derive the player from authenticated Cloud Code context, then verify seat membership. Never trust a client-supplied player ID. |
| Hidden information | Store private state server-side and return only the requesting player's authorized view. Hiding UI is not authorization. |
| Retries | Keep command ID and body unchanged after uncertain delivery. Return the saved receipt for an exact duplicate; reject conflicting reuse. |
| Concurrency | Use Cloud Save write locks and compare-and-swap, not in-memory locks across Cloud Code workers. |
| Atomic changes | Save match state, receipts and append-only history together in the room document. |
| Recovery | Persist the pending decision, owner, legal choices, payment and continuation. A reload must not silently Pass or replace the player. |
| Capacity | Surface storage limits; do not silently discard receipts or history to fit quota. |

## 2. Gather tools and identifiers

Busara's working toolchain was Unity **6000.3.6f1** with matching WebGL support,
.NET SDK **10.0.401**, Node.js **22+**, and UGS CLI **1.9.0**. The module itself
targets **.NET 8**, not the installed SDK's major version. For future projects,
check current Cloud Code runtime support and pin compatible versions.

Install the [UGS CLI using Unity's instructions](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/install-the-cli/).
Verify it with:

```powershell
ugs --version
```

Keep these identifiers distinct:

| Value | Purpose | Secret? |
| --- | --- | --- |
| Project name, such as Busara | Human-readable label | No |
| Project ID, a UUID | Routes CLI deployment and game requests | No |
| Environment name, such as `development` | Selects the environment in our commands | No |
| Environment ID, another UUID | Used by some APIs and diagnostics | No |
| Service-account key ID and secret key | Administrator/deployment login | Treat the credential pair as private |
| Player access/session tokens | Player authentication and recovery | Yes |
| Room invitation | Permission to accept an available seat | Yes |

**A UUID-shaped service-account key ID is not a Project ID.** The CLI can
accept its format and still fail authorization because it targets the wrong
project. We encountered exactly this mistake.

For reference, this session used Busara Project ID
`a36833b4-a3ed-4a52-aa76-c52e4b735d77` and environment `development`.
**Use the new game's own Project ID for future projects.**

## 3. Configure the Unity Dashboard

1. Open [Unity Dashboard](https://cloud.unity.com/) and select the intended
   organization and project. Prefer a dedicated development project.
2. Create/select an environment named `development`.
3. Enable or configure Authentication, Cloud Code and Cloud Save as required
   by the Dashboard.
4. Review [pricing and quotas](https://unity.com/products/gaming-services/pricing)
   before deployment. Free allowances are limited, not unlimited hosting.

**Anonymous authentication is built in.** It is not an entry you must add
under **Add Identity Provider**. Our Create guest action calls the anonymous
Authentication endpoint. Google, Unity Player Accounts and other providers
were not needed for this milestone.

**Editor linking was not required for this implementation.** We used REST
from the Web client and CLI deployment. Other games that use Unity Services
SDK initialization or Editor deployment tooling may need to link their
project in **Edit > Project Settings > Services**.

## 4. Create deployment credentials and log in

Create a service account and grant its roles on the intended project. The
role set used during this session was:

- **Cloud Code Editor**
- **Unity Environments Admin**
- **Project Resource Policy Editor**
- **Project Resource Policy Reader/Viewer** (our Dashboard said Viewer)

Dashboard categories and names may change; Cloud Code Editor appeared under
LiveOps. Save the role assignments. These are deployment permissions, not
permissions to give players. Reassess least privilege for future workflows
rather than granting unrelated administrator roles.

Run:

```powershell
ugs login
```

Enter the service-account key ID and secret at the interactive prompts. Do
not paste them into source code, Web configuration, screenshots or chat.
Never use the service-account secret to authenticate the game client.

## 5. Select the repository and deployment target

Open PowerShell in the repository root, not `C:\Windows\System32`. Replace
the example path and UUID below before executing:

```powershell
Set-Location 'C:\path\to\your-game-repository'
```

```powershell
$projectId = '<your-Unity-project-UUID>'
```

Run each command separately and stop if it fails:

```powershell
ugs config set project-id $projectId
```

```powershell
ugs config set environment-name 'development'
```

```powershell
ugs cloud-code modules list
```

An empty module list can be normal before your first deployment. A 403 is
not success: check the actual Project ID, project role assignments and
environment access first. Configure the target explicitly before every
deployment rather than trusting an old CLI default.

**PowerShell lesson:** paste complete single-line commands. In our session,
a multiline build command was pasted in reverse order, making PowerShell
try to execute `-Development` as a command. Parameters belong after the
script name; the single-line form avoids backtick/continuation mistakes.

## 6. Set storage access policy and initialize private storage

Review existing policies before deploying a project-wide rule. In Busara:

```powershell
ugs deploy .\online\ugs\cloud-save-policy.ac
```

Read back the policy using an explicit target:

```powershell
ugs access get-project-policy --project-id $projectId --environment-name development
```

The intended rule denies direct Player access to `urn:ugs:cloud-save:/**`
with Action `["*"]`. Cloud Code uses its server-generated service token to
access Private Game Data. A more-specific Allow can override a broad Deny;
inspect the entire policy, especially in a shared project.

**Deployment/readback is not proof of enforcement.** Our real test found a
Default Game Data read discrepancy that remains unresolved; see section 11.
Do not weaken the rule or redefine a successful read as denial to get a pass.

For Busara only, initialize the following **once** in the selected environment:

| Dashboard location/field | Value |
| --- | --- |
| Cloud Save > Game Data > Custom Item ID | `busara_directory_v1` |
| Access class | **Private** |
| Key | `document` |
| Value type | **String**, or JSON Object with the updated adapter |
| Initial contents | `{"schemaVersion":1,"guests":{},"creates":{},"joins":{}}` |

If the Dashboard presents a raw JSON value editor and you want a String, use
outer quotes and escaped inner quotes:

```json
"{\"schemaVersion\":1,\"guests\":{},\"creates\":{},\"joins\":{}}"
```

**Never reset an existing directory.** It contains guest expiry records and
published-room references. Our first module expected a String and failed on
a Dashboard-created Object. We fixed the adapter to accept both without
destroying data; the current implementation preserves the write lock and
schema validation.

The manual initialization addresses a Cloud Save concurrency detail:
an unlocked write is not a safe create-if-absent operation. New games must
design their own safe initialization/publication scheme, not blindly reuse
Busara's directory name or schema.

## 7. Package and deploy the authoritative module

Use the actual path to your compatible .NET executable:

```powershell
.\online\scripts\package-ugs.ps1 -Dotnet 'C:\path\to\dotnet.exe'
```

This produces `online\.local\ugs-package\BusaraUgs.ccm`. Packaging is local;
it does **not** deploy. Busara packages a framework-dependent Linux x64
.NET 8 module and checks the archive size.

Deploy and inspect it:

```powershell
ugs deploy .\online\.local\ugs-package\BusaraUgs.ccm
```

```powershell
ugs cloud-code modules get BusaraUgs
```

The module/assembly name is `BusaraUgs`, without periods. Its `Execute`
function accepts string parameters `operation`, `payload` and `matchId`.
Use authenticated player calls for gameplay tests; our module deliberately
rejects administrator-only invocations without a player context.

## 8. Run a real cloud API test before calling setup complete

In the same PowerShell window:

```powershell
$env:BUSARA_UGS_PROJECT_ID = $projectId
```

```powershell
$env:BUSARA_UGS_ENVIRONMENT = 'development'
```

```powershell
$env:BUSARA_UGS_SMOKE_CONFIRM = 'development'
```

```powershell
node .\online\scripts\smoke-ugs.cjs
```

These variables belong to the current PowerShell process; set them again in
a new window. The smoke creates real anonymous test players and, if earlier
checks pass, a match. It consumes quota, leaves records in the environment
and does not retain credentials for resuming the room. If policy protection
fails, the write probe may add a `probe` key on its own test player's data.

A complete pass checks registration, direct storage denial, duplicate
requests, invitations, ownership, start, private projections and token
refresh. It does not establish Unity rendering, browser CORS, Retraction
recovery or production readiness.

For read-only Cloud Save diagnostics:

```powershell
node .\online\scripts\smoke-ugs.cjs --policy-check
```

This still creates one anonymous identity, but does not register a Busara
guest, create a match or write Cloud Save. It prints safe routing/status
information, not tokens or response bodies. It is not a replacement for
the full smoke.

## 9. Build Unity and host it locally over HTTPS

Save your work and close Unity before the isolated build. The Busara script
defaults to Unity's standard `6000.3.6f1` installation path and checks both
the editor version and matching WebGL module.

```powershell
.\Busara\Assets\Busara\Online\Editor\Invoke-BusaraOnlineBuild.ps1 -Backend ugs -ProjectId $projectId -EnvironmentName 'development' -Development
```

If Unity is installed elsewhere, append `-UnityPath` followed by the full,
quoted path to the approved `Unity.exe`. Success ends with:

```text
Built Unity OnlineMVP: ...\online\web\index.html
```

`online\web\busara-config.js` contains public routing, the module name and
polling interval. It must never contain a service-account secret. Building
Unity and deploying Cloud Code are separate operations.

### Local certificate prerequisite

You need a PFX certificate valid for `127.0.0.1` and a private JSON file
outside the repository:

```json
{"path":"C:\\private\\localhost.pfx","password":"<your-PFX-password>"}
```

Those are placeholders, not files created by UGS. In this session we reused
a certificate from earlier local tests. It covered `localhost` and
`127.0.0.1`, but was not Windows-trusted and expires **October 17, 2026**.
Do not assume that test certificate will work indefinitely or on another
machine. Provision an appropriate local certificate for future setups.

Prefer a certificate already trusted by the test browser. Where a self-signed
test certificate requires a browser exception, restrict it to the loopback
origin in an isolated test profile. Do not disable TLS checks, install global
trust as a workaround or bypass certificate errors on Unity service domains.

Use your actual private configuration path:

```powershell
$env:BUSARA_WEB_CERTIFICATE_FILE = 'C:\private\web-certificate.json'
```

```powershell
$env:BUSARA_WEB_PORT = '7443'
```

```powershell
node .\online\scripts\serve-ugs.cjs
```

Keep this terminal open and visit **https://127.0.0.1:7443**. It binds only to
loopback. Opening `index.html` directly or using Unity Editor Play Mode does
not perform this Web/UGS test.

## 10. Test with two independent players

1. Use two separate external browser profiles. Ordinary tabs share identity;
   multiple private windows may share the same private browsing session.
2. In profile A, create a guest and room. Copy the private invitation.
3. In profile B, open the invitation, create a separate guest and join.
4. Name both players, mark both ready, and start from the host.
5. Exercise setup, legal actions, costs, powers and turn transitions.
6. Reload each profile without clearing storage. Verify the same seat,
   resources, virtues, match and turn return, not merely that the page loads.
7. Reload while an off-turn decision is pending, before answering it.
   Verify the same prompt returns and payment occurs only once.
8. In a development environment, redeploy the module while a decision is
   pending, without resetting storage, then reconnect and resolve it.

Test winning, exact retries, wrong-seat attempts, concurrency and private
payload separation as well. A successful happy-path game is not the whole
acceptance suite.

Anonymous identity is stored in browser local storage; it is not portable
merely by copying a match URL. Clearing site data can permanently lose a
seat. Busara's guest lifetime is fixed at 30 days, invitations at 24 hours.
Neither changing browsers nor creating a new guest recovers an old seat.
Plan account linking/recovery deliberately for future production games.

## 11. Troubleshooting lessons from this session

| Symptom | What we learned or did |
| --- | --- |
| Could not find "Anonymous" under identity providers | Anonymous Authentication is built in; no provider needed adding. |
| `project-id 'Busara'` was rejected | Use the project's UUID, not its name. |
| CLI accepted a UUID but `GetEnvironments` returned 403 | We had used the service-account key ID as Project ID. Correct the target and verify project-scoped roles. |
| Node printed `DEP0190` | The CLI wrapper warning was separate from the observed authorization error; it did not explain the 403. |
| Registration returned `503 storage_unavailable` | Safe Cloud Code logs identified `InvalidOperationException`; the stored document was an Object instead of the original expected String. Adapter compatibility fixed this without resetting the directory. |
| Private storage probe returned 401 rather than 403 | Private Game Data is server-only. The probe now accepts 401/403 only there; normally player-accessible probes still require 403. |
| Default Game Data reads returned 200 despite policy readback | Still unresolved. Reads with and without query parameters returned 200; own-player reads returned 403. Do not mark the full smoke passed. |
| `-Development` was "not recognized" | The multiline command was reversed when pasted. Run the complete single-line build command. |
| Embedded browser said "guest session expired or unavailable" after reload | The existing identity could not be restored. Missing local identity and rejected authentication are possible causes; we did not establish which. Do not describe this as proven token expiry or deleted server state. |
| External browser profiles reloaded successfully | Continue testing there; this does not resolve the embedded-browser failure. |
| Forge selected only two resources and submitted immediately | Current online-MVP scope/UI behavior, not a UGS limitation. |

### The outstanding access-policy discrepancy

Policy readback was checked against the exact project and `development`
environment. Diagnostic results were:

| Probe | Observed HTTP status |
| --- | --- |
| Private custom data read | 401 |
| Default custom data read with query | 200 |
| Default custom data read without query | 200 |
| Own-player data read | 403 |

The diagnostic's selected environment UUID claims were absent or not UUIDs;
that alone does not prove incorrect environment routing. Default and Private
are separate access classes, so Default HTTP 200 is not by itself proof that
the private directory was disclosed.

The cause remains unknown. Preserve the policy, timestamp and safe status
output for Unity support. Ask whether policy evaluation covers Default
custom-item reads, including an item existing only in Private storage.
Do not send credentials, invitations, raw stored data or authentication
tokens. Manual gameplay continued, but did not clear this gate.

## 12. What was actually established by September 19, 2026

| Check | Evidence at the end of this session |
| --- | --- |
| Cloud authentication and Busara registration | Registration succeeded after the storage-format fix. |
| Unity UGS Web build | User's build completed successfully. |
| Two-player ordinary play | User reported setup, drawing, movement and forging working. |
| Kingdom powers | User reported Retraction and Abundance working correctly. |
| External-browser reload | User reported both external browsers reloaded correctly. |
| Reload during a pending Retraction decision | Not explicitly confirmed; ordinary reload is not equivalent. |
| Module redeployment with a pending decision | Not established in this live UGS session. |
| Full real-UGS API smoke | Blocked at the Default Cloud Save read-denial check. |
| Embedded Copilot browser recovery | Failed; cause unresolved. |
| Winning and the remaining adversarial/live-cloud acceptance cases | Not established by these manual reports. |

Earlier local adapter tests, packaging checks and legacy PostgreSQL browser
tests are separate evidence. Do not relabel them as real-UGS acceptance.
Busara online remains a two-human milestone with three kingdoms; all 15
offline powers are not available online.

## 13. Reuse checklist for the next game

- [ ] Choose a turn-based or real-time architecture appropriate to the game.
- [ ] Implement server rules, identity/seat binding, private projections,
      durable decisions and retry/concurrency behavior.
- [ ] Create a dedicated project/environment and record its correct identifiers.
- [ ] Pin compatible tools and review current quotas/pricing.
- [ ] Configure project-scoped deployment credentials; keep them out of clients.
- [ ] Deploy and verify access policy with real player requests.
- [ ] Initialize storage safely without overwriting existing records.
- [ ] Package/deploy the module, then run the real-cloud API test.
- [ ] Build the actual Unity client and serve it over appropriate HTTPS.
- [ ] Use independent browser identities and test play, reload and recovery.
- [ ] Record unresolved failures separately from successful checks.
- [ ] Before public launch, address account recovery, abuse/rate limiting,
      observability, storage growth, costs and production deployment.

For returning to an existing development setup, first confirm its target and
unresolved checks. Rebuild/redeploy only the components you changed; **do not**
reinitialize the directory, recreate credentials or reset matches each time.
Keep all tests local/private until public exposure is deliberately approved.
