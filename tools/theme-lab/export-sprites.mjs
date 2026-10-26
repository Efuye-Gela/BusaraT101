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

// Single-mode UI sprite, no mipmaps, alpha is transparency, max size = the file's own size.
const spriteMeta = (guid, size) => `fileFormatVersion: 2
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
  isReadable: 0
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
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
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

  for (const t of themes) {
    const dir = path.join(outRoot, t.id);
    fs.mkdirSync(dir, { recursive: true });
    writeIfChanged(dir + '.meta', folderMeta(stableGuid(assetPath(dir))));
    const sprites = engine.spriteList(t.id);
    for (const s of sprites) {
      const source = s.raster
        ? 'data:image/png;base64,' + fs.readFileSync(path.join(spritesRoot, s.raster)).toString('base64')
        : null;
      const b64 = await page.evaluate(async ({ id, file, size, source }) => {
        let src = source;
        if (!src) {
          const sprite = window.BusaraThemes.spriteList(id).find((x) => x.file === file);
          const svg = sprite.svg().replace(/ width="\d+" height="\d+"/, ` width="${size}" height="${size}"`);
          src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
        }
        const img = new Image();
        img.src = src;
        await img.decode();
        const c = document.createElement('canvas');
        c.width = c.height = size;
        const ctx = c.getContext('2d');
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = 'high';
        // Raster sources keep their aspect ratio, centred on a square canvas.
        const k = Math.min(size / img.naturalWidth, size / img.naturalHeight);
        const w = img.naturalWidth * k, h = img.naturalHeight * k;
        ctx.drawImage(img, (size - w) / 2, (size - h) / 2, w, h);
        return c.toDataURL('image/png').split(',')[1];
      }, { id: t.id, file: s.file, size: s.size, source });
      const png = path.join(dir, s.file);
      fs.writeFileSync(png, Buffer.from(b64, 'base64'));
      writeIfChanged(png + '.meta', spriteMeta(stableGuid(assetPath(png)), s.size));
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
      },
      files: sprites.map((s) => ({ file: s.file, size: s.size, guid: stableGuid(assetPath(path.join(dir, s.file))) })),
    };
    const mf = path.join(dir, 'manifest.json');
    writeIfChanged(mf, JSON.stringify(manifest, null, 2) + '\n');
    writeIfChanged(mf + '.meta', textMeta(stableGuid(assetPath(mf))));
    console.log(`${t.id}: ${sprites.length} sprites`);
  }
  await browser.close();
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) await main();
