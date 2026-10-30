// Square and hexagon variants of every layered theme, for players who choose square or hexagon pieces:
//
//   node tools/theme-lab/export-shapes.mjs                 # all layered themes
//   node tools/theme-lab/export-shapes.mjs eko-2090 ...    # chosen themes
//
// Per theme and shape it writes board_<shape>.png (holes drawn in that shape) and slot_state_<state>_<shape>.png,
// adds the theme's pieces in that shape to the shared layer library (Sprites/Pieces/<shape>/), and records the
// layer stacks and piece geometry in Themes/<id>/shapes.json. Existing theme sprites are left untouched.
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadPlaywright, stableGuid, folderMeta, textMeta, spriteMeta } from './export-sprites.mjs';

const require = createRequire(import.meta.url);
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const spritesRoot = path.join(repo, 'Busara', 'Assets', 'Busara', 'Sprites');
const outRoot = path.join(spritesRoot, 'Themes');
const engine = require(path.join(here, 'engine.js'));
const SHAPES = ['square', 'hex'];
const BOARD_PX = 768;
const assetPath = (abs) => path.relative(path.join(repo, 'Busara'), abs).split(path.sep).join('/');
const writeIfChanged = (file, text) => { if (!fs.existsSync(file) || fs.readFileSync(file, 'utf8') !== text) fs.writeFileSync(file, text); };
const importCap = (px) => { let p = 32; while (p < px) p *= 2; return p; };

const wanted = process.argv.slice(2);
const themes = engine.ALL_THEMES.filter((t) => engine.isLayered(t) && (!wanted.length || wanted.includes(t.id)));
const { chromium } = loadPlaywright();
const browser = await chromium.launch();
const page = await browser.newPage();
await page.setContent('<!doctype html><html><body></body></html>');
const rasterise = (svg, W, H) => page.evaluate(async ({ svg, W, H }) => {
  const img = new Image();
  img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg.replace(/ width="\d+" height="\d+"/, ` width="${W}" height="${H}"`));
  await img.decode();
  const c = document.createElement('canvas');
  c.width = W; c.height = H;
  const ctx = c.getContext('2d');
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(img, 0, 0, W, H);
  return c.toDataURL('image/png').split(',')[1];
}, { svg, W, H });
const ensureFolder = (dir) => {
  for (let d = dir; d.startsWith(spritesRoot + path.sep); d = path.dirname(d)) {
    fs.mkdirSync(d, { recursive: true });
    if (!fs.existsSync(d + '.meta')) fs.writeFileSync(d + '.meta', folderMeta(stableGuid(assetPath(d))));
  }
};
const writeSprite = async (file, svg, W, H, readable = false) => {
  ensureFolder(path.dirname(file));
  fs.writeFileSync(file, Buffer.from(await rasterise(svg, W, H), 'base64'));
  writeIfChanged(file + '.meta', spriteMeta(stableGuid(assetPath(file)), importCap(Math.max(W, H)), undefined, readable));
  return stableGuid(assetPath(file));
};

const writtenLayers = new Set();
for (const t of themes) {
  const dir = path.join(outRoot, t.id);
  const shapes = {};
  for (const shape of SHAPES) {
    const board = await writeSprite(path.join(dir, `board_${shape}.png`), engine.boardSVG(t, { holeShape: shape }), BOARD_PX, BOARD_PX);
    const slots = {};
    for (const state of Object.keys(engine.SLOT_STATES))
      slots[state] = await writeSprite(path.join(dir, `slot_state_${state}_${shape}.png`),
        engine.slotStateSVG(t, state, { holeShape: shape }), 256, 256);
    const lib = engine.layerLibrary(t.id, { shape });
    for (const e of lib.entries) {
      if (writtenLayers.has(e.path)) continue;
      writtenLayers.add(e.path);
      await writeSprite(path.join(spritesRoot, e.path), e.svg(), e.w, e.h, true);
    }
    shapes[shape] = {
      board, slots,
      pieceAspect: Number(engine.pieceAspect(t, { shape }).toFixed(4)),
      piecePivotY: Number(engine.piecePivotY(t, { shape }).toFixed(4)),
      layers: Object.fromEntries(Object.entries(lib.stacks).map(([k, stack]) => [k, stack.map((ly) => ({
        name: ly.name, file: ly.path, tint: ly.tint, guid: stableGuid(assetPath(path.join(spritesRoot, ly.path))),
      }))])),
    };
  }
  const file = path.join(dir, 'shapes.json');
  writeIfChanged(file, JSON.stringify({ id: t.id, shapes }, null, 2) + '\n');
  writeIfChanged(file + '.meta', textMeta(stableGuid(assetPath(file))));
  console.log(`${t.id}: square and hexagon variants`);
}
await browser.close();
