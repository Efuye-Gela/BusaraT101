# UGS multiplayer: setup and a simple test

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

## 4. Initialize the private directory ONCE

In this environment's **Cloud Save > Game Data**, create a Custom Item with
ID **`busara_directory_v1`**. Select its **Private** access class, then add:

| Setting | Value |
| --- | --- |
| Key | `document` |
| Value type | **String** (a JSON Object is also accepted by the updated module) |
| String contents | `{"schemaVersion":1,"guests":{},"creates":{},"joins":{}}` |

The contents are also in [directory-value.json](../online/ugs/directory-value.json).
If the Dashboard uses a raw JSON value editor instead of a String selector,
use a JSON string literal (including its outer quotes):

```json
"{\"schemaVersion\":1,\"guests\":{},\"creates\":{},\"joins\":{}}"
```

If you already created `document` as a **JSON Object**, leave its contents
intact. The updated module accepts both formats, validates the same schema,
and preserves the write lock. Its next successful update stores a JSON String
without dropping existing guests, room references or join reservations.

**Never reset or overwrite an existing directory.** Check the project,
environment and Private tab first. Replacing it destroys published-room
lookup and guest expiry records. It is not a routine migration or test reset.
No player-callable bootstrap function exists.

Why the manual step: Cloud Save write locks protect updates, but an unlocked
create can overwrite an existing key. Starting from a known existing record
lets concurrent Cloud Code workers publish room creation with compare-and-swap
instead of risking a match reset.

## 5. Build and deploy the module

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

The module exposes **`Execute`**, with string parameters `operation`, `payload`
and `matchId`. Calls originate from authenticated players; running it as an
administrator without a player context is deliberately rejected.

## 6. Run the small real-UGS API smoke test

This creates **two real anonymous test players and a private match** in the
selected environment. It refuses `production`, never logs player tokens or
invitations, and does not delete shared data. Its credentials live only in
process memory, so this test room cannot be resumed after the process exits.
If access control is misconfigured, its write-denial probe can create a `probe`
key on that test player's own data; it never writes an existing match/directory.

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
policy, module and private directory rather than bypassing checks.

This is a real cloud **API** test, not a Unity/browser/CORS or Retraction test.

## 7. Build and open the Unity game

Close the Unity Editor before the isolated batch build. From the repository root:

```powershell
.\Busara\Assets\Busara\Online\Editor\Invoke-BusaraOnlineBuild.ps1 `
  -UnityPath 'C:\path\to\6000.3.6f1\Editor\Unity.exe' `
  -Backend ugs -ProjectId '<your-Unity-project-UUID>' `
  -EnvironmentName 'development' -Development
```

The generated `online\web\busara-config.js` contains public project/environment
routing, the module name and `pollSeconds` (default 10). No service-account
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

## 8. Simple two-browser play test

1. Open the URL in **two separate browser profiles**. Ordinary tabs share
   local storage and an identity; two private windows may also share a profile.
2. In browser A, explicitly create a guest, create a room and copy its invitation.
3. Open that invitation in browser B, explicitly create its guest and join.
4. Give both seats different names and mark both ready. The host starts.
5. Place the five setup resources on your own non-adjacent spaces. Take a
   normal draw/place or move turn. The other browser should update within the
   configured polling interval; the connection label says **UGS - HTTPS polling**.
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
- Room creation initializes a fresh random candidate, then CAS-publishes its
  receipt in the existing directory. Losing or interrupted creators can leave
  **inaccessible orphan candidates**; they cannot overwrite a published room.
  The private directory retains the invitation secret for identical create
  retries; individual room records retain its hash. Neither is player-readable.
  Treat populated Dashboard records as private, not shareable diagnostics.
  Join request identities are reserved in that directory before room mutation,
  preventing cross-room ID reuse. An interrupted/rejected reservation remains
  bound to its original request; retry it identically or use a new request ID.
  There is no automatic garbage collector. Do not delete/reset the directory
  to reclaim storage, or delete records based solely on creation age.
- Cloud Save permits 5 MiB per Custom Item/access class. Busara stops writes
  at 2 MiB of inner JSON per document, leaving string-encoding headroom.
  `storage_capacity_reached` is explicit; history/receipts are never silently
  evicted. The shared directory is also finite and can become a contention
  point. This is a bounded private milestone, not a production storage design.
- Polling is intentional; no custom WSS endpoint, Relay or Netcode server is
  used. At 10-second polling, two idle open seats make about 12 Cloud Code
  calls and 24 Cloud Save reads per minute, before actions/auth/retries.
- UGS matches do not import the legacy PostgreSQL database or browser cookies.
  The [legacy backend](../online/db/README.md) remains explicit `-Backend legacy`.
- Anonymous signup and Cloud Code endpoints are Internet-accessible once you
  deploy. Private invitations protect seat membership, not your service quota.
  Production abuse prevention/account recovery are not implemented; use a
  dedicated development project and monitor usage before sharing the player.

## Local checks and evidence boundary

### Host registration reports storage_unavailable

The original module accepted only a JSON String for the private `document`
value. A Dashboard-created JSON Object caused `InvalidOperationException`
and a safe `503 storage_unavailable` response. If you used Object, **do not
reset the directory**: rebuild and redeploy the updated `BusaraUgs` module
using Step 5, then rerun Step 6. The storage adapter now accepts both formats.
If the error persists, inspect the Cloud Code error type and configuration;
do not publish document contents, tokens or invitations.

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

With the three Step 6 environment variables still set, run:

```powershell
node .\online\scripts\smoke-ugs.cjs --policy-check
```

This creates **one anonymous test identity**, but does not register a Busara
guest, create a room or write Cloud Save. It prints only the configured target,
allowlisted project/environment UUID claims, a UTC timestamp and four read
statuses. Tokens, player IDs and response bodies are never printed. It compares
Default reads with/without query parameters and an own-player read; all must
return 403, while the Private read may return 401 or 403. The decoded token
claims are diagnostic routing information, not independent token verification.
Compare the environment UUID with **Project Settings > Environments** in the
Dashboard. If routing and the deployed policy match but reads still succeed,
retain these diagnostics for Unity support; do not bypass the failing gate.
This diagnostic does not replace the full smoke, and needs no module deployment.

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
