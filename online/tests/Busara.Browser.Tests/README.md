# Real two-browser Unity acceptance

These NUnit tests drive the **actual Unity Web canvas**, not an HTML board or a
command API surrogate. Each human seat has a separate Chromium browser context,
guest cookie and outbox. The only JavaScript UI observation is the development
build's read-only `window.busaraVisibleUi` descriptor. Coordinates are normalized
to `#unity-canvas`, with a top-left origin. Mutations are mouse clicks on those
Unity controls. Invitations are obtained with Unity's Copy invitation control and
the browser clipboard. The normal-play strategy reads only authorized network
`ClientView` responses and uses public rules to select the next visible choice.

## Prerequisites

- .NET 10 SDK and the project's pinned Playwright Chromium installation.
- A real development Unity Web build (`index.html`, `Build/*.wasm*`) containing
  the online scene and visible-control descriptors.
- PostgreSQL on `127.0.0.1`, with the explicitly migrated `busara_test` database.
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
| `BUSARA_BROWSER_CONNECTION_FILE` | Private JSON: `host`, `port`, `username`, `password`, `testDatabase` |
| `BUSARA_BROWSER_CERTIFICATE_FILE` | Private JSON: certificate `path` and `password` |
| `PLAYWRIGHT_BROWSERS_PATH` | Directory containing the pinned Chromium installation |
| `BUSARA_BROWSER_HEADED` | Optional `1` for headed execution |

```powershell
dotnet test .\online\tests\Busara.Browser.Tests\Busara.Browser.Tests.csproj `
  --logger 'trx;LogFileName=browser-tests.trx'
```

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
