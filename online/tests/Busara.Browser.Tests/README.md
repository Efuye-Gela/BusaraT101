# Real two-browser Unity acceptance

**Legacy ASP.NET/PostgreSQL backend only.** For the default UGS backend, use
[UGS setup and cloud/browser smoke](../../../docs/ugs-setup.md). This suite's
owned backend restart and positive WSS assertions do not apply to UGS polling.

These NUnit tests drive the **actual Unity Web canvas**, not an HTML board or a
command API surrogate. Each human seat has a separate Chromium browser context,
guest cookie and outbox. The only JavaScript UI observation is the development
build's read-only `window.busaraVisibleUi` descriptor. Coordinates are normalized
to `#unity-canvas`, with a top-left origin. Mutations are mouse clicks on those
Unity controls. Invitations are obtained with Unity's Copy invitation control and
the browser clipboard. The normal-play strategy reads only authorized network
`ClientView` responses and uses public rules to select the next visible choice.

## Prerequisites

- .NET SDK **10.0.401** and Chromium installed by the project's pinned
  **Microsoft.Playwright 1.62.0** installer.
- A real development Unity Web build (`index.html`, `Build/*.wasm*`) containing
  the online scene and visible-control descriptors.
- PostgreSQL on `127.0.0.1`, with an existing `busara_test` database and a role
  permitted to create/drop schemas. The fixture migrates its own isolated schema.
- A file-only HTTPS certificate for `127.0.0.1`.
- An unused unprivileged loopback HTTPS port.

The fixture launches and owns a real backend child process, migrates the test
database explicitly, health-checks it, and stops only that process. It does not
start/stop PostgreSQL, install services, alter certificate trust, expose public
listeners, or use Docker. No database or pre-existing schema is truncated.
Each fixture creates its own schema in `busara_test` and a private
32-byte key; both remain identical across the backend restart. Cleanup removes
only that generated schema. `Testing` plus `BUSARA_TEST_RANDOM=fixed-zero` enables the server's
test-only deterministic random source; it is not an HTTP seed endpoint, a
production setting, or inventory seeding.

Supply these process-local environment variables before `dotnet test`:

| Variable | Value |
| --- | --- |
| `BUSARA_BROWSER_DOTNET` | Absolute path to `dotnet.exe` |
| `BUSARA_BROWSER_SERVER_DLL` | Built `Busara.Server.dll` with its dependencies |
| `BUSARA_BROWSER_WEB_ROOT` | Absolute real Unity Web build directory |
| `BUSARA_BROWSER_ARTIFACTS` | Existing private evidence directory |
| `BUSARA_BROWSER_ORIGIN` | `https://127.0.0.1:54443` or another unused port |
| `BUSARA_BROWSER_CONNECTION_FILE` | Private JSON: `host` must be `127.0.0.1`, `port`, `username`, `password`, `testDatabase` must be `busara_test` |
| `BUSARA_BROWSER_CERTIFICATE_FILE` | Private JSON: certificate `path` and `password` |
| `PLAYWRIGHT_BROWSERS_PATH` | Directory containing the pinned Chromium installation |
| `BUSARA_BROWSER_HEADED` | Optional `1` for headed execution |

The certificate JSON's `path` is an absolute PFX path and `password` its password.
Keep both configuration files and the evidence directory private and outside
tracked source. Certificate/database provisioning is a prerequisite; there is
no checked-in all-in-one provisioning or browser-runner script.

First build the player from the repository root using
`.\Busara\Assets\Busara\Online\Editor\Invoke-BusaraOnlineBuild.ps1 -Development -Backend legacy`
(pass `-UnityPath` for a non-default installation). Then configure the variables
above. Set `DOTNET_ROOT` and the process `PATH` for a portable SDK.

Run the following steps **one at a time**, stopping on nonzero exit. From the
repository root, change into `online` so its `global.json` selects the SDK:

```powershell
Set-Location .\online
& $env:BUSARA_BROWSER_DOTNET build .\tests\Busara.Browser.Tests\Busara.Browser.Tests.csproj
.\tests\Busara.Browser.Tests\bin\Debug\net10.0\playwright.ps1 install chromium
& $env:BUSARA_BROWSER_DOTNET build .\src\Busara.Server\Busara.Server.csproj
& $env:BUSARA_BROWSER_DOTNET test .\tests\Busara.Browser.Tests\Busara.Browser.Tests.csproj `
  --no-build --results-directory $env:BUSARA_BROWSER_ARTIFACTS `
  --logger 'trx;LogFileName=browser-tests.trx'
```

For these default Debug builds, `BUSARA_BROWSER_SERVER_DLL` must point to
`online\src\Busara.Server\bin\Debug\net10.0\Busara.Server.dll` in your checkout;
the Web root is `online\web`. Install Chromium once initially or after changing
the Playwright version, not on every run. No separate running game server is
required: the fixture owns its backend and refuses an occupied test port.

Missing prerequisites **fail** the tests. They are never ignored or reported as
passing skips. Certificate-error acceptance exists only in these isolated test
contexts, whose HTTP requests are restricted to the one configured HTTPS origin.
Server TLS validation/configuration is not weakened.

## Evidence and coverage

The tests create/join a private room through Unity, change each seat's own
readiness, verify distinct supported private kingdoms, place the authored
resources, and earn Art/Security through legal moves/forges. They verify:

- The off-turn Retraction owner, ID, prompt and revision survive reactor reload
  and death/restart of the owned backend using the same PostgreSQL database.
- Both fixed guest cookies and memberships survive, without silent new guests.
- Exact selected, normally earned payment remains spent after board rollback,
  reveal, revision advance and Continue to the correct next seat.
- An actual Unity Use request commits but loses its ACK; the visible Retry
  identical pending action control resends the same outbox body/ID and incurs no
  second payment or revision.
- A separate normal-play match exercises Pass without payment, reveal or rollback.
- Continued real draw/place/move/forge clicks reach an authored kingdom victory,
  not a stalemate.
- Projection/receipt schemas are strict; opponent setup/payment identities,
  unrevealed kingdoms, non-owner decisions, snapshots and future decks are not
  accepted. WSS messages contain only match/version invalidations.

Evidence contains canvas-only PNGs, sanitized network schema checks and public
milestone counts. Raw HTTP bodies, HARs, traces, cookies, clipboard contents,
invites and secret files are deliberately not saved. Screenshots show the
viewer’s own visible game information; handle the evidence directory privately.

These tests do not replace the server suite's concurrency, wrong-seat, stale,
forged-payment, hidden-virtue fixture or multiple-worker tests. A passing compile
or API suite is **not** passing two-browser evidence: report the actual NUnit
result and screenshots from an executed Unity Web run separately.

The bounded `TradeWeaponAndChainUseRealControlsAndRestorePendingDecisions`
case exercises a new v2 match through actual Unity controls: trade rejection
without losing the turn, an accepted exact-resource swap after responder
reload, a cross-board weapon with a defender discard restored after backend
restart, and an ordered three-resource forge that earns two authored virtues.
It uses only native setup resources and legal moves, not inventory seeding.
Run it with `--filter TradeWeaponAndChainUseRealControlsAndRestorePendingDecisions`
for the ordinary-action smoke; it does not replace the longer victory flow.

`LegacyBrowserReceivesAuthenticatedWebSocketInvalidation` is a short handshake
regression. Browser HTTP/2 WebSockets use `CONNECT`, while HTTP/1.1 uses `GET`;
both routes retain the same origin, guest, membership and CSRF checks. Safe
handshake failure categories are recorded without URLs, tokens or headers.
The observer associates requests with their document generation: intentional
navigation/disposal or an observed failed transport can cancel body inspection.
Those responses are explicitly marked incomplete, not reported as valid JSON
or schema violations. Complete responses still pass the strict projection and
receipt checks, including responses from an older document.
