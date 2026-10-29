# Busara theme lab

Art direction and sprite generation for Busara's board and piece customisations.

| File | What it does |
| --- | --- |
| `engine.js` | Theme data (15 directions: 7 past kingdoms, 4 future cities, 4 fusions, plus Busara Classic), piece materials, 9 hole profiles, the SVG generators for boards, pieces, holes and every play state, and the Panther Court interface kit. Runs in the browser and in Node. |
| `index.html` | The art lab: a playable board, direction gallery, material library, hole and state matrix, themed interface kit and a workshop for mixing eras. Open it directly in a browser. |
| `export-sprites.mjs` | Rasterises each theme (plus Busara Classic) with Chromium into `Busara/Assets/Busara/Sprites/Themes/<theme-id>/`, with Unity `.meta` files whose GUIDs are derived from the path, so re-exporting never breaks references. |

## Regenerate sprites

```sh
node tools/theme-lab/export-sprites.mjs              # every theme
node tools/theme-lab/export-sprites.mjs eko-2090     # one or more theme ids
```

Needs Node 20+ and Playwright with Chromium (`npm i -D playwright && npx playwright install chromium`
if neither is installed globally).

## Sprite set per theme

| File | Pixels | Unity use |
| --- | --- | --- |
| `board.png` | 1024 | `Board` background, replaces `Sprites/New/UI/Board.png` |
| `board_turn_glow.png` | 1024 | `Board.Highlight` image (already pulses in `FadeHighlight`) |
| `piece_water/earth/fire/air.png` | 256 wide | Resource prefab icons (taller for tall shapes) |
| `piece_hidden.png` | 256 wide | Back face for hidden information |
| `piece_state_selected/threatened/shielded.png` | 256 | Overlays on a resource: selection, disaster or weapon removal, Celestial Dome |
| `slot_state_hover/legal/blocked/selected/target.png` | 256 | Overlays on a slot; `legal` fits the existing `Slot.highlight` |
| `manifest.json` | n/a | Colours, materials, geometry and file list for a theme `ScriptableObject` |

Board geometry: 1024 design units, a 96-unit frame band, 4x4 slots on a 208-unit pitch with
radius 82. Piece sprites hold a disc of radius 214/512, so a piece drawn at 75% of a slot's
width sits inside the hole. The project's default 2D import mode brings PNGs in as sprites.

## In the Unity project

- **Assets**: one `BoardTheme` per folder in `Assets/Busara/ScriptableObjects/Themes/`, listed in
  `ThemeCatalog.asset` (Busara Classic first). Seat defaults: Akan Goldweight, N'evulandis Data Court,
  Swahili Bao, Volta Resonance.
- **GameScene**: `Managers/ThemeManager` holds the catalog. The four boards are saved with their seat
  themes applied, so the editor shows the real look; at runtime `ThemeManager` re-applies them and
  re-skins every piece placed, moved or traded onto a board (`Board.PlaceResource`, `Slot.OccupySlot`,
  `Resource.Start`).
- **Player setup**: each player row has a board theme picker (previous / next, with a preview) and
  a piece preview in the player's colour (tap it to change colour). The choices travel on
  `PlayerSetupEntry.Theme` and `ColourIndex` and are applied in `PlayerManager.TryConfigurePlayers`.
- **Geometry**: every theme, Classic included, uses the same layout. `ThemeManager` sizes each board's
  slot grid to `board width x 208/1024` with no spacing, hides the slot images (they stay as click
  targets) and stretches slot highlights and the turn glow to their parents.
- **Editor menu**: *Tools > Busara > Board Themes > Import Themes From Sprites* rebuilds the
  `BoardTheme` assets and catalog after an export; *Apply Seat Themes In Open Scene* re-applies the
  seat defaults to the open scene with undo. `BoardThemeTests` (EditMode) checks the catalog and layout.

## Project sprite layout

All art lives under `Assets/Busara/Sprites/`, named in `snake_case` by role. Files were moved with
their `.meta`, so every scene and prefab reference kept its GUID. Sources above 2048 px were
downscaled to 2048 (Unity's import cap) with sprite-sheet rects scaled to match.

| Folder | Contents |
| --- | --- |
| `Themes/<theme-id>/` | Board, turn glow, pieces and state overlays for each theme, including `classic` (the original look, converted to the standard format) |
| `Elements/` | The four resource icons used across the interface (`element_fire.png`, ...) |
| `Virtues/` | Virtue icons (`virtue_art.png`, ...) |
| `UI/Backgrounds`, `UI/Panels`, `UI/Frames`, `UI/Buttons`, `UI/Icons`, `UI/Effects`, `UI/Shapes` | Interface art, grouped by role |
| `Branding/` | Busara and publisher logos |
| `Archive/` | Superseded designs, duplicates, screenshots and reference photos (kept, unused) |

## Piece shapes

Pieces are drawn in projection: the footprint sits in the hole and the top face rises up the
board by the shape's height, with a shaded side wall between. Shapes: cabochon, short cylinder
(metal rim + inset), tall banded cylinder, hexagon, square, river pebble, tumbled stone, carved
gem, bead (sphere) and crystal vial. Glyph finishes: carved, gold leaf, hand-painted, glowing.
Each theme picks a default (`pieceShape`, optional `pieceRim`); the workshop can apply any shape.

Piece sprites are 256 wide and `256 x pieceAspect` tall. The manifest and `BoardTheme` carry
`pieceAspect` and `piecePivotY` (base centre height from the bottom); `ThemeManager` pins that
point to the slot centre so tall pieces rise over the row behind, and lower rows draw in front.

## Piece layers

Every piece except the cabochon and Classic's raster icons is also exported as a stack of shared
layer masks under `Sprites/Pieces/<shape>/`. From the bottom up:

| Layer | Drawn as | Tint |
| --- | --- | --- |
| `shadow` | contact shadow | none |
| `trim` | metal rim, band or vial caps | rim metal, or the seat colour |
| `paint` | white or grey material mask | piece colour |
| `motif` | dots, ring, charge, veins or stitch | inlay or glow colour, or the seat colour |
| `detail` | black and white lighting, shared by every material | none |
| `relief` | carved or gilded relief | none |
| `glyph` | element glyph (or the back mark) | ink, gold or glow |
| `glow` | emissive core | none |

Masks with the same key are the same image, so the 75 stacks (15 themes x 4 elements + the
hidden back) share 222 files. Each theme's `manifest.json` lists its stacks under `layers` (file,
GUID, tint). The single `piece_*.png` sprites are those stacks pre-composited and stay as the
fallback.

A layer is drawn as `rgb x tint`, then alpha-composited over the layers below. The exporter
re-composites every exported stack and compares it with the direct render. The result is written to
`layer-verification.json`: currently a mean channel delta of 0.22 / 255, at most 50, and only on
antialiased edges. The export fails if a stack's mean passes 0.5.

Unity's linear colour space would blend separate UI Images in linear light, which washes out the
lighting (a mean drift of about 6 / 255). So `PieceCompositor` blends the stacks on the CPU in
sRGB, using the same maths as the check, and caches one sprite per stack and tint set. The layer
textures import with Read/Write on.

Player colours replace the `trim` and `motif` tints, so players on the same theme stay distinct.
`ThemeCatalog.playerColours` holds eight: gold, silver, copper, jade, ruby, lapis, amethyst and
ivory. Seats 1 to 4 start on the first four. In player setup, each row shows the player's piece
in their theme and colour; tapping the piece cycles the colour. The choice travels on
`PlayerSetupEntry.ColourIndex` to `ThemeManager.SetPlayerColour`, which recolours that board's
pieces and the owner's name on the board. Turn colours off with `usePlayerColours`. The art lab's *Piece layers* section shows each mask, the
composites per seat and the live delta.

## Menus

The home screen (`MainMenuScreen` on mainMenu's Canvas) and the player setup screen
(`PlayerSetupUI`) are built in code from `ScriptableObjects/UI/BusaraUiStyle.asset`. That asset
holds the Poppins fonts, the light wordmark (`Branding/busara_logo_light.png`), the backdrop and
the colours. `UiKit` generates its rounded panels at runtime, so no extra sprites are needed.

- **Home**: wordmark, tagline, then Play and Quit. These are the scene's own buttons, restyled, so
  their click events are unchanged. On the right, a board showcase cycles through every theme with
  pieces in each seat colour.
  With a `UiSkin` assigned (mainMenu uses `PantherCourtUiSkin`), the screen also takes the skin's look:
  neon friezes across the top and bottom, the neon Play button, the gold outline Quit button and a steel
  bezel around the showcase.
- **Setup**: one card per player. Each card has the name, a board preview with the player's piece
  (arrows change the theme, and the piece or a swatch changes the colour) and the kingdom choice.
  Below the choice are the kingdom's power, its cost and the virtues it needs to win.

## In-game UI skin

`GameUiSkinner` on GameScene's Canvas restyles the side panels, popups and action buttons from a
`UiSkin` asset. `ScriptableObjects/UI/PantherCourtUiSkin.asset` uses the Panther Court kit:

| Element | Kit sprite |
| --- | --- |
| Side panels | leather |
| Kingdom / Virtue tabs, kingdom name, headers | gold label tab |
| Description, goals and virtue lists | dark inset |
| Virtue goal entries | wood kingdom card |
| Forge, Draw, Trade, Weapon | steel ring buttons |
| Use Power | the orange power knob |
| Popups | neon panel |

Popup buttons use the neon button and More uses the outline button. Text is set in Poppins. Objects
are found by name, so the layout and click events are untouched. The skin applies when the scene
starts; to see it in the editor too, run *Tools > Busara > UI Skin > Apply Skin In Open Scene* and
save the scene. To make another skin, create a *Busara > UI Skin* asset from any theme's `ui/`
sprites and assign it to the skinner.

## Panther Court interface kit

Panther Court (built from the Afrofuturist superhero-kingdom mockups) also exports a nine-slice
interface kit to `Themes/panther-court/ui/`: device frame, leather / wood / neon panels, neon and
carved triangle friezes, Play / Next / pill / outline buttons, ring buttons, power knob, avatar
tiles, kingdom card, label tab, dark inset, virtue counter chip, add icon, kinetic dots and
diamond ornament. Each `.meta` carries its sprite border, so set the Image type to **Sliced**.
The art lab's Panther Court section rebuilds the four reference screens from these sprites.
Names avoid Marvel trademarks (no "Wakanda", "Vibranium" or "Kimoyo") so the theme can ship.

## Adding a theme

Copy a theme object in `THEMES` (or paste the spec from the lab's workshop), keep the readability
contract (Water blue, Fire red, Air amber, Earth dark, plus the glyph silhouettes), re-run the
exporter, then run *Import Themes From Sprites* in Unity.
