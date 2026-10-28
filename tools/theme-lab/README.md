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
| `piece_water/earth/fire/air.png` | 256 | Resource prefab icons |
| `piece_hidden.png` | 256 | Back face for hidden information |
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
- **Player setup**: each player row has a board theme picker (previous / next, with a preview).
  The choice travels on `PlayerSetupEntry.Theme` and is applied in `PlayerManager.TryConfigurePlayers`.
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
