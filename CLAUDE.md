# Busara

Unity 6 (6000.0) board game. The Unity project is in `Busara/`. Game code is in `Busara/Assets/Busara/script`.
The art pipeline is in `tools/theme-lab`, and the project-local Unity skills are in `.claude/skills`.

## UI rules

- **Author UI in scenes and prefabs.** Don't skin UI at runtime, and don't create UI components from
  code (`new GameObject`, `AddComponent<Image/Button/TMP...>`). Scripts may fill in authored objects:
  set text, swap sprites, show or hide them, and copy an authored inactive template such as the
  power-choice button.
- The only runtime sprite swapping is gameplay content. `ThemeManager` applies the board theme each
  player picks, `PieceCompositor` builds seat-coloured pieces, and `ThemeShowcase` runs the home-screen
  slideshow.
- Use fully qualified uGUI types (`UnityEngine.UI.Image`, `UnityEngine.UI.Button`). The game defines its
  own global `Selectable` class.
- GameScene's CanvasScaler uses **1000** reference pixels per unit. Its nine-slice images therefore need
  `pixelsPerUnitMultiplier` set 10x higher than on a 100-PPU canvas.
- Set Raycast Target off on graphics nothing clicks. Turn TMP AutoSize off on text that changes at
  runtime (counters, names).

## Art pipeline

`node tools/theme-lab/export-sprites.mjs [theme ids]` regenerates the theme sprites, the piece layer
library and the action icons (`Sprites/UI/Icons/Actions`). See `tools/theme-lab/README.md`.
