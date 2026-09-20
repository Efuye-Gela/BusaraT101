# Two Windows PCs on different networks

UGS hosts the multiplayer rules and storage. Each PC only needs its own copy
of the Unity Web files and a loopback HTTPS server. Both copies talk to the
same UGS project/environment over the Internet.

No router changes, port forwarding, public listener, Unity installation,
UGS CLI login or deployment credentials are needed on the second PC.
Use this for private development testing, not a production launch.

## Prepare the ZIP on the development PC

If you received a ZIP containing this document as `START-HERE.md`, skip to
**Prepare the second PC** below.

Use the existing configured Unity Web build, or rebuild after changing client
code. From the repository root:

```powershell
.\online\scripts\package-ugs-web-test.ps1
```

The script prints the ZIP path and SHA256 hash. It packages only the build,
public UGS routing, local scripts and these instructions. It refuses the
production environment. It does not deploy anything or contact UGS.

Copy that ZIP to the other PC, using a private transfer method you trust.
Do not copy the repository, private tooling directory, certificates, browser
profile or deployment credentials. Anyone with this client can attempt to
use the deployed development services, so do not publish the bundle publicly.

## Prepare the second PC

1. Install **Node.js 22 or newer** from https://nodejs.org/, then open a new
   PowerShell window so it sees the updated PATH.
2. Right-click the received ZIP, open **Properties**, and use **Unblock** if
   Windows provides that option and you trust the sender. Then **Extract All**.
   Do not run the files from inside the ZIP.
3. Open the extracted folder in File Explorer. It should contain `web`,
   `scripts` and `START-HERE.md`. Type `powershell` in its address bar and
   press Enter. This opens PowerShell in the correct directory.
4. Confirm Node is available:

```powershell
node --version
```

5. Start the local server with this single-line command:

```powershell
.\scripts\start-ugs-web-test.ps1 -CreateCertificate
```

On first use, the launcher creates a 30-day self-signed certificate for
`127.0.0.1` and `localhost`, stored with its private configuration in
`%LOCALAPPDATA%\Busara\LocalWebTest`. Only your Windows user has access to
that new directory. The script removes its temporary Personal certificate
store entry after export; it does not install a trusted root, change firewall
rules or alter PowerShell execution policy.

If Windows policy blocks script execution, do not disable it system-wide.
On an unmanaged personal PC, after reviewing/trusting the scripts, you can
use a process-only policy for this launcher:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\scripts\start-ugs-web-test.ps1 -CreateCertificate
```

If organizational policy still blocks it, ask the administrator; do not
work around managed policy.

Wait for:

```text
Unity static player: https://127.0.0.1:7443 (UGS backend)
```

Keep this window open. To stop the local file server, press **Ctrl+C**.

## Keep the first PC running too

If its local server is already serving the game on port 7443, leave it
running; do not start a duplicate.

Otherwise, from the repository root, use the private certificate configuration
you already used successfully (replace the example path):

```powershell
.\online\scripts\start-ugs-web-test.ps1 -CertificateFile 'C:\private\certificate.json'
```

Alternatively, run `.\online\scripts\start-ugs-web-test.ps1 -CreateCertificate`
to create/reuse the launcher's default per-machine certificate. Do not share
one PC's private key or copy its browser identity to the other.

## Open the game and invite the second PC

1. On each PC, open an external browser profile used only for testing and visit
   **https://127.0.0.1:7443**. Use exactly this origin on both machines.
2. The self-signed certificate can trigger a browser warning. If the browser
   offers **Advanced > Proceed**, the exception is only for this local address
   in the isolated test profile. Do not disable browser TLS checks or bypass
   errors on Unity's service domains. If policy prevents proceeding, use an
   appropriately provisioned certificate instead.
3. On PC A, explicitly create a guest and a **new room**. Copy its private
   invitation and send it privately to PC B.
4. On PC B, open that invitation in the same test profile, create its guest
   and accept. Do not post the invitation in screenshots or public messages.
5. Name both players, ready both seats and start from PC A.

**Why the localhost invitation works:** `127.0.0.1` always means the PC opening
the URL. PC B loads its own Web copy, then its client sends the invitation to
the shared UGS backend. It does not connect to PC A's Node server. Do not
replace the URL with PC A's IP address.

A normal match URL is not an invitation and does not transfer a seat. Start
a new room when testing new browser identities. Keep the same browser
profile, origin and site storage when resuming a seat. Existing rooms stay
in UGS when a local file server is stopped.

## What to check

- Both browsers show different seats in the same room.
- Setup, drawing, moving, two-resource forging and turn ownership synchronize.
- Reload each browser without clearing storage; the same seat and board return.
- If Mask of Light is dealt, earn a legal payment and reload **before answering**
  an off-turn Retraction prompt. The same decision must return, payment must
  occur only once, and play must continue in the correct order.

Two PCs on separate networks provide new connectivity evidence. They do not
by themselves verify module-redeployment recovery, all failure cases or the
unresolved Default Cloud Save access-policy discrepancy.

The earlier session confirmed ordinary external-browser reload and manual
Retraction/Abundance use. Copilot's embedded browser failed to restore a guest
after reload for an undiagnosed reason; use external profiles for this test.
The live API smoke still failed because a Default Game Data read returned
200 instead of the required 403. Do not weaken that gate or describe this
bundle as production-ready.

## Returning later and troubleshooting

On the second PC, reopen PowerShell in the extracted folder and run:

```powershell
.\scripts\start-ugs-web-test.ps1
```

| Symptom | Action |
| --- | --- |
| `node` is not recognized | Install Node 22+ and reopen PowerShell. |
| `EADDRINUSE` | Port 7443 is already occupied. Use the existing Busara server or stop the known process yourself; do not kill unrelated processes. |
| Site cannot be reached | Confirm the local Node server is running on **that PC**, with the exact HTTPS address and port. |
| Certificate expired | Create a new private certificate directory using `New-WebTestCertificate.ps1`, then pass its `certificate.json` with `-CertificateFile`. Do not delete browser storage. |
| Certificate directory already exists but configuration is missing | Initialization was incomplete. Choose a new private directory; do not overwrite unknown files. |
| Guest unavailable after changing browsers | The original identity belongs to the original profile/origin. A match URL cannot recover it. |
| Invitation rejected | Check that it is unexpired, unused, and both builds show the same project/environment. |
| Changes do not appear in the other game | Verify the same match, turn, project/environment and Internet connectivity. Allow the configured polling interval (normally 10 seconds). |

Keep both PCs on the same bundle version. After changing the Web client,
rebuild and create a new ZIP rather than mixing files from different builds.
Cloud Code changes require their separate deployment workflow.
