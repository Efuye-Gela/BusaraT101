# One HTTPS game website with Vercel and UGS

Players open one website; they do **not** install Node.js, Unity or a local
server. Vercel serves the Unity Web files. Unity Authentication, Cloud Code
and private Cloud Save still handle identity, rooms, rules and persistence.

This guide publishes a **development game**, not a production-ready service.
Unity reported the Default Cloud Save access-policy issue fixed on September
28, 2026; Busara re-verification remains outstanding in the UGS setup guide.
Private invitations protect seats, not anonymous
signup or service usage quotas. Review that risk before publishing.

## Current development website

**https://busara-web-test.vercel.app** was updated on September 21, 2026,
in Vercel project `freadams-projects/busara-web-test`, using UGS project
`a36833b4-a3ed-4a52-aa76-c52e4b735d77` / `development`.

Signed-out HTTPS requests confirmed the Unity page and all four build assets
were available with the expected MIME/cache headers. Public configuration
points to the intended UGS environment; tested private file paths returned
404. Authentication and Cloud Code preflights allowed this origin.
Those checks did not create players or execute Unity gameplay. Two-player
hosted play, browser runtime behavior and decision recovery still require
the checks below. The Cloud Save policy gate is not verified by hosting the client.

The coordinated recovery release keeps bare command replies for older players
and adds explicit receipt-plus-view replies for the rebuilt client. The UGS
runtime now uses preprovisioned private registration shards, compact durable
receipts and legacy-record migration. The published HTML, browser adapter and
four Unity assets were compared byte-for-byte with this build.

The current player uses adaptive polling: visible waiting/lobby views check
every 2 seconds, your own turn/decision normally every 10 seconds, and hidden
or finished views at least every 30 seconds. Reload both external browser
tabs after a client update without clearing their storage. This reduces the
scheduled waiting interval, not UGS execution/network time; actual two-PC
action-to-display latency has not been measured for this update. See
[polling rates and usage costs](ugs-setup.md#persistence-privacy-and-operational-limits).

A September 20, 2026 check of the preceding client used two isolated, visible Chromium
processes with live UGS and cached copies of the verified published Web player.
Four ordinary moves took 5.07 seconds on average to appear for the opponent
(range 3.39-6.25 seconds); ten setup placements averaged 3.05 seconds
(range 2.24-3.74 seconds). No measured action required a retry. Timing ran from
the final board click to the opponent's updated development UI descriptor
across two animation frames. That descriptor publishes every 250 ms, so the
measurement includes observation overhead and is not pixel-level render timing.
This was one PC/network with software rendering, not two
physical PCs or a controlled before/after benchmark; it does not establish a
speedup. Earlier comparison attempts were inconclusive.

For those four moves, the average time before the first opponent read returning
the new version started was 3.281 seconds; that read took 1.605 seconds; the
remaining browser/observation interval was 0.184 seconds. The first interval
includes input dispatch, command/commit timing and polling, not polling alone.
The command roundtrip averaged 1.677 seconds and overlaps that timeline; it
must not be added again. No backend spans were captured, so these results do
not separately measure Cloud Save, rule execution, cold starts or network time.

The recovery release passed 56 normal UGS checks, 57 maintenance-build checks,
39 targeted Unity checks, and the browser-adapter/bridge/provisioning checks.
A real-cloud isolated old-format guest fixture recovered through GET and POST
without renewing its expiry; current API creation and old/new receipt retries
also passed. An initial across-deployment API probe was inconclusive because
its assertion handler suppressed the failure location; it is not counted as
successful live migration or pending-Retraction redeployment evidence.

A subsequent two-browser run against the recovery release completed private
create/join, a September 19 client's bare-receipt action, deliberate loss of a
committed acknowledgement followed by reload/identical-outbox recovery, and an
old-to-new client reload preserving identity and seat. Both players then
completed ten normal setup placements and four ordinary moves through Unity
controls. No measured placement or move needed a retry.

| Recovery-release sample | Count | Mean opponent display delay | Range |
| --- | ---: | ---: | ---: |
| Ordinary moves | 4 | 2.76 s | 1.45-4.86 s |
| Setup placements | 10 | 3.18 s | 1.63-8.43 s |

The acting player's four moves averaged 1.61 seconds to display; command
roundtrips averaged 0.89 seconds. These are the same one-PC/software-rendering
and descriptor-timing boundaries described above, not a controlled before/after
study. Initial guest registration needed a retry. After the recovery assertions
and all 14 gameplay samples completed, the watchdog interrupted the final
screenshot; the runner therefore exited with code 1, not an overall pass.
Both owned browsers were closed and cleanup was independently confirmed.

## 1. Build the Unity Web client

For a command-contract update, package and deploy the compatible UGS module
first using the [UGS guide](ugs-setup.md), then rebuild and publish this client.
The module retains bare `command` replies for already-open older clients;
the new player opts into `commandWithView`. Both formats retain the original
command ID/body on retry. Do not clear browser storage to resolve a mismatched
receipt: that can lose the identity or the uncertain command.

Source edits and Cloud Code deployments do not update `online\web` or Vercel.
Check the generated HTML's content-hashed build filenames and the deployed
asset bytes. A browser cache revalidation message alone does not establish
which source revision was built.

If the existing `online\web` build already has the desired code and UGS
configuration, it can be reused. Otherwise save and close Unity, then run
from the repository root, replacing the Project ID:

```powershell
.\Busara\Assets\Busara\Online\Editor\Invoke-BusaraOnlineBuild.ps1 -Backend ugs -ProjectId '<your-Unity-project-UUID>' -EnvironmentName 'development' -Development
```

The approved build uses uncompressed, content-hashed Unity files. Keep the
complete build together. Vercel does not compile Unity, run the C# module,
or require Unity licensing credentials.

## 2. Prepare a safe upload folder

```powershell
.\online\scripts\prepare-vercel.ps1
```

This prints a fresh folder under `online\.local\vercel\site-...`. It contains:

```text
vercel.json
public\
  index.html
  busara-config.js
  busara-ugs.js
  Build\
    [the four referenced Unity build files]
```

Only public configuration fields are exported. No server source, database,
Cloud Code archive, certificates, CLI credentials, browser identities or
local Node server are included. The script refuses UGS `production`, missing
files, unhashed filenames and uploads over its conservative 100 MB limit.
It never overwrites an existing export or publishes anything.

Set this PowerShell variable to the exact **Prepared site** path it printed:

```powershell
$site = 'C:\path\printed\by\prepare-vercel'
```

Do not deploy the repository root, the whole `online` directory or the
two-PC ZIP. Do not import the source repository into Vercel and expect it
to find the ignored Unity build automatically.

## 3. Sign in to Vercel on the development PC only

Use a Vercel account/team you control. Review the current
[plan terms](https://vercel.com/docs/plans/hobby) and
[limits](https://vercel.com/docs/limits); do not assume every commercial/team
use qualifies for a free Hobby plan. No paid plan or domain is required by
this code, and nothing automatically provisions one.

Check whether the CLI is installed:

```powershell
vercel --version
```

If it is missing, install it on the development PC, then sign in interactively:

```powershell
npm install --global vercel
```

```powershell
vercel login
```

Complete the browser login yourself. Do not paste tokens in chat or commit
credentials. Players do not need these tools or a Vercel account when the
game's production domain is publicly accessible.

Some CLI versions create `.vercel` metadata and an `.env.local` file when
linking a folder. That environment file can contain a Vercel-issued token:
never display, share or commit it. Keep CLI files outside `public`; subsequent
exports start in a fresh directory and never copy those files.

## 4. Publish only after approving public access

This command uploads the prepared files and can make the game publicly
reachable. Run it only when ready:

```powershell
vercel deploy --prod --archive=tgz --cwd "$site"
```

The archive compresses the upload only; the served Unity files stay unchanged.
It reduced this development build's transfer from about 48 MB to 14 MB when
retrying a failed upload. Check the project's deployment list after a network
failure before retrying the same prepared folder.

Review interactive prompts rather than using `--yes` blindly:

- Select your intended account/team.
- Create a dedicated project, for example `busara-web-test`, or select the
  same existing project for subsequent uploads.
- The project directory is the prepared folder (`.` if prompted from there).
- Use framework **Other**, no install/build command, and output **public**.
  The supplied `vercel.json` defines these settings.
- Do not add UGS service-account credentials or connect backend services to Vercel.

**Vercel `--prod` and UGS `production` are different things.** Here `--prod`
updates the stable Vercel project domain; the game still calls the UGS
`development` environment embedded in its public configuration.

Use the stable project domain shown in the Vercel Dashboard, such as
`https://busara-web-test.vercel.app`, not a different preview URL for every
test. The example hostname is not reserved or guaranteed to be available.

Vercel deployment protection can require visitors to sign in, especially on
preview/unique deployment URLs. Verify the stable domain in a signed-out
browser. If it is protected, review the project's intended access settings
before changing them; the scripts do not disable protection or use bypass
tokens. Never put a deployment-protection secret in an invitation URL.

## 5. Play from two PCs on any networks

1. Both PCs open the same stable HTTPS website in external browsers.
2. Player A creates a guest and room, then copies the game's invitation.
3. Player B opens that invitation, creates a separate guest and joins.
4. Both ready up, then the host starts. Each browser talks directly to UGS.

Invitations automatically use the current website origin/path. You do not
need localhost, port forwarding, a local certificate or the Node server.

The Vercel origin has different browser storage from localhost. Existing
localhost identities do not transfer by opening a match URL. For this test,
create new guests and a new room. Keep the stable origin and browser profile
when reloading; changing domains or clearing storage can lose the local
identity needed for a seat.

## 6. Verify the actual hosted experience

- The signed-out browser can load the website and complete the Unity loader.
- Build `.wasm` responses have `Content-Type: application/wasm`; missing assets
  return errors rather than the entry HTML. There is no catch-all SPA rewrite.
- Authentication and Cloud Code requests succeed from the new origin. Local
  browser success does not establish hosted CORS behavior.
- Both seats can join, play and reload with the same state.
- Reload before answering a pending Retraction Use/Pass prompt; verify the
  same decision returns and payment is applied once.

If the Unity loader fails, inspect the failing asset's HTTP status, MIME type
and any deployment-protection page. If UGS calls fail, inspect the CORS
preflight/status, not just the game's loading screen. Do not export HARs,
tokens or invitation secrets as diagnostics.

The headers prevent caching the entry/config/adapter and allow immutable
caching only for content-hashed Build files. `Referrer-Policy: no-referrer`
reduces accidental disclosure. `noindex` requests search-engine exclusion;
it is **not access control**.

## Updates and implementation boundaries

For a client change, rebuild Unity, prepare a fresh site, and deploy it to
the **same Vercel project**. For a server rule change, deploy Cloud Code
separately. Never reset the private Cloud Save directory as a deployment step.

`StaticHostingProvider` owns generic static export and file/size checks.
`VercelHostingProvider` extends it with Vercel configuration and its upload
budget. Unity build selection and public UGS configuration are separate
helpers shared with local packaging. Future hosting adapters can extend the
base without putting provider-specific code into game rules. This is not
yet a refactor of all existing UGS runtime code.

The export scripts do not create a deployment, publish source, add DNS,
install global trust, start billable services or alter your Git branches.
Any hosted play results must be recorded separately from local/export tests.

References: [Vercel CLI deployment](https://vercel.com/docs/cli/deploy),
[static configuration](https://vercel.com/docs/project-configuration/vercel-json),
[deployment protection](https://vercel.com/docs/deployment-protection).
