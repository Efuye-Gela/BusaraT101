# Private online multiplayer

Online play is a private two-player Busara variant whose rules run on the
server. **New matches use `busara-online-v3`, which offers all fifteen kingdom
powers.** Existing `busara-online-mvp-v1` and `busara-online-mvp-v2` matches
keep their original three-kingdom rules; saved matches are never silently
upgraded. The offline game remains separate. Disasters, bots, public
matchmaking and spectators are still offline-only or unavailable.

**Default hosting: Unity Gaming Services.** Follow
[UGS setup and a simple two-browser test](ugs-setup.md) to deploy Cloud Code,
initialize private Cloud Save and build the player. The ASP.NET/PostgreSQL
backend remains an explicit legacy option; its dated verification below does
not establish UGS cloud or browser acceptance.

## Supported play

**v3 (new matches):** two invited humans each receive a different random
kingdom from all fifteen authored kingdoms. Every kingdom power can be used
online, following the offline rules in the
[kingdom powers guide](kingdom-powers-guide.md), with these online specifics:

- Using a power is the turn's action.
- Reactions are durable decisions owned by the reacting seat:
  King's Necklace can cancel any committed power; Celestial Dome protects
  against weapons only; Retraction is offered after the other player's
  completed action, and its payment stays spent.
- Time grants its extra turn through a durable turn queue. Manipulation lets
  its owner act at the start of the other player's turn, on that player's
  board and paying from that player's virtues; trading is not offered
  during a manipulated turn.
- If the target's virtues are hidden, Imagination offers all six virtue types.
  This is an online deviation: the server does not reveal hidden virtues
  through the choice list.
- Disasters are not part of the online deck.

The sections below describe the three-kingdom v1/v2 rules, which are
still the rules for those saved matches. v3 keeps the same board, setup,
deck, forging, trading and weapon rules.

**v1/v2 (saved matches):** two invited humans receive different kingdoms randomly from **Egolica
(Abundance)**, **Mask of Light (Retraction)**, and **N'evulandis (Infinite
Knowledge)**. Neither player chooses the other's kingdom; lobby readiness does
not disclose the deal.

The game uses the first two authored boards, their original eight-column global
slot coordinates, and their five-resource setup cards. Setup placement is on
your own empty spaces, not orthogonally adjacent to another resource on your
board. The deck contains the scene's 24 resource cards, six of each resource
type, without its four disaster cards.

Ordinary actions include drawing and placing a resource and moving an owned
resource to an adjacent empty participating space. Both rulesets support
two-resource forging: start with your resource, choose an adjacent resource of
a different type, consume both, and award the recipe's virtue to each distinct
board owner represented. Moves and forges can cross the two boards' boundary.

New v2 matches also support:

- **Chain forging:** select two or more distinct resources in order, starting
  on your board. Consecutive resources must be adjacent and different types.
  Each successive pair produces its authored virtue for **every board owner
  encountered so far**, matching offline forging. All selected resources are
  consumed once. The complete chain is validated before any rewards apply.
- **Trading:** offer one owned resource for a different resource type. The
  opponent explicitly accepts or rejects; acceptance requires owning that type.
  After acceptance, the offerer chooses the exact matching opponent resource
  and confirms the swap. The resources exchange slots; neither player gains a
  free resource. The offerer can cancel at that selection step or dismiss a
  rejection to return to the same ordinary turn without consuming it.
- **Weapons:** select exactly three connected resources of the same type,
  starting with your own. They may span the boards. All three are consumed,
  then the opponent chooses one of their remaining resources to discard. If
  none remain, there is no discard choice. This is one action, including its
  defender discard, for Retraction.

Trade responses, accepted-offer selections and weapon discards are durable
owner-specific decisions. Browser reload and backend restart do not choose
for either player. Only the decision owner can respond using its current ID
and revision. Chain/weapon selection is a local draft until confirmed; reload
can discard that unsubmitted draft, but never a submitted command's outbox.

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

**Unavailable online:** disasters, bots, tournaments, more than two players,
spectators and public matchmaking. Saved v1/v2 matches are limited to their
three kingdoms; v1 also excludes trades, weapons and longer forge chains. Use
offline play for disasters and bots.

## Coordinated release

Publish the updated authoritative module and rebuilt Unity Web client together.
The new client understands both rulesets; a previous client may reject a v2
projection and needs a refresh after the new Web build is published. Keeping an
old match's v1 rules is not a binary rollback guarantee: the new writer can
persist added decision fields even in v1 rooms. Do not roll back to an older
writer without a compatible migration of states and durable receipts.

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

**UGS:** Unity Authentication session tokens identify the player; Cloud Code
checks that player against persisted membership and fixed Busara guest expiry.
Refresh tokens live in browser local storage, not HttpOnly cookies. A
`command` response carries the acting player's own updated projection
alongside its receipt, so submitting your own move never costs a second
round trip. The client separately polls authorized Cloud Code projections
over HTTPS to discover the *other* player's moves: every 2 seconds while
visibly waiting or in the lobby, normally 10 seconds on your own turn, and
at least 30 seconds while hidden or finished. Failed reads back off rather
than continuously retrying. The interval excludes backend/network latency.
Legacy WebSocket delivery and its fixed polling fallback remain separate. See the
[UGS privacy and storage limits](ugs-setup.md#persistence-privacy-and-operational-limits).

**Legacy backend:** guest credentials are 256-bit opaque tokens in Secure, HttpOnly, SameSite
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
the invitation secret. Once joined, the backend's saved identity and durable seat mapping
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

UGS saves state, compact durable receipts and append-only events in one
private Cloud Save document per room using a write-lock compare-and-swap.
Room publication is recorded in the creating player's own guest document.
Preprovisioned registration shards CAS-publish immutable actor-to-document
mappings and fixed expiry. Fresh random guest candidates are written before
publication; losing registrations use the winning mapping and never overwrite
its ledger. Only registration contends for a shard lock. Ordinary commands read
registration/expiry alongside the room, without loading a guest's create/join
ledger. PostgreSQL's legacy backend serializes
transitions with a match-row lock. After authorizing membership, the server
checks for an existing receipt before checking staleness. Repeating the same
authenticated command returns its recorded receipt; reusing its ID with
different contents is rejected. State, receipt, and append-only history are
committed atomically before acknowledgement. Different workers cannot
independently spend the same payment.

UGS keeps `command` replies as bare receipts for already-open older clients.
New clients explicitly request `commandWithView`, receiving
`{"receipt": {...}, "view": {...}}`. Both operations use the same command identity
and exact body fingerprint. Retries return the original receipt alongside the
current authorized actor view, not a stored copy of an old projection. The
legacy backend remains bare-receipt only. An embedded view updates the polling
activity without falsely completing an outstanding view request.

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
- `online/src/Busara.Ugs`: default Cloud Code adapter using the same domain.
- `online/tests/Busara.Ugs.Tests`: simulated CAS/worker tests, not live UGS evidence.
- `online/scripts/smoke-ugs.cjs`: explicit real UGS API smoke; see [setup](ugs-setup.md).
- `Busara/Assets/Busara/Online/Client`: the Unity online client. Its scene
  `Scenes/OnlineMVP.unity` is authored by `BusaraOnlineSceneBuilder`
  (`BusaraOnlineBuild.GenerateScene`) using EgComponents **v0.1.4** UI pages
  (`Entry`, `Lobby`, `Match`), saved button/label prefabs under `Prefabs/` and
  baked shape sprites under `Art/`. `OnlineTheme` holds the shared palette and
  layout, taken from the authored board and panel art. Regenerate the scene
  after builder changes; do not hand-edit it.
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

The following setup/commands are for the **legacy backend**. UGS deployment,
auth, polling and checks are in the [UGS guide](ugs-setup.md).

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
.\Busara\Assets\Busara\Online\Editor\Invoke-BusaraOnlineBuild.ps1 -Backend legacy
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

## Verified local milestone (legacy backend)

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
