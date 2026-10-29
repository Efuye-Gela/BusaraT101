// Renders every theme in engine.js to transparent PNG sprites for Unity.
//
//   node tools/theme-lab/export-sprites.mjs                 # all themes (classic included)
//   node tools/theme-lab/export-sprites.mjs eko-2090 ...    # chosen themes
//
// Output: Busara/Assets/Busara/Sprites/Themes/<theme-id>/*.png + manifest.json, each with a
// Unity .meta whose GUID is derived from the file path, so re-exporting never breaks the
// BoardTheme assets or scene references that point at these sprites.
// Rasterises with Chromium (Playwright) so the PNGs match the art lab exactly.
import { createRequire } from 'node:module';
import { createHash } from 'node:crypto';
import { execSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const spritesRoot = path.join(repo, 'Busara', 'Assets', 'Busara', 'Sprites');
const outRoot = path.join(spritesRoot, 'Themes');
const engine = require(path.join(here, 'engine.js'));

function loadPlaywright() {
  try { return require('playwright'); } catch { /* fall through to a global install */ }
  const globalRoot = execSync('npm root -g').toString().trim();
  return require(path.join(globalRoot, 'playwright'));
}

export const stableGuid = (assetPath) => createHash('md5').update('busara-theme:' + assetPath).digest('hex');

const folderMeta = (guid) =>
  `fileFormatVersion: 2\nguid: ${guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;

const textMeta = (guid) =>
  `fileFormatVersion: 2\nguid: ${guid}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;

const platform = (target, size, quality) => `  - serializedVersion: 4
    buildTarget: ${target}
    maxTextureSize: ${size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: ${quality}
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
`;

// Single-mode UI sprite, no mipmaps, alpha is transparency. size = import cap (power of two),
// border = nine-slice [left, bottom, right, top] in pixels. readable keeps a CPU copy (piece layers are composited at runtime).
const spriteMeta = (guid, size, border = [0, 0, 0, 0], readable = false) => `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: ${readable ? 1 : 0}
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: ${size}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: ${border[0]}, y: ${border[1]}, z: ${border[2]}, w: ${border[3]}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
${platform('DefaultTexturePlatform', size, 100)}${platform('Standalone', size, 50)}${platform('Android', size, 50)}${platform('iOS', size, 50)}${platform('WebGL', size, 50)}  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
`;

const importCap = (px) => { let p = 32; while (p < px) p *= 2; return p; };
const assetPath = (abs) => path.relative(path.join(repo, 'Busara'), abs).split(path.sep).join('/');
const writeIfChanged = (file, text) => { if (!fs.existsSync(file) || fs.readFileSync(file, 'utf8') !== text) fs.writeFileSync(file, text); };

async function main() {
  const wanted = process.argv.slice(2);
  const themes = wanted.length ? engine.ALL_THEMES.filter((t) => wanted.includes(t.id)) : engine.ALL_THEMES;
  if (wanted.length && themes.length !== wanted.length) {
    console.error(`Unknown theme id. Known ids: ${engine.ALL_THEMES.map((t) => t.id).join(', ')}`);
    process.exit(1);
  }

  const { chromium } = loadPlaywright();
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.setContent('<!doctype html><html><body></body></html>');
  await page.addScriptTag({ path: path.join(here, 'engine.js') });

  fs.mkdirSync(outRoot, { recursive: true });
  writeIfChanged(outRoot + '.meta', folderMeta(stableGuid(assetPath(outRoot))));

  // Rasterise an SVG string (generated in Node) at W x H and return base64 PNG.
  const rasterise = (svg, W, H) => page.evaluate(async ({ svg, W, H }) => {
    const img = new Image();
    img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg.replace(/ width="\d+" height="\d+"/, ` width="${W}" height="${H}"`));
    await img.decode();
    const c = document.createElement('canvas');
    c.width = W; c.height = H;
    c.getContext('2d').drawImage(img, 0, 0, W, H);
    return c.toDataURL('image/png').split(',')[1];
  }, { svg, W, H });
  const ensureFolder = (dir) => {
    for (let d = dir; d.startsWith(spritesRoot + path.sep); d = path.dirname(d)) {
      fs.mkdirSync(d, { recursive: true });
      writeIfChanged(d + '.meta', folderMeta(stableGuid(assetPath(d))));
    }
  };
  const writtenLayers = new Set();
  const report = {};

  for (const t of themes) {
    const dir = path.join(outRoot, t.id);
    fs.mkdirSync(dir, { recursive: true });
    writeIfChanged(dir + '.meta', folderMeta(stableGuid(assetPath(dir))));
    const sprites = engine.spriteList(t.id);
    for (const s of sprites) {
      const source = s.raster
        ? 'data:image/png;base64,' + fs.readFileSync(path.join(spritesRoot, s.raster)).toString('base64')
        : null;
      const W = s.w || s.size, H = s.h || s.size;
      const b64 = await page.evaluate(async ({ id, file, W, H, source }) => {
        let src = source;
        if (!src) {
          const sprite = window.BusaraThemes.spriteList(id).find((x) => x.file === file);
          const svg = sprite.svg().replace(/ width="\d+" height="\d+"/, ` width="${W}" height="${H}"`);
          src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
        }
        const img = new Image();
        img.src = src;
        await img.decode();
        const c = document.createElement('canvas');
        c.width = W;
        c.height = H;
        const ctx = c.getContext('2d');
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = 'high';
        // Raster sources keep their aspect ratio, centred on the canvas.
        const k = Math.min(W / img.naturalWidth, H / img.naturalHeight);
        const w = img.naturalWidth * k, h = img.naturalHeight * k;
        ctx.drawImage(img, (W - w) / 2, (H - h) / 2, w, h);
        return c.toDataURL('image/png').split(',')[1];
      }, { id: t.id, file: s.file, W, H, source });
      const png = path.join(dir, s.file);
      const sub = path.dirname(png);
      if (sub !== dir) {
        fs.mkdirSync(sub, { recursive: true });
        writeIfChanged(sub + '.meta', folderMeta(stableGuid(assetPath(sub))));
      }
      fs.writeFileSync(png, Buffer.from(b64, 'base64'));
      writeIfChanged(png + '.meta', spriteMeta(stableGuid(assetPath(png)), importCap(Math.max(W, H)), s.border));
    }
    const manifest = {
      id: t.id,
      name: t.name,
      era: t.era,
      origin: t.origin,
      kingdoms: t.kingdoms,
      colors: { accent: t.accent, danger: t.danger, light: t.light, palette: Object.fromEntries(t.palette) },
      board: { frame: t.board.frame, field: t.board.field, frieze: t.board.frieze, slot: t.board.slot },
      pieces: Object.fromEntries(engine.ELEMENTS.map((e) => [e, t.raster
        ? { source: t.raster[e] }
        : { material: engine.MATERIALS[t.pieces[e].mat].name, glyph: `${t.glyphs}/${t.glyphMode}` }])),
      geometry: {
        note: 'Board sprite maps 1024 design units to its pixel size. Slots are a 4x4 grid; slot overlays cover one slot pitch.',
        boardUnits: engine.BOARD.size,
        frameBand: engine.BOARD.band,
        slotPitch: engine.BOARD.pitch,
        slotRadius: engine.BOARD.holeR,
        pieceDiscRadiusOf512: 214,
        pieceShape: t.raster ? 'icon' : engine.themeShape(t),
        // sprite height / width, and the base centre's height from the sprite bottom (fraction)
        pieceAspect: t.raster ? 1 : Number(engine.pieceAspect(t).toFixed(4)),
        piecePivotY: t.raster ? 0.5 : Number(engine.piecePivotY(t).toFixed(4)),
      },
      files: sprites.map((s) => Object.assign({ file: s.file, width: s.w || s.size, height: s.h || s.size, guid: stableGuid(assetPath(path.join(dir, s.file))) },
        s.border && s.border.some((v) => v > 0) ? { nineSliceBorder: { left: s.border[0], bottom: s.border[1], right: s.border[2], top: s.border[3] } } : {})),
    };
    // Layer library: shared sprites under Sprites/Pieces/<shape>/, stacks per element in the manifest.
    const lib = engine.layerLibrary(t.id);
    if (lib) {
      for (const e of lib.entries) {
        if (writtenLayers.has(e.path)) continue;
        writtenLayers.add(e.path);
        const file = path.join(spritesRoot, e.path);
        ensureFolder(path.dirname(file));
        fs.writeFileSync(file, Buffer.from(await rasterise(e.svg(), e.w, e.h), 'base64'));
        writeIfChanged(file + '.meta', spriteMeta(stableGuid(assetPath(file)), importCap(Math.max(e.w, e.h)), undefined, true));
      }
      manifest.layers = Object.fromEntries(Object.entries(lib.stacks).map(([k, stack]) => [k, stack.map((ly) => ({
        name: ly.name, file: ly.path, tint: ly.tint, guid: stableGuid(assetPath(path.join(spritesRoot, ly.path))),
      }))]));
      report[t.id] = {};
      for (const [k, stack] of Object.entries(lib.stacks)) {
        const direct = fs.readFileSync(path.join(dir, k === 'hidden' ? 'piece_hidden.png' : `piece_${k}.png`)).toString('base64');
        const layers = stack.map((ly) => ({ tint: ly.tint, b64: fs.readFileSync(path.join(spritesRoot, ly.path)).toString('base64') }));
        report[t.id][k] = await page.evaluate(verifyComposite, { direct, layers, board: t.board.fieldTint || engine.MATERIALS[t.board.field].base });
      }
    }
    const mf = path.join(dir, 'manifest.json');
    writeIfChanged(mf, JSON.stringify(manifest, null, 2) + '\n');
    writeIfChanged(mf + '.meta', textMeta(stableGuid(assetPath(mf))));
    console.log(`${t.id}: ${sprites.length} sprites`);
  }
  await browser.close();
  if (Object.keys(report).length) {
    const rows = Object.values(report).flatMap((r) => Object.values(r));
    const worst = (key) => Math.max(...rows.map((r) => r[key]));
    const summary = {
      stacks: rows.length,
      meanDelta: Number((rows.reduce((s, r) => s + r.mean, 0) / rows.length).toFixed(4)),
      worstMeanDelta: worst('mean'), worstMaxDelta: worst('max'),
      unityLinearMeanDelta: Number((rows.reduce((s, r) => s + r.linearMean, 0) / rows.length).toFixed(4)),
      unityLinearWorstMax: worst('linearMax'),
    };
    fs.writeFileSync(path.join(here, 'layer-verification.json'), JSON.stringify({ summary, themes: report }, null, 2) + '\n');
    console.log(`layers: composite vs direct render, mean channel delta ${summary.meanDelta} / 255 (worst stack ${summary.worstMeanDelta}, max ${summary.worstMaxDelta}); ` +
      `(stacking them as separate UI Images in linear colour space would drift by mean ${summary.unityLinearMeanDelta}, max ${summary.unityLinearWorstMax}, so Unity composites in sRGB on the CPU)`);
    if (summary.worstMeanDelta > 0.5) { console.error('Layer composite drifted from the direct render.'); process.exitCode = 1; }
  }
}

// Runs in the page. Composites tinted layer PNGs exactly like a Unity UI Image stack (rgb * tint, then over)
// and compares with the direct render: premultiplied sRGB channels, and on-screen colour in linear blending.
async function verifyComposite({ direct, layers, board }) {
  const load = async (b64) => {
    const img = new Image(); img.src = 'data:image/png;base64,' + b64; await img.decode();
    const c = document.createElement('canvas'); c.width = img.naturalWidth; c.height = img.naturalHeight;
    const x = c.getContext('2d'); x.drawImage(img, 0, 0);
    return x.getImageData(0, 0, c.width, c.height);
  };
  const hex = (h) => { const n = parseInt(h.slice(1), 16); return [(n >> 16) & 255, (n >> 8) & 255, n & 255].map((v) => v / 255); };
  const lin = (v) => (v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4));
  const enc = (v) => (v <= 0.0031308 ? v * 12.92 : 1.055 * Math.pow(v, 1 / 2.4) - 0.055);
  const D = await load(direct), N = D.width * D.height;
  const acc = new Float32Array(N * 4), accL = new Float32Array(N * 3), bg = hex(board).map(lin);
  for (let i = 0; i < N; i++) { accL[i * 3] = bg[0]; accL[i * 3 + 1] = bg[1]; accL[i * 3 + 2] = bg[2]; }
  for (const ly of layers) {
    const I = await load(ly.b64), t = ly.tint ? hex(ly.tint) : [1, 1, 1], tl = t.map(lin), d = I.data;
    for (let i = 0; i < N; i++) {
      const a = d[i * 4 + 3] / 255; if (!a) continue;
      for (let c = 0; c < 3; c++) {
        const v = (d[i * 4 + c] / 255) * t[c];
        acc[i * 4 + c] = v * a + acc[i * 4 + c] * (1 - a);
        accL[i * 3 + c] = lin(d[i * 4 + c] / 255) * tl[c] * a + accL[i * 3 + c] * (1 - a);
      }
      acc[i * 4 + 3] = a + acc[i * 4 + 3] * (1 - a);
    }
  }
  let sum = 0, max = 0, sumL = 0, maxL = 0;
  const dd = D.data;
  for (let i = 0; i < N; i++) {
    const da = dd[i * 4 + 3] / 255;
    for (let c = 0; c < 4; c++) {
      const ref = c === 3 ? da : (dd[i * 4 + c] / 255) * da;
      const e = Math.abs(acc[i * 4 + c] - ref) * 255; sum += e; if (e > max) max = e;
    }
    for (let c = 0; c < 3; c++) {
      const refL = lin(dd[i * 4 + c] / 255) * da + bg[c] * (1 - da);
      const e = Math.abs(enc(accL[i * 3 + c]) - enc(refL)) * 255; sumL += e; if (e > maxL) maxL = e;
    }
  }
  return { mean: Number((sum / (N * 4)).toFixed(4)), max: Math.round(max), linearMean: Number((sumL / (N * 3)).toFixed(4)), linearMax: Math.round(maxL) };
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) await main();
