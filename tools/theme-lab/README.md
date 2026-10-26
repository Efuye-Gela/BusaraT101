# Busara theme lab

Art direction and sprite generation for Busara's board and piece customisations.

| File | What it does |
| --- | --- |
| `engine.js` | Theme data (14 directions: 7 past kingdoms, 4 future cities, 3 fusions), 41 piece materials, 8 hole profiles, and the SVG generators for boards, pieces, holes and every play state. Runs in the browser and in Node. |
| `index.html` | The art lab: a playable board, direction gallery, material library, hole and state matrix, themed interface kit and a workshop for mixing eras. Open it directly in a browser. |
| `export-sprites.mjs` | Rasterises each theme with Chromium into `Busara/Assets/Busara/Sprites/Themes/<theme-id>/`. |

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

## Adding a theme

Copy a theme object in `THEMES` (or paste the spec from the lab's workshop), keep the readability
contract (Water blue, Fire red, Air amber, Earth dark, plus the glyph silhouettes), then re-run
the exporter.
