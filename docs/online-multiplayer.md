# Private online multiplayer: first playable milestone

Online MVP is an explicit, reduced Busara rules variant: `busara-online-mvp-v1`.
The existing offline game remains separate. This is not support for all fifteen
kingdom powers, a public matchmaking service, or a production deployment.

## Supported play

Two invited humans receive different kingdoms randomly from **Egolica
(Abundance)**, **Mask of Light (Retraction)**, and **N'evulandis (Infinite
Knowledge)**. Neither player chooses the other's kingdom; lobby readiness does
not disclose the deal.

The game uses the first two authored boards, their original eight-column global
slot coordinates, and their five-resource setup cards. Setup placement is on
your own empty spaces, not orthogonally adjacent to another resource on your
board. The deck contains the scene's 24 resource cards, six of each resource
type, without its four disaster cards.

Ordinary actions are drawing and placing a resource, moving an owned resource
to an adjacent empty participating space, and forging **exactly two** adjacent
resources of different types. A forge must start with your resource. It consumes
the pair and gives the corresponding virtue to each distinct board owner
represented by that pair. Moves and forges can cross the boundary between the
two participating boards.

| Kingdom | Power | Victory requirements |
| --- | --- | --- |
| Egolica | Abundance: reveal your hidden kingdom and add up to two resources; no virtue payment. | 2 Security, 3 Nature, 4 Economy |
| Mask of Light | Retraction: pay two owned virtues after the other player's action to undo it. | 3 Art, 4 Security, 2 Economy |
| N'evulandis | Infinite Knowledge: pay one owned virtue to learn the opponent's kingdom privately. | 4 Energy, 2 Wisdom, 3 Economy |

The existing resource/virtue recipes and kingdom requirements are the source
of truth. Abundance checks the existing power stock limit of 20 resources per
type and available board space; any unplaceable remainder is explicitly
reported. The existing ordinary drawing/forging paths do not enforce the power
helpers' stock caps, and this milestone does not silently add such a rule.

Win checks occur after reactions, at turn end, checking the just-active player
before the other player. Retraction ends that action's turn; it does not grant
a replacement action. The existing empty-board skip/stalemate rule is retained.
This is distinct from disconnection: no timeout chooses Pass, forfeits a player,
or substitutes a bot.

**Unavailable online:** the other twelve powers, disasters, trading, weapons,
longer forge chains, bots, tournaments, more than two players, spectators, and
public matchmaking. Use offline play for the full local rules.

## Earning a Retraction payment through play

The following uses **zero-based slot labels** from the online board. It requires
Mask of Light in seat 2; production dealing is random, not forced.

Place seat 2's setup Fire resources at `4`, `13`, and `20`, Water at `6`, and
Earth at `15`. These five spaces obey setup adjacency rules. On four separate
turns, with the opponent taking their turns between:

1. Move Water `6 -> 5`.
2. Forge Fire `4` and Water `5` to earn Art.
3. Move Earth `15 -> 14`.
4. Forge Fire `13` and Earth `14` to earn Security.

The Fire at `20` remains on the board. After the next opposing action, the
reactor can pay the earned Art and Security. There are no starting virtues,
debug resource grants, or gameplay state-seeding endpoints.

## Authority, privacy, and reconnecting

The server owns match state. The Unity Web client sends commands and renders
its authorized projection; it does not run the offline managers as a second
authority. Hiding a Unity panel is not an information-security boundary.

Guest credentials are 256-bit opaque tokens in Secure, HttpOnly, SameSite
cookies. PostgreSQL stores their hashes and a durable guest-to-seat mapping,
not a trusted player ID supplied in a command. A guest has a **fixed 30-day
expiry**. Browser reload and backend restart preserve a valid identity; they do
not rotate it. Clearing the cookie or reaching expiry can make a seat
unrecoverable: this milestone has no accounts or seat-recovery service.

Invitations are separate, high-entropy, single-use, **24-hour** secrets. An
invitation can claim the one empty invited seat, not impersonate an existing
player or recover their secrets. The invitation is exchanged from a URL
fragment over HTTPS and removed from the address bar. Do not share it beyond
the intended second player.

When a command is awaiting a response, the browser retains its immutable
command ID and payload. An interrupted response is retried with that same ID;
it is not submitted as a new action. Stale revisions require refreshing and
choosing again. While the backend is unavailable or a missing player owns a
decision, the client reports that state rather than playing offline or
automatically answering.

Room creation and joining also retain immutable request IDs across reload.
Creation resumes automatically. An unfinished join requires reopening the
original invitation to retry: the browser stores only its fingerprint, not
the invitation secret. Once joined, the guest cookie and durable seat mapping
restore access without the invitation.

Each seat receives only currently observable information. Public board
resources and normally public virtues remain visible. Hidden kingdoms, hidden
virtues, private decision/payment options, future deck order, and internal
snapshots are never placed in a shared client response. Infinite Knowledge
discloses information only to its entitled viewer. Legitimate play and timing
can still permit deductions; this is not a traffic-analysis secrecy guarantee.

## Durable command and reaction model

`OnlineCommand` carries a unique `commandId`, string `expectedVersion`, command
kind and arguments, and a `decisionId` for responses to a pending choice.

PostgreSQL serializes transitions with a match-row lock. After authorizing
membership, the server checks for an existing receipt before checking
staleness. Repeating the same authenticated command returns its recorded
receipt; reusing its ID with different contents is rejected. State, receipt,
and append-only history are committed atomically before acknowledgement.
Different workers cannot independently spend the same payment.

The saved match contains the current phase, active seat, setup/draw/power
continuation, pending decision owner and ID, server deck, action snapshot, and
independent reaction payment ledger. No Unity object references or C# callbacks
are serialized.

Retraction checks exact owned token IDs both now and in the pre-action
snapshot, including all independent reaction payments. Virtues acquired only
through the action being undone cannot fund its reversal. Use restores the
action snapshot and reapplies committed reaction costs exactly once. Already
revealed kingdoms and acquired knowledge remain known. An undo creates a new
version and an append-only event linking the original action; it never erases
the original event or rewinds the version counter.

## Code and validation

- `Busara/Assets/Busara/Online/Shared/Runtime`: shared value types, pure helpers,
  authoritative state machine and per-seat projections; no Unity dependencies.
- `online/src/Busara.Domain`: .NET Standard 2.1 project compiling those same
  source files.
- `online/src/Busara.Server`: ASP.NET Core and PostgreSQL protocol/persistence.
- `Busara/Assets/Busara/Online/Client`: the Unity online client.
- `online/tests/Busara.Domain.Tests`: rule, payment, serialization, privacy and
  normal-command progression tests.
- `online/tests/Busara.Server.Tests`: actual PostgreSQL, multi-worker and restart
  integration tests; see the [server guide](../online/db/README.md).
- `online/tests/Busara.Browser.Tests`: real Unity two-browser acceptance; its
  [README](../online/tests/Busara.Browser.Tests/README.md) provides the private
  local configuration and reproducible build/install/run commands.
- `Busara/Assets/Busara/script/Editor/BusaraOnlineParityTests.cs`: checks the
  shared definitions and geometry against the actual Unity assets, recipes,
  setup cards and deck.

Use the deliberately pinned Unity **6000.3.6f1** with its matching WebGL module,
.NET SDK **10.0.401**, and PostgreSQL **17.11**. Do not open a different checkout
through a pre-existing Unity MCP connection. Do not commit local database
files, credentials, certificates, SDK downloads, or Web build output.

The package manifest keeps its existing declarations. Unity 6000.3.6f1 resolves
its bundled 2D feature to **2.0.2**, test framework to **1.6.0**, and Multiplayer
Center to **1.0.1**; the lockfile records these and their required dependencies.
Retain that editor-resolved lockfile rather than restoring the older editor's
resolution after each import. The Poppins, Share Tech and TMP fallback font
texture serialization changes from version 3 to 4 are also editor migrations,
not visual redesigns; their texture data and dimensions are unchanged.

The [local server guide](../online/db/README.md) documents the exact connection,
certificate and origin environment variables. Persist its externally stored
32-byte `BUSARA_SECRET_KEY` across migrations, server restarts and workers: it
derives CSRF and retryable invitation secrets. A mismatched key blocks startup
rather than silently invalidating recovery. Test fixtures use independent,
uniquely named schemas and keys; do not reuse their configuration for a
long-lived local room.

From the repository root, with the SDK on the current process's PATH and local
configuration loaded privately:

```powershell
dotnet test .\online\tests\Busara.Domain.Tests\Busara.Domain.Tests.csproj
.\online\scripts\test-server.ps1 -ConnectionFile C:\private\connection.json
.\Busara\Assets\Busara\Online\Editor\Invoke-BusaraOnlineBuild.ps1
.\online\scripts\migrate-server.ps1
.\online\scripts\start-server.ps1
```

The build script uses the approved editor, checks that no Unity editor is
already running, and emits the actual Unity Web player into `online\web`.
Visit the exact loopback HTTPS origin you configured. Use separate browser
profiles/private contexts for the two guest identities. An ordinary second
tab in the same profile shares the guest cookie and is not a second seat.
The match outbox permits one active tab per guest/room; close that tab and
reload another one to transfer control safely.

For automated browser acceptance, build with `-Development` to expose
`window.busaraVisibleUi`: only visible control labels/bounds and connection
status, not hidden state or command functions. The browser tests click these
actual Unity controls and observe authorized API projections. Release builds
do not publish this diagnostic surface.

Domain tests alone do **not** prove PostgreSQL durability or a working browser
client. The milestone's acceptance additionally requires actual PostgreSQL
tests across workers/restarts and two independent browser contexts running
the built Unity Web player: setup, ordinary play, earned Retraction, reload
before responding, backend restart while pending, authenticated resume,
Use/Pass, duplicate/stale/wrong-seat attempts, and continued play to victory.

Local browser testing may use a file-only self-signed localhost certificate
with localhost/127.0.0.1 SANs and an explicitly isolated test-browser certificate
exception. This does not install system trust or disable production TLS
verification. Public deployment, public listeners, tunnels, paid services, and
production security/operations hardening are outside this local milestone.

## Verified local milestone

The following gates were executed on Windows on **2026-09-17**, using the
versions above:

| Gate | Result |
| --- | --- |
| Shared .NET domain tests | 15 passed |
| Combined Unity EditMode client, parity, kingdom-power and setup tests | 103 passed |
| Backend suite | 23 passed: 12 actual PostgreSQL/process integration tests and 11 local TLS guard cases |
| Browser transport/room-outbox Node tests | Passed |
| Development Unity Web build | Succeeded |
| Two independent Chromium contexts running that Web build | 2 passed, none skipped |

The actual browser flow created and joined a private room, configured each
seat separately, placed native setup resources, and earned Art and Security
through ordinary moves and forges. The off-turn Retraction decision retained
its owner, ID, prompt and exact payment options after both a browser reload
and a real backend-process restart. Use paid once, restored the action
snapshot, and survived an intentionally lost acknowledgement followed by an
identical retry. A separate flow verified Pass without payment or reveal.
Continued ordinary play reached the authored Mask of Light victory at
revision 153, after 127 additional commands.

Both browser tests require positive, nonzero WSS invalidation evidence for
each seat, not merely validation of any frames that happen to arrive. Captured
final network evidence and the visible **Live** connection state confirmed
this. Earlier HTTPS polling/reconnection was observed and recovered; no
global certificate bypass or production TLS change was added.

Browser acceptance uses a server-side deterministic deal only in the explicit
Testing environment. It does not seed resources or virtues or expose a state
mutation endpoint. The tested browser route covers Egolica/Mask; additional
Abundance/Infinite Knowledge browser scenarios, release-build acceptance,
other browsers, mobile layouts, and public hosting are not claimed by this
local milestone.
