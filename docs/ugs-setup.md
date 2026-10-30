# UGS multiplayer: setup and a simple test

For the end-to-end design and adaptation to another project, start with
[Multiplayer architecture and hosting](multiplayer-architecture-and-hosting.md).

For the guided setup recap, troubleshooting lessons and a checklist for future
games, see the [reusable UGS multiplayer playbook](ugs-multiplayer-playbook.md).

Busara's default online build uses **Unity Authentication + Cloud Code C#
modules + private Cloud Save Game Data**. Unity hosts the authoritative game
logic. You do not start an ASP.NET server, PostgreSQL or a VPS for this route.
The actual game remains the Unity Web player, not a replacement web board.

The rules are unchanged: two invited humans and three supported kingdoms,
not full offline parity. See [online rules](online-multiplayer.md).

## What you need

- A Unity account and a **dedicated Busara UGS project** you can administer.
- A non-production environment named `development` (or your chosen name).
- Unity **6000.3.6f1**, with its matching WebGL module.
- .NET SDK **10.0.401**, selected when commands run under `online`.
  The Cloud Code module itself targets `net8.0`; do not retarget it to .NET 10.
- Node.js **22+** for the local static server and smoke scripts.
- The [UGS CLI](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/install-the-cli/).
- For manual local browser play, an HTTPS certificate valid for your loopback
  origin. No script installs certificates or changes global trust.

Keep Unity package versions/lockfile as committed. This Web client calls
UGS's documented REST APIs; adding Relay, Netcode or Unity client SDK packages
is not required. Deployment credentials must never enter the Web build.

## 1. Enable services in Unity Dashboard

Open your project in [Unity Dashboard](https://cloud.unity.com/), create/select
the `development` environment, and enable **Authentication**, **Cloud Code**
and **Cloud Save**. Anonymous sign-in is built into Unity Authentication:
**do not add an identity provider**. It does not appear in the Dashboard's
**Add Identity Provider** menu and needs no provider-specific configuration.
Busara's **Create guest** action calls Unity's anonymous authentication endpoint;
you do not need Unity Player Accounts, Google or Username & Password for this
milestone. See [Unity's anonymous sign-in guide](https://docs.unity.com/en-us/authentication/use-anon-sign-in).
Record your **Project ID (UUID)** and **environment name**, not the numeric
organization ID or environment UUID.

Check the [current UGS pricing](https://unity.com/products/gaming-services/pricing)
and usage/billing controls before enabling services. Free tiers are quotas;
polling, test accounts, storage and repeated smoke tests consume them. Neither
this repository nor the scripts guarantee that usage remains free.

## 2. Authenticate the deployment CLI

Follow Unity's [CLI authentication instructions](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/get-authenticated/).
Create a service account scoped to this project with the required Cloud Code,
environment and Access Control deployment permissions. Keep its key/secret in
the CLI's supported credential flow, not source, screenshots or command logs.
It is an administrator credential, **never a player credential**.

Configure the CLI explicitly before every deployment:

```powershell
ugs config set project-id '<your-Unity-project-UUID>'
ugs config set environment-name 'development'
```

Run commands separately and stop on failure. Nothing below deploys to a
project unless you run the deployment commands yourself.

## 3. Lock down direct player storage access

From the repository root:

```powershell
ugs deploy .\online\ugs\cloud-save-policy.ac
ugs access get-project-policy
```

The policy denies **all direct player Cloud Save reads and writes**. Cloud
Code accesses private Game Data with its server-generated service token.
Players can call `BusaraUgs.Execute`, but it derives the actor from Unity's
authenticated context and checks room membership before returning a projection.

**This is a project access policy.** Do not apply it blindly to a project
shared with other games/services. Inspect existing policies: a more-specific
Allow may override the broad Deny. The smoke test below requires normally
player-accessible reads and writes to return 403; the server-only Private
read may return 401 or 403. Do not weaken the policy to make a
failing smoke test pass.

## 4. Build and deploy the module

The runtime requires 64 preprovisioned **Private** registration shards:
`busara_registration_v1_00` through `busara_registration_v1_63` (decimal
suffixes), each with key `document` and value
`{"schemaVersion":1,"registrations":{}}`. The public layout is in
`online/ugs/registration-layout.json`. The deployment service account needs
project-scoped Private Game Data read/write permission; Cloud Code Editor
alone is insufficient.

Each shard CAS-publishes immutable guest-document pointers and fixed expiry.
Guest ledgers live at fresh random candidate IDs, initialized before
publication. Missing shards return `registration_not_initialized`; there is
no public bootstrap or unsafe deterministic-key initialization.

From the repository root:

```powershell
.\online\scripts\package-ugs.ps1 -Dotnet 'C:\path\to\dotnet.exe'
```

This publishes a Linux x64, framework-dependent .NET 8 module and produces
ignored `online\.local\ugs-package\BusaraUgs.ccm`. It does **not** deploy.
It checks native exit status and the documented 10 MB archive limit.
The module name and assembly name are **`BusaraUgs`**, without periods.

Deploy the resulting archive to the configured development environment:

```powershell
ugs deploy .\online\.local\ugs-package\BusaraUgs.ccm
ugs cloud-code modules get BusaraUgs
```

### Upgrading existing guests safely

Prepare and validate both module packages and the new Unity build before
interrupting service. Save the currently deployed module for rollback before
activation; never automatically roll back to an old writer after new guest
registrations have been published.

1. Build the maintenance module with
   `.\online\scripts\package-ugs.ps1 -Maintenance`. Its archive is
   `online\.local\ugs-package\maintenance\BusaraUgs.ccm`.
2. Deploy that archive to the explicit project/development environment. It
   returns `503 storage_upgrade_in_progress` before authentication/storage
   access. Browser outboxes remain intact.
3. Confirm maintenance is visible, then wait at least 60 seconds to drain old
   invocations. Unity documents a 15-second execution limit, after which the
   worker is killed. Use one deployment operator for provisioning.
4. Inspect the shards without changing them:

   ```powershell
   node .\online\scripts\provision-ugs-registration.cjs --project-id '<project-UUID>' --environment development --layout .\online\ugs\registration-layout.json
   ```

5. During the confirmed maintenance window, run the same command with
   `--apply --maintenance-confirmed`. It creates only missing private shard
   documents, verifies them, and never overwrites an existing shard. It waits
   for all initializers to finish before returning. Keep maintenance enabled
   if provisioning fails. On Windows it uses the npm-installed native
   `ugs.exe`; use `--cli '<native-CLI-path>'` for another installation.
6. Deploy the normal, non-maintenance `BusaraUgs.ccm`, then publish the freshly
   built Unity player using the [Vercel guide](vercel-hosting.md).
   Confirm old/new client receipts and identity/outbox recovery.

For a new environment, provision before making the runtime available to
players. The maintenance confirmation is an operator safety requirement,
not a claim that a read-then-upsert is an atomic create primitive.

Old `busara_directory_v1` and deterministic per-player guest documents are
read-only migration sources. Migration copies both ledgers into a fresh
candidate and preserves the earliest existing expiry; conflicting reservations
fail explicitly rather than choosing a new identity. Old join hashes do not
encode recoverable actor ownership, so legacy reservations are conservatively
copied, with the same explicit capacity limit. Published schema-1 rooms keep
their state, pending decisions, receipts and history; orphan candidates remain
inaccessible. Do not delete or reset legacy records during this process.

The module exposes **`Execute`**, with string parameters `operation`, `payload`
and `matchId`. Calls originate from authenticated players; running it as an
administrator without a player context is deliberately rejected.

## 5. Run the small real-UGS API smoke test

This creates **two real anonymous test players and a private match** in the
selected environment. It refuses `production`, never logs player tokens or
invitations, and does not delete shared data. Its credentials live only in
process memory, so this test room cannot be resumed after the process exits.
If access control is misconfigured, its write-denial probe can create a `probe`
key on that test player's own data; it never writes an existing match record.

```powershell
$env:BUSARA_UGS_PROJECT_ID = '<your-Unity-project-UUID>'
$env:BUSARA_UGS_ENVIRONMENT = 'development'
$env:BUSARA_UGS_SMOKE_CONFIRM = 'development'
node .\online\scripts\smoke-ugs.cjs
```

Expected: `PASS: real UGS registration, ...` and exit code 0. It checks direct
Cloud Save denial, concurrent duplicate room creation/commands, invitation
acceptance/retry, unauthorized access, readiness/start, hidden projections and
session-token refresh. Stop on any failure; check the selected environment,
policy and module rather than bypassing checks.

This is a real cloud **API** test, not a Unity/browser/CORS or Retraction test.

## 6. Build and open the Unity game

Close the Unity Editor before the isolated batch build. From the repository root:

```powershell
.\Busara\Assets\Busara\Online\Editor\Invoke-BusaraOnlineBuild.ps1 `
  -UnityPath 'C:\path\to\6000.3.6f1\Editor\Unity.exe' `
  -Backend ugs -ProjectId '<your-Unity-project-UUID>' `
  -EnvironmentName 'development' -Development
```

The generated `online\web\busara-config.js` contains public project/environment
routing, the module name and `pollSeconds` (default 10 for your own turn or
decision). Visible waiting/lobby views poll every 2 seconds; hidden tabs and
finished matches use at least 30 seconds. No service-account
secret belongs there. Missing UGS configuration blocks the build/client; it
never starts the legacy backend as a fallback.

For a local static HTTPS host, prepare a private JSON file outside the repo:

```json
{"path":"C:\\private\\localhost.pfx","password":"<your-PFX-password>"}
```

Use a certificate valid for `127.0.0.1` and trusted by the browser you use.
Then run:

```powershell
$env:BUSARA_WEB_CERTIFICATE_FILE = 'C:\private\web-certificate.json'
$env:BUSARA_WEB_PORT = '7443'
node .\online\scripts\serve-ugs.cjs
```

Keep that terminal open and visit **`https://127.0.0.1:7443`**. This process
serves files only and binds to loopback; the rules/storage run in UGS.
Opening `index.html` directly, or Play Mode in the Editor, is not this test.
For isolated automated browser tests, any certificate exception must be
restricted to the local test origin; never disable TLS verification for UGS
or modify global trust as a workaround.

## 7. Simple two-browser play test

For the normal player experience across different PCs/networks, use
[Vercel HTTPS hosting](vercel-hosting.md). Both players open one hosted
website; neither needs Node or a local server. The
[portable two-PC setup](two-pc-testing.md) is only an alternative developer
test when you do not want to publish a website.

1. Open the URL in **two separate browser profiles**. Ordinary tabs share
   local storage and an identity; two private windows may also share a profile.
2. In browser A, explicitly create a guest, create a room and copy its invitation.
3. Open that invitation in browser B, explicitly create its guest and join.
4. Give both seats different names and mark both ready. The host starts.
5. Place the five setup resources on your own non-adjacent spaces. Take a
   normal draw/place or move turn. A visible waiting browser polls every
   2 seconds, plus Cloud Code/network time to receive the projection; this is
   not a guaranteed two-second delivery time. The connection label says
   **UGS - HTTPS polling**.
6. Reload either browser. Its identity, seat and match should return without
   reopening the invitation. No replacement guest should be created.

For the persistence/reaction check, follow the
[earned Retraction recipe](online-multiplayer.md#earning-a-retraction-payment-through-play).
It needs Mask of Light in seat 2; dealing remains random. Reload that browser
while its off-turn decision is pending. For a module-redeployment check,
redeploy the same module without changing Cloud Save, then reload: the same
decision must remain. Use the exact earned payment once, or Pass, and continue.
Do not seed inventory or force a kingdom in the production module.

If Node smoke passes but browser calls fail, inspect the browser's Network
panel for the Authentication/Cloud Code **CORS preflight or HTTP status**.
Do not export HARs or copy Authorization/session/invite values into reports.
CORS behavior from your actual static origin must be verified separately.

## Persistence, privacy and operational limits

- The unchanged shared C# domain is authoritative in Cloud Code. Clients
  receive only `Projection.ForSeat`, never raw Cloud Save documents.
- UGS player IDs are mapped to seats server-side. A Busara guest expires
  30 days after first registration; token refresh and re-registration do not
  extend this. Invitations are single-use with a fixed 24-hour expiry.
- The UGS refresh/session token is stored in browser local storage; access
  tokens stay in memory. This is **not the legacy HttpOnly-cookie model**.
  Treat the static host as trusted code: third-party scripts/XSS can read
  browser-held credentials. Clearing storage can permanently lose a seat.
- Browser outboxes retain exact command strings/IDs. Project/environment
  names scope their storage. An uncertain call is retried identically.
- Each published room has its own private Custom Item. One CAS saves match,
  receipts and append-only event history together. Conflicts retry against
  the new stored revision; duplicates are checked before staleness.
- Room creation initializes a fresh random candidate, then CAS-publishes it by
  recording the win in the creating player's published guest ledger,
  then flipping the room's `published` flag. Losing
  or interrupted creators can leave **inaccessible orphan candidates**; they
  cannot overwrite a published room, and an unpublished room is never returned
  by `view`/`command` regardless of how its ID was obtained. That same
  per-actor document retains the invitation secret for identical create
  retries; individual room records retain its hash. Neither is player-readable.
  Treat populated Dashboard records as private, not shareable diagnostics.
  Join request identities are reserved in that same per-actor document before
  room mutation, preventing cross-room ID reuse. An interrupted/rejected
  reservation remains bound to its original request; retry it identically or
  use a new request ID. There is no automatic garbage collector for a player's
  own create/join history — it stays for that player's account lifetime — but
  unlike the old single shared directory, its size and any contention scale
  with how many rooms *that one player* has made, not with the whole game's
  player count.
- Cloud Save permits 5 MiB per Custom Item/access class. Busara stops writes
  at 2 MiB of inner JSON per document, leaving string-encoding headroom.
  `storage_capacity_reached` is explicit; append-only event history is never
  silently evicted. Compact command/join receipts are retained for the room's
  lifetime. Another seat can submit commands while an acknowledgement is lost,
  so a shared 16-receipt window is not safe even with one outbox per browser.
  Projections are generated for replies instead of saved with every receipt.
  Receipts already evicted by a previously deployed version cannot be recovered
  from event history; the server never invents an acknowledgement for them.
  This is a bounded private milestone, not a production storage design.
- Polling is intentional; no custom WSS endpoint, Relay or Netcode server is
  used. A provider-neutral client scheduler uses the authorized `awaitingOther`
  projection, including off-turn decisions, not just the active seat:
  visible waiting/lobby views use 2 seconds; your own turn/decision uses
  `pollSeconds` (default 10, bounded to 5-60); hidden/finished views use
  `max(30, pollSeconds)`. Returning to a visible tab requests a fresh view.
  Requests never overlap; slow responses and browser suspension can delay
  updates. Failures back off to 10/20/40/60 seconds, or the current interval
  when longer, capped at 60. Visibility changes do not bypass failure backoff.
  Ordinary scheduled reads pause during a command. New clients request
  `commandWithView`, applying the actor-only view with the receipt and switching
  the polling interval immediately when ownership changes. Missing or stale
  embedded views trigger a normal read. Old clients keep the bare-receipt
  `command` operation and their follow-up read. Older-than-confirmed projections
  retry after 0.5 seconds.
- With default settings and fast responses, one waiting and one active seat
  make roughly 36 Cloud Code calls / 72 Cloud Save reads per minute, compared
  with 12 / 24 under the old fixed 10-second schedule. Both visible lobby seats
  can approach 60 calls / 120 reads. These estimates exclude commands, auth,
  foreground refreshes and retries. Monitor UGS usage; faster polling is not
  push delivery and does not reduce the backend's execution time.
- UGS matches do not import the legacy PostgreSQL database or browser cookies.
  The [legacy backend](../online/db/README.md) remains explicit `-Backend legacy`.
- Anonymous signup and Cloud Code endpoints are Internet-accessible once you
  deploy. Private invitations protect seat membership, not your service quota.
  Production abuse prevention/account recovery are not implemented; use a
  dedicated development project and monitor usage before sharing the player.

## Local checks and evidence boundary

### Host registration reports storage_unavailable

Check the private registration-shard preflight first. A missing shard returns
`registration_not_initialized`; provision it only through the maintenance
procedure above. Existing malformed records, incompatible schemas or conflicting
migration ledgers require investigation, not resetting records. Inspect safe
Cloud Code error types and configuration; do not publish document contents,
tokens or invitations.

### Direct Cloud Save access check fails

The smoke script reports the actual HTTP status alongside the expected denial,
without printing response bodies or credentials. A different status is not
automatically proof of readable data: authentication, routing, rate limiting
and service failures also fail this gate. Do not relax permissions or treat
an arbitrary error as a pass. Check `ugs access get-project-policy` against
the intended project/environment and report the failing probe's HTTP status.
Updating this local diagnostic script does not require module redeployment.

The Private Game Data endpoint is server-only and can reject a valid player
token with **401**, before evaluating the project policy. The updated smoke
test accepts **401 or 403 only for that private read**. It still requires
**403** for Default Game Data reads and the player's own data writes, which
would otherwise be player-accessible. Thus private 401 alone does not prove
the deny policy is installed. HTTP 200, 404, 429 and service failures never pass.

If the policy looks correct but Default reads still return 200, verify its
target explicitly (replace the project UUID):

```powershell
ugs access get-project-policy --project-id '<your-Unity-project-UUID>' --environment-name development
```

With the three Step 5 environment variables still set, run:

```powershell
node .\online\scripts\smoke-ugs.cjs --policy-check
```

This creates **one anonymous test identity**, but does not register a Busara
guest, create a room or write Cloud Save. It prints only the configured target,
allowlisted project/environment UUID claims, a UTC timestamp and four read
statuses, with redacted method/route labels. Tokens, player IDs and response
bodies are never printed. It compares Default **Game Data/Custom Items** reads
with/without a `keys=document` filter and an own-player **Player Data Default**
read; all must
return 403, while the Private read may return 401 or 403. The decoded token
claims are diagnostic routing information, not independent token verification.
Compare the environment UUID with **Project Settings > Environments** in the
Dashboard. If routing and the deployed policy match but reads still succeed,
retain these diagnostics for Unity support; do not bypass the failing gate.
This diagnostic does not replace the full smoke, and needs no module deployment.

When comparing results with support, distinguish the API families:
[Default Game Data](https://docs.unity.com/en-us/cloud-save/concepts/game-data)
is normally readable by any player; [Default Player Data](https://docs.unity.com/en-us/cloud-save/concepts/player-data)
is normally readable only by its owning player. The project's deny policy is
an additional restriction being tested, not a replacement for that distinction.
`GET /custom/{customId}/items?keys=document` uses a key filter, **not the Query
API**. A Player Data Query API result therefore does not reproduce this probe.
The September 19 diagnostic used `customId=busara_directory_v1`; the current
script uses a preprovisioned registration shard. Neither targets player-owned
items for the unexpected Default Game Data results. The diagnostic discards
response bodies, so HTTP 200 alone does not establish that any Private-class
document was returned. Unity Support reported the service-side issue fixed
on September 28, 2026 (ticket 3524761), with both Default reads now returning
403. This has not yet been independently reverified in Busara. Rerun the
existing diagnostic to establish local evidence; do not relax the gate.

### Local validation

No cloud credentials are needed for:

```powershell
Push-Location .\online
try {
  dotnet test .\tests\Busara.Ugs.Tests\Busara.Ugs.Tests.csproj
  if ($LASTEXITCODE -ne 0) { throw 'UGS adapter tests failed.' }
} finally { Pop-Location }
node .\online\tests\ugs-transport.test.cjs
node .\online\tests\ugs-policy-probe.test.cjs
node .\Busara\Assets\Busara\Online\Client\Tests\bridge.test.cjs
```

Adapter tests simulate independent workers and CAS storage; they cover normally
earned Retraction Use/Pass, lost ACK, unchanged history, room/command races,
ownership, privacy, fixed expiry and capacity failure. They are **not proof
of live Cloud Save concurrency or browser CORS**. The historical PostgreSQL/
WSS two-browser results in the online guide do not validate this UGS backend.
Cloud deployment and the real smoke/two-browser checks are user-run gates;
no UGS project was provisioned or deployed by this change.

Official contracts: [Cloud Code packaging](https://docs.unity.com/en-us/cloud-code/modules/how-to-guides/package-code),
[REST invocation](https://docs.unity.com/en-us/cloud-code/modules/how-to-guides/run-modules/rest-api),
[player authentication](https://docs.unity.com/en-us/services-web-apis/client-auth),
[Cloud Save Game Data](https://docs.unity.com/en-us/cloud-save/concepts/game-data),
[write locks](https://docs.unity.com/en-us/cloud-save/concepts/write-locks),
[access control](https://docs.unity.com/en-us/services/access-control).
