# Contributing to Busara

Read [README.md](README.md) for setup and [AGENTS.md](AGENTS.md) for the code map,
gameplay/privacy invariants and editing safeguards. Keep offline behavior and
the explicitly limited online variant distinct.

## Before editing

Inspect `git status --short` and the existing diff. Use the intended checkout
and the pinned Unity editor; do not overwrite unrelated edits or upgrade
packages/platform settings as a side effect. Preserve asset `.meta` GUIDs and
scene references. Stop/coordinate Editor work before an isolated batch test or
build, and verify any MCP connection identifies this checkout.

For a rules change, compare the authored kingdom/setup/recipe assets, existing
offline implementation and shared online helpers first. Add focused tests for
the changed behavior and update the relevant linked guide. Keep secrets,
private match state, build output and test artifacts untracked.

## Validation recipes

Examples use Windows PowerShell. Replace installation/private paths and run
from the repository root unless a command changes directory. Run each step
separately and inspect its exit code/result before continuing. For a portable
.NET installation, set `DOTNET_ROOT` to its installation directory and add that
directory to the current process's `PATH`.

### Shared domain and browser bridge

Run .NET commands from `online` so its `global.json` selects SDK 10.0.401
(`latestPatch` roll-forward):

```powershell
Push-Location .\online
try {
  dotnet test .\tests\Busara.Domain.Tests\Busara.Domain.Tests.csproj
  if ($LASTEXITCODE -ne 0) { throw 'Domain tests failed.' }
} finally { Pop-Location }

node .\Busara\Assets\Busara\Online\Client\Tests\bridge.test.cjs
```

Shared source changes also need Unity compilation. For rule/catalog changes,
run `BusaraOnlineParityTests` against the actual authored assets.

### Unity checks

Use **Window > General > Test Runner > EditMode**, selecting the smallest
affected fixtures. The milestone regression selection was
`OnlineClientTests;BusaraOnlineParityTests;KingdomPowerTests;PlayerSetupTests;PlayerSetupSceneTests`.
Other bot/playtest/MCP edits need their own relevant fixtures; this selection
does not cover everything in the project.

For the same selection in an isolated batch Editor, close the interactive
Editor first and use an absolute executable path:

```powershell
$unity = 'C:\path\to\6000.3.6f1\Editor\Unity.exe'
$project = (Resolve-Path .\Busara).Path
$results = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force -Path $results | Out-Null
& $unity -batchmode -nographics -buildTarget WebGL -projectPath $project `
  -runTests -testPlatform EditMode `
  -testFilter 'OnlineClientTests;BusaraOnlineParityTests;KingdomPowerTests;PlayerSetupTests;PlayerSetupSceneTests' `
  -testResults (Join-Path $results 'editmode-results.xml') `
  -logFile (Join-Path $results 'editmode.log')
```

Do not add `-quit` to the asynchronous test invocation. Wait for the Editor to
exit, then inspect the fresh XML and log; stale XML or a launch exit alone is
not a pass. This recipe uses the matching WebGL module. Review generated asset,
lockfile and project-setting diffs after any import/test/build.

### UGS backend and Web integration

UGS is the default backend; follow [UGS setup](docs/ugs-setup.md) for deployment,
one-time private directory initialization and real-cloud/browser smoke checks.
No player-facing bootstrap or direct Cloud Save access may be added. Never
replace CAS with an unconditional update, or evict receipts to fit quota.

```powershell
Push-Location .\online
try {
  dotnet test .\tests\Busara.Ugs.Tests\Busara.Ugs.Tests.csproj
  if ($LASTEXITCODE -ne 0) { throw 'UGS adapter tests failed.' }
} finally { Pop-Location }
node .\online\tests\ugs-transport.test.cjs
.\online\scripts\package-ugs.ps1 -Dotnet 'C:\path\to\dotnet.exe'
```

Build Unity with `-Backend ugs -ProjectId '<project-UUID>' -EnvironmentName
'development'` using the existing build script. Local adapter tests use
simulated CAS; the real `smoke-ugs.cjs` and two-browser steps are separate,
explicit cloud operations. Do not count the legacy WSS suite as UGS coverage.

### Legacy backend and real browser integration

Prepare an already running disposable loopback PostgreSQL database and the
private configuration described in the [server guide](online/db/README.md).
From the repository root:

```powershell
.\online\scripts\test-server.ps1 -Dotnet 'C:\path\to\dotnet.exe' `
  -ConnectionFile 'C:\private\connection.json' `
  -OutputPath 'C:\private\server-test-output.txt'
```

Use its `-Filter` parameter for a focused .NET test selection when appropriate.
The runner builds the host and executes real process/database tests; missing
PostgreSQL is a failure, not a skip or an in-memory substitute.

Client/protocol integration changes additionally require a real Web build:

```powershell
.\Busara\Assets\Busara\Online\Editor\Invoke-BusaraOnlineBuild.ps1 `
  -UnityPath 'C:\path\to\6000.3.6f1\Editor\Unity.exe' -Development -Backend legacy
```

Then follow the [two-browser README](online/tests/Busara.Browser.Tests/README.md)
to build the fixture/server, install its pinned Chromium and supply private
test settings. Development enables read-only visible-control descriptors;
tests must still click the actual Unity canvas. Do not count compilation,
HTTP-only tests or fabricated inventory as end-to-end gameplay evidence.

### Optional MCP

From `tools\unity-mcp`, use `npm ci` for initial dependency installation and
`npm test` for the TypeScript/Node suite. The [MCP guide](tools/unity-mcp/README.md)
has a separate read-only live smoke command; it requires the correct Unity
project open and does not follow from unit-test success.

## Before handing off

Run `git diff --check`; when preparing a commit also inspect
`git diff --cached --check`, since ordinary diff does not check untracked files.
Review the exact changed/staged paths and keep generated files, credentials and
private screenshots/logs out. Summarize behavior changes, executed checks,
failures and remaining unverified scenarios. Documentation-only changes require
link/path/command checks, not a Unity build or database run.
