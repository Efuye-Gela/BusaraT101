// Renders every theme in engine.js to transparent PNG sprites for Unity.
//
//   node tools/theme-lab/export-sprites.mjs                 # all themes
//   node tools/theme-lab/export-sprites.mjs eko-2090 ...    # chosen themes
//
// Output: Busara/Assets/Busara/Sprites/Themes/<theme-id>/*.png + manifest.json.
// Rasterises with Chromium (Playwright) so the PNGs match the art lab exactly.
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const outRoot = path.join(repo, 'Busara', 'Assets', 'Busara', 'Sprites', 'Themes');
const engine = require(path.join(here, 'engine.js'));

function loadPlaywright() {
  try { return require('playwright'); } catch { /* fall through to a global install */ }
  const globalRoot = execSync('npm root -g').toString().trim();
  return require(path.join(globalRoot, 'playwright'));
}

const wanted = process.argv.slice(2);
const themes = wanted.length ? engine.THEMES.filter((t) => wanted.includes(t.id)) : engine.THEMES;
if (wanted.length && themes.length !== wanted.length) {
  const known = engine.THEMES.map((t) => t.id).join(', ');
  console.error(`Unknown theme id. Known ids: ${known}`);
  process.exit(1);
}

const { chromium } = loadPlaywright();
const browser = await chromium.launch();
const page = await browser.newPage();
await page.setContent('<!doctype html><html><body></body></html>');
await page.addScriptTag({ path: path.join(here, 'engine.js') });

for (const t of themes) {
  const dir = path.join(outRoot, t.id);
  fs.mkdirSync(dir, { recursive: true });
  const files = engine.spriteList(t.id).map((s) => ({ file: s.file, size: s.size }));
  for (const s of files) {
    const b64 = await page.evaluate(async ({ id, file, size }) => {
      const sprite = window.BusaraThemes.spriteList(id).find((x) => x.file === file);
      const svg = sprite.svg().replace(/ width="\d+" height="\d+"/, ` width="${size}" height="${size}"`);
      const img = new Image();
      img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
      await img.decode();
      const c = document.createElement('canvas');
      c.width = c.height = size;
      c.getContext('2d').drawImage(img, 0, 0, size, size);
      return c.toDataURL('image/png').split(',')[1];
    }, { id: t.id, file: s.file, size: s.size });
    fs.writeFileSync(path.join(dir, s.file), Buffer.from(b64, 'base64'));
  }
  const manifest = {
    id: t.id,
    name: t.name,
    era: t.era,
    origin: t.origin,
    kingdoms: t.kingdoms,
    colors: { accent: t.accent, danger: t.danger, light: t.light, palette: Object.fromEntries(t.palette) },
    board: { frame: t.board.frame, field: t.board.field, frieze: t.board.frieze, slot: t.board.slot },
    pieces: Object.fromEntries(engine.ELEMENTS.map((e) => [e, { material: engine.MATERIALS[t.pieces[e].mat].name, glyph: `${t.glyphs}/${t.glyphMode}` }])),
    geometry: {
      note: 'Board sprite maps 1024 design units to its pixel size. Slots are a 4x4 grid; slot overlays cover one slot pitch.',
      boardUnits: engine.BOARD.size,
      frameBand: engine.BOARD.band,
      slotPitch: engine.BOARD.pitch,
      slotRadius: engine.BOARD.holeR,
      pieceDiscRadiusOf512: 214,
    },
    files,
  };
  fs.writeFileSync(path.join(dir, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
  console.log(`${t.id}: ${files.length} sprites`);
}

await browser.close();
