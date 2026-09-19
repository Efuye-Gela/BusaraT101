# Local PostgreSQL and authoritative server

**Optional legacy backend.** The default online build now uses UGS; see
[UGS setup and a simple test](../../docs/ugs-setup.md). To use this server, build
the Web player with `Invoke-BusaraOnlineBuild.ps1 -Backend legacy`. There is no
automatic fallback or migration between PostgreSQL matches and UGS matches.

Requires .NET SDK **10.0.401** and an actual local PostgreSQL instance. No in-memory fallback exists. The service binds **IPv4 loopback HTTPS only** and refuses missing/unmigrated databases or a changed persistent secret. Do not expose it publicly.

Configure these environment variables in your local terminal; never put their values in tracked files:

| Variable | Purpose |
| --- | --- |
| `ConnectionStrings__Busara` | Npgsql connection string for the game database; use a least-privilege application role. Legacy `BUSARA_DATABASE` is accepted only if this is unset |
| `BUSARA_ORIGIN` | Exact HTTPS origin, e.g. `https://localhost:7443`; no path, query, or fragment. May instead use a single matching `ASPNETCORE_URLS` URL; if both are set they must agree |
| `BUSARA_SECRET_KEY` | Base64 encoding of exactly 32 cryptographically random bytes; **persist securely across restarts and workers** |
| `ASPNETCORE_Kestrel__Certificates__Default__Path` | Absolute path to an untracked trusted local HTTPS PFX |
| `ASPNETCORE_Kestrel__Certificates__Default__Password` | PFX password |
| `BUSARA_WEB_ROOT` | Optional absolute Unity build directory; the start script defaults to `online\web` |
| `BUSARA_TEST_DATABASE` | Separate disposable **loopback** PostgreSQL database; test role needs schema creation/removal |

The local provisioning convention is PostgreSQL on `localhost:55432`, databases `busara` and `busara_test`, and non-superuser login `busara_dev` owning both. Load its random password from your private untracked connection configuration without echoing it. Tests create/drop only their uniquely named `busara_test_*` schema inside the explicitly selected test database; they never truncate shared tables or reset the database.

Use a trusted localhost certificate. Visit exactly the configured origin. A browser using IPv6-only localhost resolution can instead use a trusted certificate covering `127.0.0.1` and configure that origin. There are no production certificate-validation exceptions. Tests use their own short-lived self-signed certificate, real HTTPS server processes, and a unique PostgreSQL schema; only the test HTTP clients bypass certificate validation.

From a PowerShell terminal at the checkout root:

```powershell
.\online\scripts\migrate-server.ps1 -Dotnet C:\path\to\dotnet.exe
.\online\scripts\test-server.ps1 -Dotnet C:\path\to\dotnet.exe
.\online\scripts\start-server.ps1 -Dotnet C:\path\to\dotnet.exe
```

The test script optionally accepts `-ConnectionFile C:\private\connection.json` with `host`, `port`, `username`, `password`, and `testDatabase` fields. It loads the test connection only into the current process environment, restores it afterward, and never echoes credentials. `-OutputPath C:\private\sanitized-server-test-output.txt` writes sanitized build/test output, redacting the configured password/connection string, guest cookie tokens, CSRF fields, and invitation fragments. Paths should be absolute. This script connects to an already running database; it does not start PostgreSQL.

Scripts validate exit codes and do not install tools or silently initialize a replacement database. Migration takes an advisory transaction lock, applies `001_initial.sql` atomically, and records schema/key identity. Startup only checks migration state; it never runs migrations implicitly. `GET /api/health/ready` verifies database availability, schema and key. PostgreSQL 16+ is recommended. Normal shutdown uses the running terminal; tests terminate only the exact worker processes they create.

Build the Unity Web client **without compressed output**, into `online\web`. Start with `start-server.ps1` so content-root resolution is correct, or set `BUSARA_WEB_ROOT` explicitly when launching the DLL. `.wasm` and `.data` MIME types are explicitly supported; there is no replacement HTML game engine. A missing Web build returns an explicit 503 at `/` while API/database readiness remains independently available.

## Identity and protocol

- Explicit `POST /api/guest` creates a 256-bit opaque token only when no guest cookie exists. Cookie is `__Host-busara`, `Secure`, `HttpOnly`, `SameSite=Strict`, path `/`, with a fixed 30-day expiration. PostgreSQL stores its SHA-256 hash only. `GET /api/guest` restores the same identity/CSRF token/expiration without renewal.
- A present invalid/expired cookie is an explicit auth error, never silently replaced. The limited milestone has no seat recovery. Browser cookie expiration eventually removes the cookie; the client must still explicitly request a new guest rather than automatically create one when reconnecting.
- Mutations require the exact configured `Origin` and `X-CSRF-Token`. Initial guest creation requires the origin but cannot require a prior CSRF token. No CORS or proxy-header trust is configured.
- Browser WebSockets offer `["busara.v1", "csrf." + csrfToken]`; the server selects `busara.v1`. This avoids CSRF secrets in URLs. WSS requires origin, valid cookie, CSRF, and membership. The server rechecks database membership/expiry each second and closes on failure.
- WSS sends **only** `{matchId, version}` invalidations. Each worker polls committed database revisions, so there is no worker-local notification dependency. Reconnect and every invalidation require authorized GET of the current projection.
- Requests are bounded to 16 KiB, unknown JSON members are rejected, and IDs are UUID strings. DTO fields are included explicitly with `System.Text.Json.IncludeFields = true`. Versions are decimal strings.
- Commands lock the match row `FOR UPDATE`, authorize membership before receipt lookup, return identical actor/fingerprint duplicates **before** stale-version checks, and reject changed-body or changed-actor reuse. Typed serialization yields the fingerprint independently of incoming JSON property order. State, append-only event and receipt commit together before ACK.
- Domain rule rejections are durably recorded with a bounded safe receipt; transport/auth/invalid-shape failures do not write gameplay. An uncertain response must retry the **identical command ID/body**. A stale rejection requires fresh projection and deliberate reselection.
- Create/join use guest-level durable receipts and guest-row locking. Invitation consumption and joining use the match lock too; competing guests cannot occupy the same seat.

## Invitation secret at-rest design

Create derives a 256-bit invitation using HMAC-SHA-256 over a purpose-separated domain, guest UUID and command UUID, keyed by `BUSARA_SECRET_KEY`. PostgreSQL stores only the invitation hash plus expiry/consumption. Create receipts store the original safe URL prefix, match ID and version, **not the invite secret**. On an identical authenticated retry the secret is derived again, including after restart; this is why the external key must remain stable and backed up separately from the database. Losing the key is not recoverable by changing it; startup rejects its mismatched fingerprint.

The invite appears only in the URL fragment (`/#invite=...`), is exchanged in a POST body, and must be removed by the browser immediately. It expires after 24 hours or consumption (and cannot join a started room). It claims only empty seat 1; it cannot recover or impersonate an existing seat. Retrying the original create may show the same now-consumed/expired invitation, not mint a new one.

Never enable HTTP body/header logging, Npgsql parameter logging, analytics, or URL capture around invite handling. Default server request logging is disabled; API errors omit database/stack/payload details. Snapshots contain hidden game state and belong only in protected PostgreSQL storage/backups. Apply operational filesystem/database permissions locally.

## Integration test contract

`test-server.ps1` builds the real host and runs `Busara.Server.Tests`; missing PostgreSQL is a **failure**, not a skip. The fixture starts two independent OS host processes against one isolated PostgreSQL schema, tests concurrent commands/invite claims, independent match locks, forced transactional rollback, and safe WSS invalidations. Fresh host processes verify durable guest/create/join/state/receipt restoration, a pending resource placement, and a pending off-turn Retraction with normally earned Art/Security payment. The Retraction test follows the authored setup and four-turn move/forge path, restarts the process, verifies both private projections/payment IDs, undoes, retries across workers, and checks exactly-once spending and turn continuation. Test certificates live only below build output and are removed on cleanup. An interrupted test may leave a uniquely named `busara_test_*` schema or `.runtime` folder; inspect ownership before manual cleanup.

The test role can alter expiry directly to exercise expiration. For reproducible real-process integration/browser tests, set both `ASPNETCORE_ENVIRONMENT=Testing` (and `DOTNET_ENVIRONMENT=Testing` if set) and `BUSARA_TEST_RANDOM=fixed-zero`. This injects `IGameRandom.Next(max) => 0`, leaving the forward-shuffled authored deal Egolica/Mask and deck order intact. Any attempt to set this flag outside Testing fails startup. Without the flag, randomness remains cryptographic, including in Testing. There is no RNG/state-seeding HTTP endpoint or inventory/debug endpoint. Never enable the Testing environment for normal gameplay.
