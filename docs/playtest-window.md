# Playtest Runner

Open **Tools > Busara > Playtest Runner** in Unity. The window has **Game Setup**
and **Gameplay** tabs. Save your work before switching scenes; **Open GameScene**
offers Unity's normal save prompt.

## Game Setup tab

Open **GameScene**, choose **2-4 players** (limited by the scene's configured
boards), and enter each player's name and kingdom directly in this tab. After
the kingdom, choose **Player controlled** or **Bot controlled**. New players
default to player-controlled. Names and kingdoms must be unique. These choices,
including control mode, survive entering Play Mode.
Press **Play**, wait for initialization, then **Start Game** in this window;
there is no need to fill in the in-game setup form. Both entry points use the
same validation, canvas unlocking and turn-start lifecycle.

After starting, place resources manually in-game or press **Finish Resource
Setup** here. The shortcut uses **each player's actual setup card**, including
repeated resource types, rather than giving everyone one of each type. It
preserves legal partial placements and fills only their missing resources.
Every new piece uses an empty space with **no orthogonally adjacent resource
on that player's board**. Diagonal neighbors and resources belonging to another
board do not prevent placement.

All boards are planned before changes are applied. Extra resources, adjacent
existing placements, insufficient legal spaces or insufficient stock produce
an error instead of falsely completing setup. Fix that fixture and retry.
The stock override in Gameplay does not bypass these setup rules.
Successful completion advances the window to **Gameplay**. To change player
count after starting, stop and re-enter Play Mode; gameplay kingdom reassignment
remains available separately.

## Fast Test Start

With **GameScene** open alone, choose valid player names and kingdoms in **Game
Setup**, then press **Fast Test Start**. From Edit Mode it enters Play Mode,
waits for the actual GameScene managers and setup UI, starts those players through
the existing setup backend, finishes their actual setup cards using legal
non-adjacent placement, and switches to **Gameplay**. It also works when already
playing and still awaiting player setup. It never restarts an active game.

Names, control modes and kingdom asset GUIDs are saved in Editor SessionState before entering
Play Mode, so the pending request survives a domain reload. Progress and errors
remain visible in the window. Initialization has a 90-second limit; stopping Play
Mode or **Cancel Fast Test Start** cancels the pending request. Paused Play Mode
must be resumed before starting. A timeout or failure leaves any already-applied
runtime changes intact for inspection; it does not claim completion or retry
mutations automatically.

This shortcut does not open, save, or replace scenes. Use **Open GameScene** and
its normal save prompt first; additive scene setups and a conflicting Play Mode
start-scene override are rejected. Configuration failures, missing assets, and
illegal resource setups remain visible rather than falling back to defaults.

`BusaraFastTestStartTests` covers request serialization, asset GUID restoration,
validation and the deadline without entering Play Mode. The
`BusaraPlaytestToolsTests.FastTestStartEntersPlayModeAndCompletesSetup` and
`FastTestStartWorksWhileAlreadyAwaitingSetupInPlayMode` scene cases exercise both
startup routes, completed non-adjacent placement and rejection of an active game.
For additional manual scenarios, select 2/3/4 players and test **Fast Test Start** from Edit Mode
(with domain reload enabled) and from Play Mode's waiting setup screen. Verify
names/kingdoms, exact setup-card resource counts, non-adjacency and the Gameplay
tab. Also verify cancellation on Stop, a missing manager timing out, and refusal
to replace an already-started game.

## Gameplay tab: build a scenario

- **Bot Decisions** opens the initial heuristic bot's candidate paths, score
  breakdowns, chosen action and execution history. Bot-controlled players act
  automatically once resource setup is complete, even with the window closed.
  Each normal action completes its own turn; a draw is followed by a placement
  step before completion. Pause a bot here to inspect or manually step it.
  Human-controlled players are never automatically advanced. See the
  [heuristic bot guide](heuristic-bot.md) for supported actions and limitations.
- Choose a player in the window; this does not change whose turn it is.
- Add/remove resources by type and quantity, or click a numbered slot and add one
  resource there/remove its piece. Slots use the actual board indices; the button
  grid is **not** the board's adjacency layout. Bulk additions fill the first
  available spaces. Removals are immediate and clear selections. These deliberate
  fixture edits can create adjacent pieces; **Finish Resource Setup** does not.
- Add/remove virtues and inspect owned totals and remaining stock. Normal limits
  are **20 resources per type** and **12 virtues per type**, shared by players.
  Additions are best effort, limited by stock and board capacity. **Ignore stock
  limits** deliberately creates an out-of-rules fixture; it never expands a board.
- Change to a catalog kingdom, or toggle kingdom reveal and virtue hiding.
  Kingdom changes reset reveal. Duplicate kingdoms require the explicit debug
  toggle. This window intentionally exposes hidden information.
- Pick an exact live resource/disaster card and queue it next. The preview lets
  you verify the choice. Queueing does **not** draw: it moves that reference ahead
  of the other cards without changing their order/count. A scene card missing
  from the deck (for example, in an altered test fixture) is inserted once and
  increases `CardCount`. The deck rotates cards and uses `CardCount` as a draw
  counter; it is not a count of remaining scene card objects.
- **Draw Next Card** uses the game's draw action: place a drawn resource normally,
  or resolve its disaster in the game. **End Turn** uses normal turn completion,
  including any reaction prompts. Use the victory check/player popup for
  inspection, and **Select Player/Board** to inspect runtime objects.
- **Pause/Resume** and **Step** use the Editor's Play Mode controls.

## Quick scenarios

- **Hard Winter:** finish resource setup, add virtues to the players you want
  affected, queue the Hard Winter disaster, then draw it. Use the in-game X
  buttons to choose each player's loss. Leave one player without virtues to
  exercise skipping an empty inventory.
- **Resource placement:** queue the desired resource card, draw it, and place
  the piece on the real board. Use exact-slot additions beforehand to prepare
  crowded boards or particular forging patterns.
- **Kingdom powers:** assign the desired kingdoms, give their owners the
  required virtues, and arrange resources/visibility before activating powers
  with **Use Power** beside the other right-sidebar action buttons. It activates
  the current turn owner's kingdom through normal payment and confirmation;
  reaction powers are still offered automatically at their legal timing.
  Follow the timing and worked examples in the
  [kingdom powers guide](kingdom-powers-guide.md); these fixture buttons do not
  bypass a power's activation rules.

## Limits and safety

Fixture edits require configured, participating players. Finish any pending
power/reaction/modal, disaster/weapon special turn, drawn resource placement, or
partially completed action first. Resource/virtue fixtures can be edited during
resource setup; ordinary draw/turn controls require setup to be finished.
Blocked actions show a reason; operation errors appear in the window.

Changes are runtime-only, with no fixture saves to scenes or assets and no Undo.
Stop Play Mode to discard them. Inventory/visibility fixture edits refresh player
displays and establish a fresh Retraction baseline; they do not reset turn
ownership, control, or queued Time turns. This is a scenario-building tool, not
a substitute for exercising powers and actions through their normal UI.

## Focused regression scenario

The `BusaraPlaytestToolsTests` EditMode UnityTests open the real GameScene and
enter Play Mode. They configure two-, three- and four-player games through the
Editor backend; exercise validation and legal resource setup; preserve partial
placements; reject adjacent setup fixtures; and cover inventory edits, guards,
stock limits, both window tabs and queued Hard Winter through normal gameplay.
They refuse dirty scenes and restore the prior scene setup afterward.
Run it from the existing Unity Test Runner/Editor test bridge; no test result is
implied by this documentation.
