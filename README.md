# Busara

Busara is a Unity board game about arranging resources, forging virtues and
using kingdom powers to meet a kingdom's victory requirements.

| Mode | Current scope |
| --- | --- |
| Offline Unity | Local 2-4 player setup, all 15 kingdom powers, optional heuristic bots and Editor playtest tools |
| Private online MVP | Two invited humans in the real Unity Web player, with authoritative Unity Cloud Code and private Cloud Save |

Online is an explicit reduced variant, not a production multiplayer service.
Players receive distinct random kingdoms from Egolica/Abundance, Mask of
Light/Retraction and N'evulandis/Infinite Knowledge. It supports native setup,
resource drawing/placement, adjacent movement, ordered forge chains, consensual
one-resource trades, three-resource weapons, reactions and kingdom-goal victory.
New matches use `busara-online-mvp-v2`; existing v1 matches keep their original
pair-only forging and do not gain trades or weapons. It excludes the other
12 powers, disasters, bots, spectators and public matchmaking. Offline play
remains separate.

## Open and play offline

1. Install **Unity 6000.3.6f1** through Unity Hub. Add this checkout's **`Busara`**
   directory as the project; the repository root is not a Unity project.
2. Open `Assets/Scenes/mainMenu.unity` and enter Play Mode. Use the existing
   offline play entry to reach `GameScene` and configure players.
3. For focused developer playtests, open `Assets/Scenes/GameScene.unity` and
   **Tools > Busara > Playtest Runner**. Follow the
   [playtest guide](docs/playtest-window.md) for setup and Fast Test Start.

Offline play does not require PostgreSQL or the .NET server. Bots have their
own documented [capabilities and limits](docs/heuristic-bot.md).

## Run multiplayer with Unity Gaming Services

For an end-to-end explanation to reuse in another game, read
**[Multiplayer architecture and hosting](docs/multiplayer-architecture-and-hosting.md)**.

Start with **[UGS setup and a simple two-player test](docs/ugs-setup.md)**.
UGS is the default online backend. You deploy the `BusaraUgs` C# module to
Unity Cloud Code; private Cloud Save holds matches and pending decisions.
There is no VPS, PostgreSQL installation, dedicated Unity server, Lobby or
Relay dependency for this path. Free-tier allowances are usage limits, not
a guarantee of unlimited free hosting.

Install Unity **6000.3.6f1** and its matching WebGL module, .NET SDK
**10.0.401**, Node.js and the UGS CLI. The module targets **.NET 8** for Cloud
Code compatibility; it references the existing .NET Standard shared rules.
The guide covers creating a development UGS environment, access control,
one-time private storage initialization and deployment. Nothing deploys
automatically or contains a service-account credential.

After completing that setup, from the repository root:

```powershell
.\Busara\Assets\Busara\Online\Editor\Invoke-BusaraOnlineBuild.ps1 `
  -UnityPath 'C:\path\to\6000.3.6f1\Editor\Unity.exe' `
  -ProjectId '<your-Unity-project-UUID>' -EnvironmentName 'development'
```

For normal play across different PCs/networks, follow
[Vercel HTTPS hosting](docs/vercel-hosting.md): publish the Web build once,
then both players open the website with no local server or Node installation.
For development-only local testing, the UGS guide also includes a loopback-only
static server; it does **not** run game logic. Open two
separate browser profiles, create a guest/room in the first, and accept its
private invitation in the second. Ready both seats, then start. Two ordinary
tabs share an identity and are not two players.

UGS uses persistent anonymous player sessions and a fixed 30-day Busara guest
expiry. Invites are single-use and expire after 24 hours. Clearing site storage
can lose your seat; there is no account recovery or automatic disconnect
forfeit. See the [online rules](docs/online-multiplayer.md), including the
[normal-play Retraction recipe](docs/online-multiplayer.md#earning-a-retraction-payment-through-play).

### Existing local backend

The ASP.NET Core/PostgreSQL implementation remains available for regression
testing with **`-Backend legacy`** on the Web build command and the
[legacy server guide](online/db/README.md). It is not started as a fallback.

For opt-in, local browser-console timing, see [move latency diagnostics](docs/move-latency.md).
`busaraLatency.enable()` starts measurement; `busaraLatency.report()` separates
provider round-trip, verified receipt, applied view and frame-boundary timings.
Existing PostgreSQL matches/cookies are not imported into UGS; each backend
has separate identities and durable storage.

## Documentation

| Guide | Purpose |
| --- | --- |
| [Contributing](CONTRIBUTING.md) | Safe editing, local checks and validation recipes |
| [Agent instructions](AGENTS.md) | Repository-wide coding-agent map and invariants |
| [Kingdom powers](docs/kingdom-powers-guide.md) | Offline architecture and all 15 powers |
| [Playtest Runner](docs/playtest-window.md) | Editor setup, scenario tools and fast startup |
| [Heuristic bots](docs/heuristic-bot.md) | Offline bot behavior, scoring and limits |
| [Online multiplayer](docs/online-multiplayer.md) | Supported rules, privacy, durability and dated verification evidence |
| [Multiplayer architecture and hosting](docs/multiplayer-architecture-and-hosting.md) | Reusable architecture, provider boundaries, UGS setup and static website deployment |
| [UGS setup and test](docs/ugs-setup.md) | Cloud Code deployment, private Cloud Save, API smoke and two-browser play |
| [Reusable UGS playbook](docs/ugs-multiplayer-playbook.md) | Setup-session lessons, copyable commands, troubleshooting and a checklist for future games |
| [Vercel HTTPS hosting](docs/vercel-hosting.md) | One hosted Unity website for players on different PCs, with UGS as the backend |
| [Two-PC testing](docs/two-pc-testing.md) | Portable Windows Web bundle for private UGS play across different networks |
| [Legacy server/database setup](online/db/README.md) | Environment variables, migrations and PostgreSQL integration tests |
| [Legacy two-browser acceptance](online/tests/Busara.Browser.Tests/README.md) | PostgreSQL/WSS Unity Web tests and historical evidence |
| [Unity MCP](tools/unity-mcp/README.md) | Optional local Editor automation and its safety boundaries |

The previously verified **legacy-backend** milestone includes real two-browser play, normally earned
Retraction payment, refresh/backend-restart recovery, Use/Pass, identical retries
and continued victory. This is not a claim of public-hosting readiness,
all-browser/mobile support or full offline feature parity. Test-only TLS
exceptions are restricted to isolated local clients, never global trust changes.
That historical evidence is not UGS deployment or browser/CORS evidence.
