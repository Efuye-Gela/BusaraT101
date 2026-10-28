/*
 * Busara theme engine.
 *
 * One source of truth for every themed board, hole, piece and state sprite.
 * The art lab (index.html) renders these SVGs live; export-sprites.mjs
 * rasterises the exact same SVGs to PNG for Unity. Nothing here touches the DOM.
 *
 * Geometry matches the game: each player board is a 4x4 grid of round slots
 * (Board.cs / Slot.cs), pieces are round Resource discs of four elements
 * (ResourceType: Water, Earth, Fire, Air).
 */
(function (root) {
  'use strict';

  // ---------------------------------------------------------------- utils
  let uidCounter = 0;
  const uid = (p) => `${p || 'b'}${(uidCounter++).toString(36)}`;

  function rng(seed) {
    let a = seed >>> 0;
    return function () {
      a |= 0; a = (a + 0x6d2b79f5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  function hashStr(s) {
    let h = 2166136261;
    for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); }
    return h >>> 0;
  }
  function hex2rgb(h) {
    h = h.replace('#', '');
    if (h.length === 3) h = h.split('').map((c) => c + c).join('');
    const n = parseInt(h, 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  }
  function rgb2hex(r, g, b) {
    return '#' + [r, g, b].map((v) => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')).join('');
  }
  function mix(a, b, t) {
    const x = hex2rgb(a), y = hex2rgb(b);
    return rgb2hex(x[0] + (y[0] - x[0]) * t, x[1] + (y[1] - x[1]) * t, x[2] + (y[2] - x[2]) * t);
  }
  const lighten = (c, t) => mix(c, '#ffffff', t);
  const darken = (c, t) => mix(c, '#000000', t);
  function luminance(c) {
    const [r, g, b] = hex2rgb(c).map((v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); });
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
  }
  const f = (n) => (Math.round(n * 100) / 100).toString();
  const svgWrap = (w, h, body, extra) =>
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}"${extra || ''}>${body}</svg>`;

  // A colour-keyed noise layer: turbulence -> alpha via a transfer table -> flat colour.
  function noiseLayer(u, o) {
    const id = uid(u + 'n');
    const [r, g, b] = hex2rgb(o.color).map((v) => f(v / 255));
    const def =
      `<filter id="${id}" filterUnits="userSpaceOnUse" x="${o.x}" y="${o.y}" width="${o.w}" height="${o.h}" color-interpolation-filters="sRGB">` +
      `<feTurbulence type="${o.type || 'fractalNoise'}" baseFrequency="${o.freq}" numOctaves="${o.oct || 3}" seed="${o.seed || 1}" stitchTiles="stitch"/>` +
      `<feColorMatrix type="matrix" values="0 0 0 0 ${r} 0 0 0 0 ${g} 0 0 0 0 ${b} ${o.ch === 'g' ? '0 1 0 0 0' : '1 0 0 0 0'}"/>` +
      `<feComponentTransfer><feFuncA type="${o.fn || 'table'}" tableValues="${o.table}"/></feComponentTransfer>` +
      (o.blur ? `<feGaussianBlur stdDeviation="${o.blur}"/>` : '') +
      `</filter>`;
    const el = `<rect x="${o.x}" y="${o.y}" width="${o.w}" height="${o.h}" filter="url(#${id})" opacity="${o.op == null ? 1 : o.op}"${o.blend ? ` style="mix-blend-mode:${o.blend}"` : ''}/>`;
    return { def, el };
  }

  // ------------------------------------------------------------ elements
  // The readability contract: every theme keeps these hue families so a piece
  // is identifiable by colour AND by glyph silhouette (colour-blind safe).
  const ELEMENTS = ['water', 'earth', 'fire', 'air'];
  const ELEMENT_INFO = {
    water: { name: 'Water', hue: '#2F6DB5' },
    earth: { name: 'Earth', hue: '#4A4440' },
    fire: { name: 'Fire', hue: '#B8321F' },
    air: { name: 'Air', hue: '#D69A2D' },
  };

  // Glyphs in a 100x100 box. 'ornate' = carved, layered, past kingdoms.
  // 'minimal' = single-weight geometric line, future cities.
  const GLYPHS = {
    ornate: {
      fire: [
        { fill: 'M50 7C58 23 75 33 77 56C79 77 65 93 50 93C35 93 21 79 23 58C24 46 31 38 35 27C38 37 42 43 46 45C48 33 46 19 50 7ZM50 51C56 59 62 66 60 76C58 84 54 87 50 87C45 87 40 83 40 75C40 67 46 63 50 51Z' },
        { dot: [50, 70, 3.2] },
      ],
      water: [
        { stroke: 'M12 34C22 23 32 23 39 33C46 43 55 43 62 33C69 23 79 23 88 34', w: 7 },
        { stroke: 'M12 54C22 43 32 43 39 53C46 63 55 63 62 53C69 43 79 43 88 54', w: 7 },
        { stroke: 'M12 74C22 63 32 63 39 73C46 83 55 83 62 73C69 63 79 63 88 74', w: 7 },
        { dot: [50, 18, 3.5] },
      ],
      earth: [
        { fill: 'M8 80L37 28L51 50L61 36L92 80ZM37 44L22 70H52ZM61 52L56 60L64 70H78Z' },
        { dot: [26, 88, 3] }, { dot: [40, 88, 3] }, { dot: [54, 88, 3] }, { dot: [68, 88, 3] },
      ],
      air: [
        { stroke: 'M10 34H54C67 34 72 20 62 14C54 10 46 17 50 25', w: 7 },
        { stroke: 'M10 54H72C87 54 92 71 81 78C72 83 63 75 68 67', w: 7 },
        { stroke: 'M22 74H48', w: 7 },
        { dot: [84, 34, 3.5] },
      ],
    },
    minimal: {
      fire: [{ stroke: 'M22 72L50 24L78 72', w: 6 }, { stroke: 'M37 74L50 52L63 74', w: 6 }],
      water: [
        { stroke: 'M20 36Q35 26 50 36T80 36', w: 6 },
        { stroke: 'M20 52Q35 42 50 52T80 52', w: 6 },
        { stroke: 'M20 68Q35 58 50 68T80 68', w: 6 },
      ],
      earth: [{ stroke: 'M27 27H73V73H27Z', w: 6 }, { stroke: 'M27 50H73', w: 6 }],
      air: [{ stroke: 'M68 32A25 25 0 1 0 75 52', w: 6 }, { dot: [50, 50, 6] }, { stroke: 'M75 52H90', w: 6 }],
    },
    angular: {
      fire: [{ stroke: 'M50 12L82 80H18Z', w: 6 }, { stroke: 'M50 42L65 72H35Z', w: 6 }, { dot: [50, 88, 3.5] }],
      water: [
        { stroke: 'M16 34L30 24L44 34L58 24L72 34L86 24', w: 6 },
        { stroke: 'M16 54L30 44L44 54L58 44L72 54L86 44', w: 6 },
        { stroke: 'M16 74L30 64L44 74L58 64L72 74L86 64', w: 6 },
      ],
      earth: [{ stroke: 'M10 78L36 30L50 54L62 38L90 78Z', w: 6 }, { stroke: 'M36 30L44 78M62 38L56 78', w: 3.5 }],
      air: [{ stroke: 'M54 50H62V38H40V62H72V26H28V74H88', w: 6 }, { dot: [54, 50, 3.5] }],
    },
  };

  function glyphMarkup(set, el, x, y, size, color, sw) {
    const s = size / 100;
    const parts = GLYPHS[set][el].map((p) => {
      if (p.fill) return `<path d="${p.fill}" fill="${color}" fill-rule="evenodd"/>`;
      if (p.stroke) return `<path d="${p.stroke}" fill="none" stroke="${color}" stroke-width="${(p.w * (sw || 1))}" stroke-linecap="round" stroke-linejoin="round"/>`;
      if (p.dot) return `<circle cx="${p.dot[0]}" cy="${p.dot[1]}" r="${p.dot[2] * (sw || 1)}" fill="${color}"/>`;
      return '';
    }).join('');
    return `<g transform="translate(${f(x)} ${f(y)}) scale(${f(s)})">${parts}</g>`;
  }

  // ------------------------------------------------------------ materials
  // family drives the texture recipe; base/dark/light are the material's own colours.
  const MATERIALS = {
    // past: woods
    ebony: { name: 'Ebony', era: 'past', family: 'wood', base: '#2B201C', dark: '#140E0C', light: '#4A3A32', shine: 0.35, origin: 'Diospyros, Central & West African forests; carvers prize it for crisp detail.' },
    mahogany: { name: 'Khaya mahogany', era: 'past', family: 'wood', base: '#7A3524', dark: '#4A1D13', light: '#A2533A', shine: 0.3, origin: 'West African khaya, the classic ceremonial-stool timber.' },
    iroko: { name: 'Iroko', era: 'past', family: 'wood', base: '#9A6A37', dark: '#6A4421', light: '#C3924F', shine: 0.25, origin: 'Sacred tree of Yoruba and Igbo lands; golden, durable.' },
    wenge: { name: 'Wengé', era: 'past', family: 'wood', base: '#3D2C22', dark: '#1D140F', light: '#6A5243', shine: 0.2, grain: 'bold', origin: 'Congo Basin hardwood with bold espresso stripes.' },
    kiaat: { name: 'Kiaat', era: 'past', family: 'wood', base: '#8E4A2A', dark: '#5C2C17', light: '#B9734A', shine: 0.28, origin: 'Kavango woodcarving timber of northern Namibia.' },
    camwood: { name: 'Camwood (tukula)', era: 'past', family: 'wood', base: '#8F2F22', dark: '#5A1A12', light: '#B9503B', shine: 0.2, origin: 'Kuba and Kongo red dyewood, ground into tukula paste.' },
    mangrove: { name: 'Mangrove teak', era: 'past', family: 'wood', base: '#5E3A24', dark: '#38210F', light: '#86593A', shine: 0.3, origin: 'Swahili-coast boatbuilding and door-carving wood.' },
    // past: fibres and hides
    raffia: { name: 'Raffia weave', era: 'past', family: 'weave', base: '#C9A56B', dark: '#8E6E3E', light: '#E6CC93', shine: 0.05, origin: 'Kuba raffia palm cloth, woven by men, embroidered by women.' },
    leather_indigo: { name: 'Indigo leather', era: 'past', family: 'leather', base: '#25335A', dark: '#121A33', light: '#3E5287', shine: 0.15, origin: 'Tuareg and Hausa indigo-dyed goat leather.' },
    leather_ochre: { name: 'Ochre leather', era: 'past', family: 'leather', base: '#9C4A28', dark: '#5F2913', light: '#C46C42', shine: 0.15, origin: 'Hide rubbed with otjize ochre butter, Kunene and Kavango.' },
    // past: metals
    gold: { name: 'Gold', era: 'past', family: 'metal', base: '#C9962E', dark: '#7A5414', light: '#F4D77E', shine: 0.9, origin: 'Akan, Bambuk and Mapungubwe goldfields.' },
    brass: { name: 'Goldweight brass', era: 'past', family: 'metal', base: '#B08A3E', dark: '#6A4E1C', light: '#E2C578', shine: 0.75, origin: 'Lost-wax cast like Asante abrammuo goldweights.' },
    silver: { name: 'Sahel silver', era: 'past', family: 'metal', base: '#B9BCC0', dark: '#6B6F75', light: '#F1F2F3', shine: 0.85, origin: 'Tuareg (Inadan) smithing and Ethiopian processional crosses.' },
    bronze: { name: 'Edo bronze', era: 'past', family: 'metal', base: '#7C5530', dark: '#3F2A15', light: '#B48552', shine: 0.6, origin: 'Leaded brass of the Benin court guilds (Igun Eronmwon).' },
    copper: { name: 'Katanga copper', era: 'past', family: 'metal', base: '#A9583A', dark: '#5E2B1A', light: '#E09A72', shine: 0.75, origin: 'Copper croisettes, the currency of the Copperbelt.' },
    // past: stones and gems
    malachite: { name: 'Malachite', era: 'past', family: 'banded', base: '#1E7A55', dark: '#0B3F2A', light: '#53B78C', shine: 0.55, origin: 'Katanga, DR Congo; banded green carbonate.' },
    carnelian: { name: 'Carnelian', era: 'past', family: 'gem', base: '#B0401E', dark: '#6C1E0B', light: '#E77B4C', shine: 0.7, origin: 'Kerma and Meroë bead workshops, Nubia.' },
    tanzanite: { name: 'Tanzanite', era: 'past', family: 'gem', base: '#3A4BA6', dark: '#1D2560', light: '#7F8EE0', shine: 0.85, origin: 'Mererani Hills, the only source on Earth.' },
    tigerseye: { name: "Tiger's eye", era: 'past', family: 'chatoyant', base: '#A8711E', dark: '#5A3708', light: '#E8B85C', shine: 0.7, origin: 'Northern Cape, South Africa; silky shifting bands.' },
    river_stone: { name: 'River stone', era: 'past', family: 'pebble', base: '#5C6266', dark: '#33383B', light: '#8D9497', shine: 0.25, origin: 'Tumbled basalt from Blue Nile and Omo river beds.' },
    granite: { name: 'Aksum granite', era: 'past', family: 'granite', base: '#6E625E', dark: '#3E3634', light: '#A39791', shine: 0.15, origin: 'Nephelinite-syenite of the Aksum stelae.' },
    marble: { name: 'Karibib marble', era: 'past', family: 'marble', base: '#E6E1D8', dark: '#9C958A', light: '#FBF9F5', shine: 0.5, origin: 'Karibib, Namibia; white with grey veins.' },
    soapstone: { name: 'Soapstone', era: 'past', family: 'pebble', base: '#6F7A62', dark: '#3E4635', light: '#A1AC90', shine: 0.3, origin: 'Great Zimbabwe bird carvings; soft and waxy.' },
    sandstone: { name: 'Nubian sandstone', era: 'past', family: 'granite', base: '#C08A57', dark: '#83572E', light: '#E3B886', shine: 0.05, origin: 'The stone of the Meroë pyramids.' },
    faience: { name: 'Faience', era: 'past', family: 'glaze', base: '#2C9C9A', dark: '#11585A', light: '#7FD6CF', shine: 0.75, origin: 'Kushite glazed quartz paste, Meroë and Kerma.' },
    terracotta: { name: 'Nok terracotta', era: 'past', family: 'clay', base: '#A55A35', dark: '#6A3418', light: '#CC8559', shine: 0.05, origin: 'Nok culture fired clay, 1500 BCE onward.' },
    imigongo_clay: { name: 'Imigongo clay', era: 'past', family: 'clay', base: '#2A2523', dark: '#120F0E', light: '#4C4440', shine: 0.05, origin: 'Rwandan cow-dung relief, ash black, kaolin white, red earth.' },
    cowrie: { name: 'Cowrie shell', era: 'past', family: 'shell', base: '#EFE6D2', dark: '#B7A685', light: '#FFFDF6', shine: 0.85, origin: 'Indian Ocean money shell, traded inland for centuries.' },
    coral_stone: { name: 'Coral stone', era: 'past', family: 'porous', base: '#C97A5E', dark: '#874530', light: '#EBB096', shine: 0.15, origin: 'Kilwa and Lamu building stone, cut from reef limestone.' },
    mkomwe_seed: { name: 'Mkomwe seed', era: 'past', family: 'seed', base: '#6E5B4E', dark: '#3D3029', light: '#A1907E', shine: 0.7, origin: 'Grey nicker seeds, the traditional bao counters.' },
    // future: engineered and fictional
    data_glass: { name: 'Data glass', era: 'future', family: 'glass', base: '#1C5E7A', dark: '#0A2633', light: '#A8E9FF', shine: 0.9, origin: 'Fictional. N\'evula storage crystal; each disc holds a ledger of Data.' },
    nano_ceramic: { name: 'Nano-ceramic', era: 'future', family: 'ceramic', base: '#ECEEEE', dark: '#B4BABB', light: '#FFFFFF', shine: 0.4, origin: 'Sintered zirconia, matte and warm to the touch.' },
    graphene: { name: 'Graphene laminate', era: 'future', family: 'graphene', base: '#26292C', dark: '#121416', light: '#4A5056', shine: 0.5, origin: 'Stacked carbon sheets; hexagonal micro-sheen.' },
    anodized: { name: 'Anodised titanium', era: 'future', family: 'anodized', base: '#4B5FA8', dark: '#232E5C', light: '#B3A7E8', shine: 0.85, origin: 'Heat-tinted titanium from Lagos makerspaces.' },
    solar_glass: { name: 'Solar glass', era: 'future', family: 'solar', base: '#16305E', dark: '#0A1530', light: '#6C8CC8', shine: 0.85, origin: 'Perovskite skins that power the piece\'s own light.' },
    ewaste_alloy: { name: 'Reclaimed alloy', era: 'future', family: 'fleck', base: '#6E7376', dark: '#3D4143', light: '#B7BCBE', shine: 0.6, origin: 'Smelted from Agbogbloshie e-waste, flecked with old circuitry.' },
    liquid_chrome: { name: 'Liquid chrome', era: 'future', family: 'chrome', base: '#9AA3AB', dark: '#2A3036', light: '#F6F8FA', shine: 1, origin: 'Fictional. A metal that holds its shape only on the board.' },
    volta_stone: { name: 'Volta stone', era: 'future', family: 'veined', base: '#1D1B22', dark: '#0B0A0E', light: '#3A3644', shine: 0.4, origin: 'Fictional. Resonant meteoric ore of the Nakomean warriors; glows under will.' },
    dream_glass: { name: 'Ndoto glass', era: 'future', family: 'iridescent', base: '#3B2B6E', dark: '#160F2E', light: '#C9B8FF', shine: 0.9, origin: 'Fictional. Ubunifu glass that colours itself with the holder\'s thought.' },
    bio_lumen: { name: 'Biolume resin', era: 'future', family: 'lumen', base: '#0E2A26', dark: '#06120F', light: '#62F2C7', shine: 0.6, origin: 'Fictional. Resin seeded with glowing mycelium.' },
    kinetic_bead: { name: 'Kinetic bead', era: 'future', family: 'kinetic', base: '#121218', dark: '#07070B', light: '#8E92A8', shine: 0.7, origin: 'Fictional. A nanoweave shell over a core that stores every impact and gives it back as light.' },
    kinetic_ore: { name: 'Kinetic ore', era: 'future', family: 'veined', base: '#1B1824', dark: '#0B0A10', light: '#3C3650', shine: 0.35, origin: 'Fictional. Meteoric ore that drinks impacts and glows violet along its seams.' },
    slate: { name: 'Slate lacquer', era: 'future', family: 'gradient', base: '#4C514B', dark: '#353935', light: '#5E645C', shine: 0.2, origin: 'The original Busara board finish: matte slate with amber hairlines.' },
    shift_prism: { name: 'Shift prism', era: 'future', family: 'prism', base: '#8A7BD1', dark: '#2E2752', light: '#F0E9FF', shine: 0.95, origin: 'Fictional. Aradas crystal that never shows the same colour twice.' },
  };

  // Paint a material into a region. region: {shape:'circle', cx, cy, r} | {shape:'rect', x, y, w, h, rx}
  // color overrides the material's base colour (dyes, glazes, enamels, light).
  function paintMaterial(u, matId, region, color, opts) {
    const m = MATERIALS[matId];
    const o = opts || {};
    const base = color || m.base;
    const dark = color ? darken(color, 0.55) : m.dark;
    const light = color ? lighten(color, 0.45) : m.light;
    const seed = (hashStr(matId + (color || '')) % 900) + 1 + (o.seed || 0);
    const R = rng(seed);
    const defs = [], els = [];
    const isC = region.shape === 'circle';
    const bx = isC ? region.cx - region.r : region.x;
    const by = isC ? region.cy - region.r : region.y;
    const bw = isC ? region.r * 2 : region.w;
    const bh = isC ? region.r * 2 : region.h;
    const cx = bx + bw / 2, cy = by + bh / 2, rad = Math.max(bw, bh) / 2;
    const box = { x: bx, y: by, w: bw, h: bh };
    const N = (o2) => { const n = noiseLayer(u, Object.assign({}, box, o2)); defs.push(n.def); els.push(n.el); };
    const scale = isC ? 1 : Math.min(1, 420 / rad); // keep texture size physical on big boards

    els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="${base}"/>`);

    switch (m.family) {
      case 'wood': {
        const bold = m.grain === 'bold';
        N({ freq: `${f(0.004 * scale)} ${f((bold ? 0.075 : 0.11) * scale)}`, oct: 4, seed, table: bold ? '0 0 0.85 0 0 0.6 0 0 0.9 0' : '0 0.15 0.55 0.1 0 0.35 0.7 0.1 0', color: dark, op: 0.75 });
        N({ freq: `${f(0.006 * scale)} ${f(0.2 * scale)}`, oct: 2, seed: seed + 7, table: '0 0 0 0.35 0 0 0.4 0', color: light, op: 0.5 });
        break;
      }
      case 'weave': {
        const pid = uid(u + 'w');
        const s = 14;
        defs.push(`<pattern id="${pid}" width="${s * 2}" height="${s * 2}" patternUnits="userSpaceOnUse">` +
          `<rect width="${s}" height="${s}" fill="${light}" opacity="0.55"/><rect x="${s}" y="${s}" width="${s}" height="${s}" fill="${light}" opacity="0.55"/>` +
          `<path d="M0 ${s / 2}H${s}M${s / 2} ${s}V${s * 2}M${s} ${s * 1.5}H${s * 2}M${s * 1.5} 0V${s}" stroke="${dark}" stroke-width="1.6" opacity="0.6"/></pattern>`);
        els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="url(#${pid})"/>`);
        N({ freq: '0.03 0.6', oct: 2, seed, table: '0 0 0.5 0', color: dark, op: 0.5 });
        break;
      }
      case 'leather': {
        N({ freq: f(0.32 * scale), oct: 3, seed, table: '0 0 0.25 0 0.4 0', color: dark, op: 0.8 });
        N({ freq: f(0.02 * scale), oct: 3, seed: seed + 3, table: '0 0.35 0 0.25', color: light, op: 0.35 });
        break;
      }
      case 'metal': {
        if (isC) {
          let rings = '';
          for (let r = 4; r < region.r; r += 2.6) {
            const a = 0.03 + R() * 0.09;
            rings += `<circle cx="${f(cx)}" cy="${f(cy)}" r="${f(r)}" fill="none" stroke="${R() > 0.5 ? light : dark}" stroke-width="1.3" opacity="${f(a)}"/>`;
          }
          els.push(rings);
        } else {
          N({ freq: `${f(0.002)} ${f(0.5)}`, oct: 2, seed, table: '0 0.3 0 0.25 0', color: light, op: 0.5 });
          N({ freq: `${f(0.003)} ${f(0.35)}`, oct: 2, seed: seed + 2, table: '0 0.35 0 0.3', color: dark, op: 0.5 });
        }
        const gid = uid(u + 'g');
        defs.push(`<linearGradient id="${gid}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${light}" stop-opacity="0.85"/><stop offset="0.35" stop-color="${base}" stop-opacity="0"/><stop offset="0.62" stop-color="${dark}" stop-opacity="0.15"/><stop offset="0.8" stop-color="${light}" stop-opacity="0.45"/><stop offset="1" stop-color="${dark}" stop-opacity="0.6"/></linearGradient>`);
        els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="url(#${gid})"/>`);
        break;
      }
      case 'banded': {
        const fid = uid(u + 'd');
        defs.push(`<filter id="${fid}" filterUnits="userSpaceOnUse" x="${bx}" y="${by}" width="${bw}" height="${bh}"><feTurbulence type="fractalNoise" baseFrequency="${f(0.012 * scale)}" numOctaves="2" seed="${seed}"/><feDisplacementMap in="SourceGraphic" scale="${f(rad * 0.35)}" xChannelSelector="R" yChannelSelector="G"/></filter>`);
        let bands = '';
        const ox = cx + rad * 0.3, oy = cy - rad * 0.4;
        for (let r = 6, i = 0; r < rad * 2.4; r += 5 + R() * 9, i++) {
          bands += `<circle cx="${f(ox)}" cy="${f(oy)}" r="${f(r)}" fill="none" stroke="${i % 3 === 0 ? dark : light}" stroke-width="${f(2 + R() * 5)}" opacity="${f(0.35 + R() * 0.45)}"/>`;
        }
        els.push(`<g filter="url(#${fid})">${bands}</g>`);
        break;
      }
      case 'gem': {
        if (isC) {
          const n = 12, r1 = region.r * 0.48;
          let facets = '';
          for (let i = 0; i < n; i++) {
            const a0 = (i / n) * Math.PI * 2, a1 = ((i + 1) / n) * Math.PI * 2;
            const p = (r, a) => `${f(cx + Math.cos(a) * r)} ${f(cy + Math.sin(a) * r)}`;
            const t = 0.5 + 0.5 * Math.cos(a0 - 3.9);
            facets += `<path d="M${p(r1, a0)}L${p(region.r * 1.02, a0)}L${p(region.r * 1.02, a1)}L${p(r1, a1)}Z" fill="${mix(dark, light, t * 0.8)}" opacity="0.75"/>`;
            facets += `<path d="M${f(cx)} ${f(cy)}L${p(r1, a0)}L${p(r1, a1)}Z" fill="${mix(base, light, 0.25 + 0.4 * (1 - t))}" opacity="0.5"/>`;
          }
          els.push(facets);
        } else {
          N({ freq: f(0.01), oct: 3, seed, table: '0 0.4 0 0.5', color: light, op: 0.5 });
        }
        N({ freq: f(0.5 * scale), oct: 1, seed: seed + 4, table: '0 0 0 0 0 0 0.9', color: '#ffffff', op: 0.35 });
        break;
      }
      case 'chatoyant': {
        const fid = uid(u + 'c');
        defs.push(`<filter id="${fid}" filterUnits="userSpaceOnUse" x="${bx}" y="${by}" width="${bw}" height="${bh}"><feTurbulence type="fractalNoise" baseFrequency="${f(0.008 * scale)} ${f(0.03 * scale)}" numOctaves="2" seed="${seed}"/><feDisplacementMap in="SourceGraphic" scale="${f(rad * 0.25)}" xChannelSelector="R" yChannelSelector="G"/></filter>`);
        let bands = '';
        for (let y = by - 40, i = 0; y < by + bh + 40; y += 6 + R() * 14, i++) {
          bands += `<rect x="${bx - 40}" y="${f(y)}" width="${bw + 80}" height="${f(3 + R() * 9)}" fill="${i % 2 ? light : dark}" opacity="${f(0.3 + R() * 0.5)}"/>`;
        }
        els.push(`<g filter="url(#${fid})" transform="rotate(-24 ${f(cx)} ${f(cy)})">${bands}</g>`);
        break;
      }
      case 'pebble': {
        N({ freq: f(0.015 * scale), oct: 4, seed, table: '0 0.35 0.1 0.45 0', color: dark, op: 0.55 });
        N({ freq: f(0.6 * scale), oct: 2, seed: seed + 9, table: '0 0 0 0.5 0', color: light, op: 0.35 });
        break;
      }
      case 'granite': {
        N({ freq: f(0.7 * scale), oct: 2, seed, table: '0 0 0 0 0 0.95 1', color: dark, op: 0.8 });
        N({ freq: f(0.55 * scale), oct: 2, seed: seed + 5, table: '0 0 0 0 0 0 0.9', color: light, op: 0.7 });
        N({ freq: f(0.01 * scale), oct: 3, seed: seed + 2, table: '0 0.25 0 0.2', color: dark, op: 0.5 });
        break;
      }
      case 'marble': {
        N({ type: 'turbulence', freq: f(0.009 * scale), oct: 5, seed, table: '0.85 0.25 0 0 0 0 0 0 0 0', color: dark, op: 0.7 });
        N({ type: 'turbulence', freq: f(0.02 * scale), oct: 4, seed: seed + 3, table: '0.6 0 0 0 0 0 0', color: dark, op: 0.35 });
        break;
      }
      case 'glaze': {
        N({ freq: f(0.02 * scale), oct: 3, seed, table: '0 0.45 0 0.35 0', color: light, op: 0.45 });
        N({ freq: f(0.9 * scale), oct: 1, seed: seed + 1, table: '0 0 0 0 0.6', color: dark, op: 0.25 });
        break;
      }
      case 'clay': {
        N({ freq: f(0.04 * scale), oct: 4, seed, table: '0 0.3 0 0.25 0', color: light, op: 0.35 });
        N({ freq: f(0.8 * scale), oct: 2, seed: seed + 1, table: '0 0 0 0 0.7', color: dark, op: 0.5 });
        break;
      }
      case 'shell': {
        N({ freq: f(0.05 * scale), oct: 3, seed, table: '0 0 0 0 0 0.45 0.7', color: '#9B7A4E', op: 0.45 });
        N({ freq: f(0.012 * scale), oct: 2, seed: seed + 3, table: '0 0.3 0', color: dark, op: 0.35 });
        break;
      }
      case 'porous': {
        N({ freq: f(0.18 * scale), oct: 3, seed, table: '0 0 0 0 0 0.8 1', color: dark, op: 0.75 });
        N({ freq: f(0.02 * scale), oct: 3, seed: seed + 2, table: '0 0.3 0 0.25', color: light, op: 0.4 });
        break;
      }
      case 'seed': {
        N({ freq: f(0.03 * scale), oct: 4, seed, table: '0 0.45 0 0.5 0 0.3', color: dark, op: 0.6 });
        N({ freq: f(0.05 * scale), oct: 3, seed: seed + 2, table: '0 0 0.3 0', color: light, op: 0.4 });
        break;
      }
      case 'glass': {
        const gid = uid(u + 'r');
        defs.push(`<radialGradient id="${gid}" cx="${f(cx)}" cy="${f(cy + rad * 0.2)}" r="${f(rad * 1.1)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="${lighten(base, 0.25)}"/><stop offset="0.7" stop-color="${base}"/><stop offset="1" stop-color="${dark}"/></radialGradient>`);
        els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="url(#${gid})"/>`);
        let grid = '';
        const step = isC ? region.r / 5 : 32;
        for (let x = bx + step / 2; x < bx + bw; x += step) grid += `M${f(x)} ${by}V${by + bh}`;
        for (let y = by + step / 2; y < by + bh; y += step) grid += `M${bx} ${f(y)}H${bx + bw}`;
        els.push(`<path d="${grid}" stroke="${light}" stroke-width="${isC ? 1.4 : 1}" opacity="0.22"/>`);
        if (isC) {
          let nodes = '';
          for (let i = 0; i < 7; i++) {
            const a = R() * Math.PI * 2, rr = R() * region.r * 0.7;
            nodes += `<circle cx="${f(cx + Math.cos(a) * rr)}" cy="${f(cy + Math.sin(a) * rr)}" r="${f(2 + R() * 3)}" fill="${light}" opacity="0.7"/>`;
          }
          els.push(nodes);
        }
        break;
      }
      case 'kinetic': {
        // nanoweave: a tessellation of fine triangles, a few filled darker
        const pid = uid(u + 'k');
        const s2 = isC ? 26 : 34, h = s2 * 0.866;
        defs.push(`<pattern id="${pid}" width="${s2}" height="${f(h * 2)}" patternUnits="userSpaceOnUse" x="${f(cx)}" y="${f(cy)}">` +
          `<path d="M0 ${f(h)}L${s2 / 2} 0L${s2} ${f(h)}ZM${s2 / 2} ${f(h * 2)}L${s2} ${f(h)}L${s2 * 1.5} ${f(h * 2)}M${-s2 / 2} ${f(h * 2)}L0 ${f(h)}L${s2 / 2} ${f(h * 2)}" fill="none" stroke="${light}" stroke-width="1.1" opacity="0.38"/>` +
          `<path d="M0 ${f(h)}L${s2 / 2} 0L${s2} ${f(h)}Z" fill="#000" opacity="0.35"/></pattern>`);
        els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="url(#${pid})"/>`);
        N({ freq: f(0.012 * scale), oct: 2, seed, table: '0 0.3 0 0.25', color: light, op: 0.25 });
        break;
      }
      case 'gradient': {
        const gid = uid(u + 'v');
        defs.push(`<linearGradient id="${gid}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${light}"/><stop offset="1" stop-color="${dark}"/></linearGradient>`);
        els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="url(#${gid})"/>`);
        break;
      }
      case 'ceramic': {
        N({ freq: f(0.9 * scale), oct: 1, seed, table: '0 0 0 0.5', color: dark, op: 0.12 });
        break;
      }
      case 'graphene': {
        const pid = uid(u + 'h');
        const s = isC ? 12 : 18, h = s * Math.sqrt(3);
        defs.push(`<pattern id="${pid}" width="${f(s * 3)}" height="${f(h)}" patternUnits="userSpaceOnUse"><path d="M0 ${f(h / 2)}L${f(s / 2)} 0H${f(s * 1.5)}L${f(s * 2)} ${f(h / 2)}L${f(s * 1.5)} ${f(h)}H${f(s / 2)}ZM${f(s * 2)} ${f(h / 2)}H${f(s * 3)}" fill="none" stroke="${light}" stroke-width="1" opacity="0.35"/></pattern>`);
        els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="url(#${pid})"/>`);
        break;
      }
      case 'anodized': {
        const gid = uid(u + 'a');
        defs.push(`<linearGradient id="${gid}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${lighten(base, 0.3)}"/><stop offset="0.35" stop-color="${base}"/><stop offset="0.6" stop-color="${mix(base, '#B07ED8', 0.5)}"/><stop offset="0.85" stop-color="${mix(base, '#E2B85A', 0.45)}"/><stop offset="1" stop-color="${dark}"/></linearGradient>`);
        els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="url(#${gid})" opacity="0.9"/>`);
        N({ freq: `${f(0.002)} ${f(0.6)}`, oct: 1, seed, table: '0 0.3 0 0.2', color: light, op: 0.4 });
        break;
      }
      case 'solar': {
        const pid = uid(u + 's');
        const s = isC ? region.r / 3.2 : 44;
        defs.push(`<pattern id="${pid}" width="${f(s)}" height="${f(s)}" patternUnits="userSpaceOnUse" x="${f(cx)}" y="${f(cy)}"><rect x="1.5" y="1.5" width="${f(s - 3)}" height="${f(s - 3)}" fill="${dark}" opacity="0.55"/><path d="M${f(s / 2)} 1.5V${f(s - 1.5)}" stroke="${light}" stroke-width="0.8" opacity="0.45"/></pattern>`);
        els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="url(#${pid})"/>`);
        break;
      }
      case 'fleck': {
        N({ freq: f(0.5 * scale), oct: 2, seed, table: '0 0 0 0 0 0 0.9', color: '#2F9E6A', op: 0.7 });
        N({ freq: f(0.45 * scale), oct: 2, seed: seed + 11, table: '0 0 0 0 0 0 0.9', color: '#D4A23A', op: 0.7 });
        N({ freq: f(0.4 * scale), oct: 2, seed: seed + 23, table: '0 0 0 0 0 0 0.8', color: '#C24B3A', op: 0.6 });
        N({ freq: `${f(0.003)} ${f(0.4)}`, oct: 1, seed: seed + 3, table: '0 0.3 0 0.25', color: light, op: 0.35 });
        break;
      }
      case 'chrome': {
        const gid = uid(u + 'k');
        defs.push(`<linearGradient id="${gid}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFFFFF"/><stop offset="0.55" stop-color="${lighten(base, 0.35)}"/><stop offset="0.66" stop-color="${darken(base, 0.35)}"/><stop offset="0.74" stop-color="${darken(base, 0.1)}"/><stop offset="0.8" stop-color="${lighten(base, 0.4)}"/><stop offset="1" stop-color="${base}"/></linearGradient>`);
        els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="url(#${gid})"/>`);
        if (color) els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="${color}" opacity="0.4" style="mix-blend-mode:multiply"/>`);
        break;
      }
      case 'veined': {
        const vein = o.glow || color || '#E8A33A';
        els[0] = `<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="${m.base}"/>`;
        N({ freq: f(0.5 * scale), oct: 2, seed: seed + 1, table: '0 0 0 0 0.6', color: m.light, op: 0.6 });
        N({ type: 'turbulence', freq: f(0.012 * scale), oct: 3, seed, table: '1 0.15 0 0 0 0 0 0 0 0 0 0', color: vein, op: 0.6, blur: isC ? 5 : 7 });
        N({ type: 'turbulence', freq: f(0.012 * scale), oct: 3, seed, table: '1 0 0 0 0 0 0 0 0 0 0 0 0 0', color: lighten(vein, 0.5), op: 1 });
        break;
      }
      case 'iridescent':
      case 'prism': {
        const gid = uid(u + 'i'), fid = uid(u + 'x');
        const hues = m.family === 'prism'
          ? ['#F6B7D8', '#9ED6F7', '#B9F2C8', '#FBE59B', '#D4B4FA']
          : [lighten(base, 0.2), mix(base, '#3FC1C9', 0.6), mix(base, '#F06BA8', 0.5), mix(base, '#7B5CFF', 0.5), dark];
        defs.push(`<linearGradient id="${gid}" x1="0" y1="0" x2="1" y2="1">${hues.map((h, i) => `<stop offset="${f(i / (hues.length - 1))}" stop-color="${h}"/>`).join('')}</linearGradient>`);
        defs.push(`<filter id="${fid}" filterUnits="userSpaceOnUse" x="${bx}" y="${by}" width="${bw}" height="${bh}"><feTurbulence type="fractalNoise" baseFrequency="${f(0.008 * scale)}" numOctaves="2" seed="${seed}"/><feDisplacementMap in="SourceGraphic" scale="${f(rad * 0.9)}" xChannelSelector="R" yChannelSelector="B"/></filter>`);
        els.push(`<g filter="url(#${fid})"><rect x="${bx - rad * 0.5}" y="${by - rad * 0.5}" width="${bw + rad}" height="${bh + rad}" fill="url(#${gid})"/></g>`);
        if (color && m.family === 'iridescent') els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="${color}" opacity="0.55" style="mix-blend-mode:color"/>`);
        if (color && m.family === 'prism') els.push(`<rect x="${bx}" y="${by}" width="${bw}" height="${bh}" fill="${color}" opacity="0.62"/>`);
        break;
      }
      case 'lumen': {
        const glow = color || m.light;
        N({ freq: f(0.02 * scale), oct: 3, seed, table: '0 0.3 0 0.2', color: darken(glow, 0.6), op: 0.7 });
        let dots = '';
        const count = isC ? 26 : 160;
        for (let i = 0; i < count; i++) {
          const x = bx + R() * bw, y = by + R() * bh, r = 1 + R() * (isC ? 4 : 3);
          dots += `<circle cx="${f(x)}" cy="${f(y)}" r="${f(r * 2.6)}" fill="${glow}" opacity="0.12"/><circle cx="${f(x)}" cy="${f(y)}" r="${f(r)}" fill="${lighten(glow, 0.4)}" opacity="0.85"/>`;
        }
        els.push(dots);
        break;
      }
      default: break;
    }
    return { defs: defs.join(''), els: els.join('') };
  }

  // ------------------------------------------------------------ friezes
  // Tile generators for the board frame. c = palette array. Return {w, h, body}.
  const FRIEZES = {
    kente: (c) => ({ w: 72, h: 72, body:
      `<rect width="36" height="36" fill="${c[0]}"/>` +
      [0, 1, 2, 3, 4, 5].map((i) => `<rect y="${i * 6}" width="36" height="3" fill="${i % 2 ? c[2] : c[1]}"/>`).join('') +
      `<rect x="36" width="36" height="36" fill="${c[3]}"/>${[0, 1, 2].map((i) => `<rect x="${40 + i * 11}" y="0" width="5" height="36" fill="${c[1]}"/>`).join('')}` +
      `<rect y="36" width="36" height="36" fill="${c[3]}"/>${[0, 1, 2].map((i) => `<rect x="${4 + i * 11}" y="36" width="5" height="36" fill="${c[1]}"/>`).join('')}` +
      `<rect x="36" y="36" width="36" height="36" fill="${c[0]}"/>${[0, 1, 2, 3, 4, 5].map((i) => `<rect x="36" y="${36 + i * 6}" width="36" height="3" fill="${i % 2 ? c[2] : c[1]}"/>`).join('')}` +
      `<path d="M18 12L24 18L18 24L12 18ZM54 48L60 54L54 60L48 54Z" fill="${c[2]}"/>` }),
    meander: (c) => ({ w: 48, h: 48, body:
      `<rect width="48" height="48" fill="${c[0]}"/>` +
      `<path d="M0 8H32V40H16V24H24M48 8V0M40 0V48M0 40H8V16" fill="none" stroke="${c[1]}" stroke-width="5" stroke-linecap="square"/>` +
      `<path d="M0 8H32V40H16V24H24M40 0V48M0 40H8V16" fill="none" stroke="${c[2]}" stroke-width="1.5" opacity="0.8"/>` }),
    stele: (c) => ({ w: 64, h: 64, body:
      `<rect width="64" height="64" fill="${c[0]}"/>` +
      `<rect x="10" y="12" width="44" height="40" fill="${c[1]}"/><rect x="14" y="16" width="36" height="32" fill="${c[2]}"/>` +
      `<rect x="22" y="24" width="20" height="16" fill="${c[1]}"/>` +
      [12, 32, 52].map((x) => `<circle cx="${x}" cy="5" r="3.2" fill="${c[3]}"/><circle cx="${x}" cy="59" r="3.2" fill="${c[3]}"/>`).join('') }),
    zigzag: (c) => ({ w: 48, h: 48, body:
      `<rect width="48" height="48" fill="${c[0]}"/>` +
      [0, 16, 32].map((y) => `<path d="M-6 ${y + 14}L6 ${y + 2}L18 ${y + 14}L30 ${y + 2}L42 ${y + 14}L54 ${y + 2}" fill="none" stroke="${c[2]}" stroke-width="6" transform="translate(-1 -1.5)"/>` +
        `<path d="M-6 ${y + 14}L6 ${y + 2}L18 ${y + 14}L30 ${y + 2}L42 ${y + 14}L54 ${y + 2}" fill="none" stroke="${c[1]}" stroke-width="5"/>`).join('') }),
    rosette: (c) => ({ w: 64, h: 64, body:
      `<rect width="64" height="64" fill="${c[0]}"/>` +
      Array.from({ length: 8 }, (_, i) => `<ellipse cx="32" cy="20" rx="5" ry="11" fill="${c[1]}" transform="rotate(${i * 45} 32 32)"/>`).join('') +
      `<circle cx="32" cy="32" r="6" fill="${c[2]}"/>` +
      [[0, 0], [64, 0], [0, 64], [64, 64]].map(([x, y]) => `<circle cx="${x}" cy="${y}" r="6" fill="${c[3]}"/><circle cx="${x - 1.5}" cy="${y - 1.5}" r="2.2" fill="#fff" opacity="0.55"/>`).join('') }),
    lotus: (c) => ({ w: 64, h: 64, body:
      `<rect width="64" height="64" fill="${c[0]}"/>` +
      `<path d="M32 10C38 20 38 30 32 40C26 30 26 20 32 10ZM32 40C20 36 14 28 12 18C22 22 28 30 32 40ZM32 40C44 36 50 28 52 18C42 22 36 30 32 40Z" fill="${c[1]}"/>` +
      `<path d="M0 52Q8 46 16 52T32 52T48 52T64 52" fill="none" stroke="${c[2]}" stroke-width="3"/><path d="M0 59Q8 53 16 59T32 59T48 59T64 59" fill="none" stroke="${c[3]}" stroke-width="2"/>` }),
    triangles: (c) => ({ w: 48, h: 48, body:
      `<rect width="48" height="48" fill="${c[0]}"/>` +
      `<path d="M0 48L12 26L24 48ZM24 48L36 26L48 48Z" fill="${c[1]}"/><path d="M12 0L24 22L36 0Z" fill="${c[1]}" opacity="0.7"/>` +
      `<path d="M24 6V18M18 12H30" stroke="${c[2]}" stroke-width="2.5"/><circle cx="24" cy="12" r="2" fill="${c[3]}"/>` +
      `<path d="M4 44L12 30L20 44" fill="none" stroke="${c[3]}" stroke-width="1.2"/>` }),
    grid: (c) => ({ w: 48, h: 48, body:
      `<rect width="48" height="48" fill="${c[0]}"/>` +
      `<path d="M0 0.5H48M0.5 0V48" stroke="${c[1]}" stroke-width="1"/>` +
      `<path d="M8 24H20M28 24H31M36 24H40" stroke="${c[2]}" stroke-width="2.5" stroke-linecap="round"/><circle cx="0.5" cy="0.5" r="2" fill="${c[3]}"/>` }),
    filaments: (c) => ({ w: 120, h: 120, body:
      `<rect width="120" height="120" fill="${c[0]}"/>` +
      `<path d="M0 30C30 10 50 60 80 40S110 10 120 30M0 90C20 70 40 110 70 90S100 60 120 90M30 0C40 30 20 50 40 70S60 110 50 120" fill="none" stroke="${c[1]}" stroke-width="2"/>` +
      `<path d="M0 30C30 10 50 60 80 40S110 10 120 30" fill="none" stroke="${c[2]}" stroke-width="6" opacity="0.18"/>` +
      `<circle cx="80" cy="40" r="4" fill="${c[3]}"/><circle cx="40" cy="70" r="3" fill="${c[3]}"/><circle cx="70" cy="90" r="2.5" fill="${c[2]}"/>` }),
    stripes: (c) => ({ w: 40, h: 40, body:
      `<rect width="40" height="40" fill="${c[0]}"/><path d="M-10 10L10 -10M-10 50L50 -10M30 50L50 30" stroke="${c[1]}" stroke-width="10"/>` +
      `<path d="M-10 30L30 -10M10 50L50 10" stroke="${c[2]}" stroke-width="2"/>` }),
    circuit: (c) => ({ w: 72, h: 72, body:
      `<rect width="72" height="72" fill="${c[0]}"/>` +
      `<path d="M0 10H20L26 16V36M0 24H12V48L20 56H36M36 0V8L44 16H72M52 16V40L60 48V72M72 30H64M26 36H44L52 40M36 56V72" fill="none" stroke="${c[1]}" stroke-width="2.4"/>` +
      `<rect x="40" y="26" width="16" height="9" fill="${c[2]}"/><rect x="10" y="58" width="16" height="9" fill="${c[3]}"/>` +
      [[26, 36], [20, 56], [44, 16], [60, 48], [64, 30]].map(([x, y]) => `<circle cx="${x}" cy="${y}" r="3.4" fill="${c[0]}" stroke="${c[1]}" stroke-width="2"/>`).join('') }),
    crosses: (c) => ({ w: 64, h: 64, body:
      `<rect width="64" height="64" fill="${c[0]}"/>` +
      `<path d="M32 10V54M10 32H54" stroke="${c[1]}" stroke-width="4"/>` +
      [[32, 10], [32, 54], [10, 32], [54, 32]].map(([x, y]) => `<circle cx="${x}" cy="${y}" r="5" fill="none" stroke="${c[1]}" stroke-width="3"/>`).join('') +
      `<rect x="25" y="25" width="14" height="14" fill="${c[2]}" transform="rotate(45 32 32)"/><circle cx="32" cy="32" r="3" fill="${c[3]}"/>` }),
    salthex: (c) => ({ w: 60, h: 104, body:
      `<rect width="60" height="104" fill="${c[0]}"/>` +
      `<path d="M30 0L60 17V52L30 69L0 52V17ZM0 69V104M60 69V104M30 69V104" fill="${c[1]}" fill-opacity="0.35" stroke="${c[2]}" stroke-width="3"/>` +
      `<circle cx="30" cy="35" r="4" fill="${c[3]}" opacity="0.8"/>` }),
    plain: (c) => ({ w: 40, h: 40, body: `<rect width="40" height="40" fill="${c[0]}" opacity="0"/>` }),
    chevron: (c) => ({ w: 40, h: 40, body:
      `<rect width="40" height="40" fill="${c[0]}"/>` +
      `<path d="M0 30L20 10L40 30" fill="none" stroke="${c[1]}" stroke-width="7"/><path d="M0 30L20 10L40 30" fill="none" stroke="${c[2]}" stroke-width="1.5" transform="translate(0 6)"/>` }),
  };

  // ------------------------------------------------------------ themes
  // Board sprite is 1024 units; frame band 0-96, field 96-928, hole pitch 208.
  const BOARD = { size: 1024, inset: 8, band: 96, pitch: 208, holeR: 82 };
  const holeCenters = () => {
    const pts = [];
    for (let r = 0; r < 4; r++) for (let c = 0; c < 4; c++) pts.push([BOARD.band + BOARD.pitch * (c + 0.5), BOARD.band + BOARD.pitch * (r + 0.5)]);
    return pts;
  };

  const THEMES = [
    {
      id: 'akan-goldweight', name: 'Akan Goldweight', era: 'past',
      origin: 'Asante and Baoulé courts, 17th–19th century', kingdoms: ['Konga'],
      thesis: 'Wealth you can weigh. Brass goldweights on espresso wengé, framed by a strip of kente.',
      motif: 'Kente strip-weave border; adinkrahene (concentric circles) at each corner; brass inlay rings around every hole.',
      board: { frame: 'wenge', field: 'wenge', fieldTint: null, frieze: 'kente', fc: ['#1B4D2E', '#E2B23A', '#9C2A1E', '#20160F'], radius: 28, slot: 'inlay', ring: '#D9AE4E', corner: 'concentric' },
      pieceShape: 'square', glyphs: 'ornate', glyphMode: 'carved',
      pieces: { water: { mat: 'brass', inlay: '#2E5FA8' }, earth: { mat: 'brass', inlay: '#1F3B26' }, fire: { mat: 'brass', inlay: '#A42818' }, air: { mat: 'gold', inlay: '#F6E7B5' } },
      accent: '#E2B23A', danger: '#C2412D', light: '#FFF1C6',
      palette: [['Wengé', '#3D2C22'], ['Brass', '#B08A3E'], ['Kente gold', '#E2B23A'], ['Kente green', '#1B4D2E'], ['Kente red', '#9C2A1E']],
      notes: ['Every piece is the same brass so value reads as wealth; the element lives in an enamel inlay.', 'Air pieces in pure gold make the rarest-feeling element shine.', 'Kente colours carry meaning: gold for royalty, green for growth, red for sacrifice.'],
    },
    {
      id: 'kuba-raffia', name: 'Kuba Raffia', era: 'past',
      origin: 'Kuba Kingdom, Kasai, DR Congo, 17th century onward', kingdoms: ['Royaume Kongo'],
      thesis: 'A board woven, not carved. Raffia cloth in a camwood frame, cowries ringing each hole.',
      motif: 'Kuba labyrinth interlace, never repeating the same way twice; cowrie-bead hole rims like royal regalia.',
      board: { frame: 'camwood', field: 'raffia', frieze: 'meander', fc: ['#2A1A12', '#C9A56B', '#8F2F22', '#F0E2C4'], radius: 18, slot: 'beaded', ring: '#EFE6D2', corner: 'diamond' },
      pieceShape: 'coin', pieceRim: 'copper', glyphs: 'ornate', glyphMode: 'carved',
      pieces: { water: { mat: 'leather_indigo', inlay: '#EFE6D2' }, earth: { mat: 'malachite', inlay: '#EFE6D2' }, fire: { mat: 'camwood', inlay: '#EFE6D2' }, air: { mat: 'cowrie', inlay: '#7A4A1E' } },
      accent: '#E9C46A', danger: '#B23A2B', light: '#FFF4D9',
      palette: [['Raffia', '#C9A56B'], ['Camwood', '#8F2F22'], ['Malachite', '#1E7A55'], ['Indigo', '#25335A'], ['Cowrie', '#EFE6D2']],
      notes: ['Mixed materials: each element is a different substance from the Kuba treasury.', 'Malachite from neighbouring Katanga gives Earth its green-black depth.', 'Kuba design values breaking the pattern on purpose; the frieze can shift per board.'],
    },
    {
      id: 'aksum-stele', name: 'Aksum Stele', era: 'past',
      origin: 'Aksumite Empire and Lalibela, Ethiopia, 1st–13th century', kingdoms: ['Telalila', 'Aradas'],
      thesis: 'Carved from one rock. Granite stepped like the stelae, river stones tumbled by the Blue Nile.',
      motif: 'False windows and monkey-head beam ends of the Aksum stelae; stepped, rock-hewn holes; silver cross lattice.',
      board: { frame: 'granite', field: 'granite', fieldTint: '#7B6F69', frieze: 'stele', fc: ['#4E4542', '#7B6F69', '#2E2826', '#B9BCC0'], radius: 6, slot: 'stepped', ring: '#B9BCC0', corner: 'cross' },
      pieceShape: 'pebble', glyphs: 'ornate', glyphMode: 'painted',
      pieces: { water: { mat: 'river_stone', color: '#4F6A80', inlay: '#E3E6EA' }, earth: { mat: 'river_stone', color: '#2F3134', inlay: '#E3E6EA' }, fire: { mat: 'sandstone', color: '#A2492E', inlay: '#F4EDE4' }, air: { mat: 'marble', inlay: '#8C9198' } },
      accent: '#C9CED4', danger: '#B5452E', light: '#F4F4F2',
      palette: [['Granite', '#6E625E'], ['Basalt', '#2F3134'], ['Silver', '#B9BCC0'], ['Red tuff', '#A2492E'], ['Marble', '#E6E1D8']],
      notes: ['A stone set: weight and silence. Pieces click into stepped holes.', 'Silver inlay glyphs echo Ethiopian processional crosses.', 'The stepped hole profile is unique to this theme and reads well at small sizes.'],
    },
    {
      id: 'imigongo-relief', name: 'Imigongo Relief', era: 'past',
      origin: 'Kingdom of Rwanda, Gisaka, 18th century onward', kingdoms: ['ILAGIK'],
      thesis: 'Four pigments, no more. Raised spirals and zigzags in ash black, kaolin white and red earth.',
      motif: 'Imigongo cow-dung relief: raised ridges, zigzags and concentric diamonds; holes ringed by a sculpted ridge.',
      board: { frame: 'imigongo_clay', field: 'imigongo_clay', frieze: 'zigzag', fc: ['#1E1A19', '#E9E3D7', '#000000', '#9E3B22'], radius: 14, slot: 'relief', ring: '#E9E3D7', corner: 'diamond', fieldMotif: 'diamonds' },
      pieceShape: 'square', glyphs: 'ornate', glyphMode: 'carved',
      pieces: { water: { mat: 'terracotta', color: '#5E6A72', inlay: '#E9E3D7' }, earth: { mat: 'imigongo_clay', inlay: '#E9E3D7' }, fire: { mat: 'terracotta', color: '#9E3B22', inlay: '#E9E3D7' }, air: { mat: 'terracotta', color: '#E2D8C6', inlay: '#1E1A19' } },
      accent: '#E9E3D7', danger: '#C4532F', light: '#FFFFFF',
      palette: [['Ash black', '#1E1A19'], ['Kaolin', '#E9E3D7'], ['Red earth', '#9E3B22'], ['River grey', '#5E6A72']],
      notes: ['The constraint is the style: one material, four natural pigments.', 'Elements are separated by value as well as hue, so the board stays readable in greyscale.', 'Tie-in: Prince Kakira of Gisaka is credited with the first imigongo.'],
    },
    {
      id: 'swahili-bao', name: 'Swahili Bao', era: 'past',
      origin: 'Kilwa, Lamu and Zanzibar, 12th–19th century', kingdoms: ['Telalila'],
      thesis: 'Played under a carved door. Mangrove-teak bowls, brass studs, counters from the sea.',
      motif: 'Zanzibar door rosettes and brass bosses; holes carved as smooth bao bowls; coral, tanzanite and cowrie pieces.',
      board: { frame: 'mangrove', field: 'iroko', frieze: 'rosette', fc: ['#3D2617', '#B9834F', '#E2C578', '#C9A24A'], radius: 40, slot: 'bowl', ring: '#C9A24A', corner: 'stud' },
      pieceShape: 'tumbled', glyphs: 'ornate', glyphMode: 'carved',
      pieces: { water: { mat: 'tanzanite', inlay: '#F2EBDD' }, earth: { mat: 'mkomwe_seed', inlay: '#E2C578' }, fire: { mat: 'coral_stone', color: '#B4472E', inlay: '#F2EBDD' }, air: { mat: 'cowrie', inlay: '#8A6A3E' } },
      accent: '#E2C578', danger: '#BF3F2A', light: '#FFF6E3',
      palette: [['Mangrove', '#5E3A24'], ['Iroko', '#9A6A37'], ['Brass boss', '#C9A24A'], ['Tanzanite', '#3A4BA6'], ['Coral', '#B4472E']],
      notes: ['Bao is East Africa\'s own board game; its bowls give this theme the most tactile holes.', 'Mkomwe seeds are the real bao counters: Earth pieces are literally from the game\'s ancestor.', 'Brass bosses come from Zanzibari doors, built to stop war elephants.'],
    },
    {
      id: 'meroe-faience', name: 'Meroë Faience', era: 'past',
      origin: 'Kingdom of Kush, Nubia, 800 BCE – 350 CE', kingdoms: ['Logone', 'Milu'],
      thesis: 'Kandake gold on pyramid sandstone. Glazed faience, carnelian and Nile blue.',
      motif: 'Lotus and Nile-wave frieze; gold inlay rings; faience and carnelian like Kushite jewellery.',
      board: { frame: 'sandstone', field: 'sandstone', fieldTint: '#C99A68', frieze: 'lotus', fc: ['#7E4F27', '#2C9C9A', '#E2B23A', '#F0D9B5'], radius: 10, slot: 'inlay', ring: '#D9AE4E', corner: 'concentric' },
      pieceShape: 'hex', glyphs: 'ornate', glyphMode: 'gilded',
      pieces: { water: { mat: 'faience', inlay: '#F6E7B5' }, earth: { mat: 'granite', color: '#2D2B2A', inlay: '#E2B23A' }, fire: { mat: 'carnelian', inlay: '#F6E7B5' }, air: { mat: 'gold', inlay: '#7A5414' } },
      accent: '#E2B23A', danger: '#B23A20', light: '#FFF3D6',
      palette: [['Sandstone', '#C08A57'], ['Faience', '#2C9C9A'], ['Carnelian', '#B0401E'], ['Kush gold', '#C9962E'], ['Black granite', '#2D2B2A']],
      notes: ['Kush was ruled by Kandakes, warrior queens; a fit for Milu\'s queen.', 'Faience Water is the most saturated colour on any past board: it pops against sandstone.', 'Gold Air pieces keep the element\'s amber family while feeling regal.'],
    },
    {
      id: 'agadez-silver', name: 'Agadez Silver', era: 'past',
      origin: 'Tuareg (Kel Tamasheq) of Aïr and the Sahel', kingdoms: ['Mask of Light'],
      thesis: 'Indigo leather, tooled and stitched. Silver crosses that catch the last light before sunset.',
      motif: 'Tooled triangles and Agadez cross pendants; silver-bead hole rims; stitched leather edges.',
      board: { frame: 'leather_indigo', field: 'leather_indigo', fieldTint: '#2C3B66', frieze: 'triangles', fc: ['#1A2444', '#3E5287', '#D9DCE0', '#E8C27A'], radius: 34, slot: 'beaded', ring: '#D9DCE0', corner: 'cross', stitch: true },
      pieceShape: 'tall', pieceRim: 'silver', glyphs: 'ornate', glyphMode: 'carved',
      pieces: { water: { mat: 'leather_indigo', color: '#2D4A8A', inlay: '#E6E8EB' }, earth: { mat: 'ebony', inlay: '#E6E8EB' }, fire: { mat: 'carnelian', inlay: '#F2F2F2' }, air: { mat: 'silver', inlay: '#B07A2A' } },
      accent: '#E8C27A', danger: '#C5462F', light: '#F3F4F6',
      palette: [['Indigo', '#25335A'], ['Silver', '#B9BCC0'], ['Henna', '#B07A2A'], ['Carnelian', '#B0401E'], ['Ebony', '#2B201C']],
      notes: ['Mask of Light\'s glowing henna becomes the amber accent used for legal moves.', 'Leather boards are soft-edged; the stitch line doubles as the turn-highlight path.', 'Silver Air pieces reflect the board, a nice "hidden" feel for invisibility powers.'],
    },
    {
      id: 'nevulandis-data', name: 'N\'evulandis Data Court', era: 'future',
      origin: 'House of IO\'s, 30th century', kingdoms: ['N\'evulandis'],
      thesis: 'A board that keeps the ledger. White nano-ceramic, hairline light, pieces of stored Data.',
      motif: 'Hairline data grid with Lota dashes; flush light sockets with four index ticks; glass discs with an inner lattice.',
      board: { frame: 'nano_ceramic', field: 'nano_ceramic', frieze: 'grid', fc: ['#E4E7E8', '#C9CFD1', '#2BB6E0', '#2BB6E0'], radius: 48, slot: 'socket', ring: '#2BB6E0', corner: 'bracket' },
      pieceShape: 'vial', glyphs: 'minimal', glyphMode: 'emissive',
      pieces: { water: { mat: 'data_glass', color: '#1E62A8', inlay: '#BFE9FF' }, earth: { mat: 'data_glass', color: '#2E3438', inlay: '#E6ECEF' }, fire: { mat: 'data_glass', color: '#A8321F', inlay: '#FFD2C4' }, air: { mat: 'data_glass', color: '#C98E22', inlay: '#FFF0C4' } },
      accent: '#2BB6E0', danger: '#E0533A', light: '#E6FAFF',
      palette: [['Ceramic', '#ECEEEE'], ['Hairline', '#C9CFD1'], ['IO cyan', '#2BB6E0'], ['Data blue', '#1E62A8'], ['Graphite', '#2E3438']],
      notes: ['The only light-surface board: test it in bright rooms and against dark UI.', 'Glyphs glow instead of being carved, so they read even when pieces overlap.', 'House values Peace, Information, Loyalty, Collaboration map to quiet, flat surfaces.'],
    },
    {
      id: 'ubunifu-dreamcraft', name: 'Ubunifu Dreamcraft', era: 'future',
      origin: 'Elite council of Ubunifu, 22nd century', kingdoms: ['Ubunifu'],
      thesis: 'Thoughts made visible. Graphene boards threaded with neural light, pieces of Ndoto glass.',
      motif: 'Glowing neural filaments; portal holes ringed with soft light; iridescent glass that shifts with the holder.',
      board: { frame: 'graphene', field: 'graphene', frieze: 'filaments', fc: ['#16181B', '#7B6CF0', '#62F2C7', '#F06BA8'], radius: 60, slot: 'portal', ring: '#9C8CFF', corner: 'bracket' },
      pieceShape: 'sphere', glyphs: 'minimal', glyphMode: 'emissive',
      pieces: { water: { mat: 'dream_glass', color: '#2C6BD8', inlay: '#CFE2FF' }, earth: { mat: 'dream_glass', color: '#3A3346', inlay: '#E8E0FF' }, fire: { mat: 'dream_glass', color: '#D2402A', inlay: '#FFD9CF' }, air: { mat: 'dream_glass', color: '#E0A830', inlay: '#FFF2C9' } },
      accent: '#9C8CFF', danger: '#FF6B57', light: '#EDE8FF',
      palette: [['Graphene', '#26292C'], ['Neural violet', '#7B6CF0'], ['Synapse mint', '#62F2C7'], ['Ndoto pink', '#F06BA8']],
      notes: ['Magical-material direction: pieces are tinted glass over a shifting iridescent core.', '"Juju men" who read thoughts suggest pieces that react: brighten on hover, dim when hidden.', 'Rounded, organic geometry contrasts with N\'evulandis\' precision.'],
    },
    {
      id: 'eko-2090', name: 'Eko 2090', era: 'future',
      origin: 'Lagos lagoon megacity, 2090', kingdoms: ['Eko Akete'],
      thesis: 'The lagoon city, rebuilt from its own scrap. Danfo yellow, reclaimed alloy, solar glass.',
      motif: 'Danfo-bus hazard stripes; magnetic dock holes with segmented rings; reclaimed alloy flecked with old circuit boards.',
      board: { frame: 'ewaste_alloy', field: 'graphene', fieldTint: '#1F2427', frieze: 'stripes', fc: ['#1C1F21', '#F2C200', '#0F1112', '#F2C200'], radius: 22, slot: 'dock', ring: '#F2C200', corner: 'bracket' },
      pieceShape: 'tall', pieceRim: 'ewaste_alloy', glyphs: 'minimal', glyphMode: 'emissive',
      pieces: { water: { mat: 'solar_glass', color: '#1E4E9A', inlay: '#CFE6FF' }, earth: { mat: 'ewaste_alloy', inlay: '#F2C200' }, fire: { mat: 'anodized', color: '#B53A26', inlay: '#FFE0D6' }, air: { mat: 'liquid_chrome', color: '#E3B44A', inlay: '#3A2A08' } },
      accent: '#F2C200', danger: '#FF5A3C', light: '#FFF6CC',
      palette: [['Danfo yellow', '#F2C200'], ['Asphalt', '#1C1F21'], ['Reclaimed alloy', '#6E7376'], ['Solar blue', '#1E4E9A'], ['Heat red', '#B53A26']],
      notes: ['Eko is the old name of Lagos; UMUNNA governance suggests a crowd-built aesthetic.', 'Every material is reused or harvested: the future is repaired, not pristine.', 'Highest-contrast theme: good default for accessibility and phone screens in sunlight.'],
    },
    {
      id: 'volta-resonance', name: 'Volta Resonance', era: 'future',
      origin: 'Empire Volta, Nakomean warrior council', kingdoms: ['Empire Volta'],
      thesis: 'Stones of Power. Meteoric ore that answers to will, veins lit with the element inside.',
      motif: 'Volta stone slabs with branching light veins; portal holes; each piece glows from inside with its element.',
      board: { frame: 'volta_stone', field: 'volta_stone', frieze: 'chevron', fc: ['#141218', '#2A2632', '#E8A33A', '#E8A33A'], radius: 12, slot: 'portal', ring: '#E8A33A', corner: 'bracket', glow: '#E8A33A' },
      pieceShape: 'tumbled', glyphs: 'ornate', glyphMode: 'emissive',
      pieces: { water: { mat: 'volta_stone', glow: '#3E9BFF', inlay: '#BFE0FF' }, earth: { mat: 'volta_stone', glow: '#8FBF6A', inlay: '#E2F5D2' }, fire: { mat: 'volta_stone', glow: '#FF5A2A', inlay: '#FFD4C2' }, air: { mat: 'volta_stone', glow: '#FFC23A', inlay: '#FFF1C4' } },
      accent: '#E8A33A', danger: '#FF4D3A', light: '#FFF0D2',
      palette: [['Volta stone', '#1D1B22'], ['Ember vein', '#E8A33A'], ['Tide vein', '#3E9BFF'], ['Flare vein', '#FF5A2A'], ['Moss vein', '#8FBF6A']],
      notes: ['Our fictional answer to a "vibranium": a resonant meteoric ore, owned by Busara\'s own lore.', 'Same stone for every piece; the element is the colour of the light inside it.', 'Earth glows moss green here because a charcoal glow would disappear; hue family stays dark-natural.'],
    },
    {
      id: 'kente-circuit', name: 'Kente Circuit', era: 'fusion',
      origin: 'Asante weaving meets Accra hardware labs', kingdoms: ['Konga', 'Eko Akete'],
      thesis: 'The loom was the first computer. Kente blocks become circuit traces on dark anodised metal.',
      motif: 'Kente blocks redrawn as copper traces and pads; inlay rings that light up; brass pieces with glowing glyphs.',
      board: { frame: 'graphene', field: 'graphene', fieldTint: '#1A2420', frieze: 'circuit', fc: ['#142019', '#C9963A', '#1B6B45', '#B5372A'], radius: 20, slot: 'inlay', ring: '#E2B23A', corner: 'concentric', fieldMotif: 'circuit' },
      pieceShape: 'hex', glyphs: 'ornate', glyphMode: 'emissive',
      pieces: { water: { mat: 'brass', inlay: '#5FB4FF' }, earth: { mat: 'bronze', inlay: '#7CE0A0' }, fire: { mat: 'copper', inlay: '#FF7A52' }, air: { mat: 'gold', inlay: '#FFF0B0' } },
      accent: '#E2B23A', danger: '#FF5A3C', light: '#FFF1C6',
      palette: [['PCB green', '#142019'], ['Trace copper', '#C9963A'], ['Kente green', '#1B6B45'], ['Kente red', '#B5372A'], ['Gold', '#C9962E']],
      notes: ['Past metals, future light: ornate glyphs rendered as emissive lines.', 'Pairs with Akan Goldweight as a "same kingdom, 300 years later" unlock.', 'Kente\'s strip logic (blocks, repeats, meaning by colour) maps cleanly to traces.'],
    },
    {
      id: 'lalibela-orbital', name: 'Lalibela Orbital', era: 'fusion',
      origin: 'Rock-hewn Lalibela reimagined as a station in orbit', kingdoms: ['Telalila', 'N\'evulandis'],
      thesis: 'Carve down into rock, look up into orbit. Basalt crosses ringed with station light.',
      motif: 'Bete Giyorgis cross plan as the frame tile; stepped holes with a ring of light; river stones with glowing glyphs.',
      board: { frame: 'river_stone', field: 'river_stone', fieldTint: '#3E4447', frieze: 'crosses', fc: ['#2C3134', '#A8B2B8', '#5FD0E8', '#5FD0E8'], radius: 16, slot: 'stepped', ring: '#5FD0E8', corner: 'cross', glowRing: true },
      pieceShape: 'pebble', glyphs: 'ornate', glyphMode: 'emissive',
      pieces: { water: { mat: 'river_stone', color: '#3D566B', inlay: '#7FD8FF' }, earth: { mat: 'river_stone', color: '#2A2C2E', inlay: '#D6E7EC' }, fire: { mat: 'river_stone', color: '#7A3A2A', inlay: '#FF9A7A' }, air: { mat: 'marble', inlay: '#E3A93A' } },
      accent: '#5FD0E8', danger: '#FF6A4D', light: '#E0F8FF',
      palette: [['Basalt', '#3E4447'], ['Station cyan', '#5FD0E8'], ['Cross grey', '#A8B2B8'], ['Ember', '#FF9A7A'], ['Marble', '#E6E1D8']],
      notes: ['Fusion through contrast: heavy stone, weightless light.', 'Stepped holes reuse the Aksum profile, linking the two Ethiopian themes.', 'A good candidate for an animated "turn begins" glow travelling around the cross tiles.'],
    },
    {
      id: 'danakil-prism', name: 'Danakil Prism', era: 'fusion',
      origin: 'Afar and the Danakil Depression, cradle of humanity', kingdoms: ['Aradas'],
      thesis: 'Where humans began, and where identity dissolves. Salt crust, sulphur colour, crystals that never hold one hue.',
      motif: 'Hexagonal salt polygons; sulphur-yellow and acid-green accents; shift-prism pieces whose tint drifts.',
      board: { frame: 'marble', field: 'nano_ceramic', fieldTint: '#EEE9DC', frieze: 'salthex', fc: ['#E9E2CF', '#D8E36A', '#C7B98E', '#4FBF8E'], radius: 30, slot: 'socket', ring: '#B8C94A', corner: 'bracket' },
      pieceShape: 'faceted', glyphs: 'minimal', glyphMode: 'carved',
      pieces: { water: { mat: 'shift_prism', color: '#3A79C9', inlay: '#1E2B4A' }, earth: { mat: 'shift_prism', color: '#3B3A40', inlay: '#F2EFE6' }, fire: { mat: 'shift_prism', color: '#C9452C', inlay: '#3A1208' }, air: { mat: 'shift_prism', color: '#E2B83A', inlay: '#3A2A08' } },
      accent: '#B8C94A', danger: '#D9482F', light: '#FBFFE0',
      palette: [['Salt crust', '#E9E2CF'], ['Sulphur', '#D8E36A'], ['Acid green', '#4FBF8E'], ['Brine', '#C7B98E'], ['Prism', '#8A7BD1']],
      notes: ['Aradas surf identities; pieces drift through colour while keeping their element tint.', 'Ancient and future at once: Lucy was found a few hundred kilometres away.', 'Light board with minimal carved glyphs: test against the N\'evulandis board for distinction.'],
    },
    {
      id: 'panther-court', name: 'Panther Court', era: 'fusion',
      origin: 'A hidden Afrofuturist kingdom: royal ritual, nanotech and kinetic ore', kingdoms: ['Empire Volta', 'N\'evulandis'],
      thesis: 'The war room of a hidden kingdom. Slate boards rimmed in gold, iridescent steel frames, stones that store every blow.',
      motif: 'Folder-tab slate boards with gold hairlines and corner triangles; glowing orange-to-purple triangle friezes; carved dark wood; kinetic beads etched with script.',
      board: { layout: 'tabbed', frame: 'graphene', field: 'graphene', frieze: 'triangles', fc: ['#2B2B40', '#E9A340', '#9B5CFF', '#E9A340'], radius: 34, slot: 'rim', ring: '#B98543', corner: 'triangle', glowColor: '#F2A33A' },
      glyphs: 'angular', glyphMode: 'emissive', pieceStyle: 'kinetic', uiKit: true,
      pieces: { water: { mat: 'kinetic_bead', glow: '#3E8BFF', inlay: '#CFE2FF' }, earth: { mat: 'kinetic_ore', glow: '#B07CFF', inlay: '#EFE4FF' }, fire: { mat: 'kinetic_bead', glow: '#FF5A2E', inlay: '#FFD9CC' }, air: { mat: 'kinetic_bead', glow: '#FFB830', inlay: '#FFF0C8' } },
      accent: '#A66BFF', danger: '#FF5A4A', light: '#EBDDFF',
      palette: [['Slate', '#2E2E44'], ['Royal gold', '#E9A340'], ['Kinetic purple', '#9B5CFF'], ['Iridescent steel', '#5B5F86'], ['Ember', '#FF8A1F']],
      notes: ['Board, holes and interface follow the reference mockups: slate, gold hairlines, iridescent steel and neon triangle friezes.', 'Stones are kinetic beads: a black triangle-weave shell, a gunmetal bezel etched with script, and a charge ring that glows in the element colour.', 'Earth is raw kinetic ore with violet seams, so it stays dark while reading clearly against blue Water.'],
    },
  ];
  // The original Busara look, converted to the same sprite format and board geometry as the
  // other themes. Pieces are the existing resource icons (raster), resampled by the exporter.
  const CLASSIC = {
    id: 'classic', name: 'Busara Classic', era: 'classic',
    origin: 'The original Busara board and resource icons', kingdoms: [],
    thesis: 'The first Busara look: slate board, amber hairlines, round enamel resources.',
    motif: 'Slate field, amber outline and hairlines, darkened corner triangles, diamond marks at each edge.',
    board: { frame: 'slate', field: 'slate', frieze: 'plain', fc: ['#4C514B', '#4C514B', '#F0A500', '#F0A500'], radius: 40, slot: 'inlay', ring: '#F0A500', corner: 'triangle', edgeMarks: true, hairlines: true, plainField: true, outline: '#F0A500', holeColor: '#353535', ringWidth: 3.5 },
    glyphs: 'minimal', glyphMode: 'carved',
    pieces: { water: { mat: 'slate', inlay: '#F0A500' }, earth: { mat: 'slate', inlay: '#F0A500' }, fire: { mat: 'slate', inlay: '#F0A500' }, air: { mat: 'slate', inlay: '#F0A500' } },
    raster: { water: 'Elements/element_water.png', earth: 'Elements/element_earth.png', fire: 'Elements/element_fire.png', air: 'Elements/element_air.png' },
    accent: '#F0A500', danger: '#E5484D', light: '#FFE7A8',
    palette: [['Slate', '#4C514B'], ['Amber', '#F0A500'], ['Hole', '#353535'], ['Fire', '#B8321F'], ['Water', '#2F6DB5']],
    notes: ['Default for players who keep the original look.'],
  };
  const ALL_THEMES = THEMES.concat([CLASSIC]);
  const themeById = (id) => ALL_THEMES.find((t) => t.id === id) || THEMES[0];

  // ------------------------------------------------------------ slots / holes
  const SLOT_STYLES = {
    bowl: { name: 'Carved bowl', era: 'past', note: 'Concave, smooth: the bao bowl. Pieces sit low and shadowed.' },
    inlay: { name: 'Inlay ring', era: 'past', note: 'Flat recess ringed with a strip of metal; formal, courtly.' },
    beaded: { name: 'Beaded rim', era: 'past', note: 'A ring of cowries or silver beads; reads as regalia.' },
    stepped: { name: 'Stepped cut', era: 'past', note: 'Two hewn terraces like the Aksum stelae.' },
    relief: { name: 'Raised ridge', era: 'past', note: 'A sculpted ridge that stands up from the surface.' },
    socket: { name: 'Light socket', era: 'future', note: 'Flush glass with four index ticks and a hairline of light.' },
    dock: { name: 'Mag dock', era: 'future', note: 'Segmented ring that clicks a piece into place.' },
    rim: { name: 'Gold rim', era: 'future', note: 'A deep slate recess ringed by a thin bronze-gold rim.' },
    portal: { name: 'Portal', era: 'future', note: 'Light pools at the edge; the hole looks deeper than the board.' },
  };

  function holeMarkup(u, style, cx, cy, r, t, surface) {
    const ring = t.board.ring;
    const sDark = darken(surface, 0.6), sLight = lighten(surface, 0.35);
    const id = uid(u + 'h');
    const out = [], defs = [];
    switch (style) {
      case 'bowl': {
        defs.push(`<radialGradient id="${id}" cx="0.6" cy="0.68" r="0.75"><stop offset="0" stop-color="${darken(surface, 0.15)}"/><stop offset="0.55" stop-color="${darken(surface, 0.4)}"/><stop offset="1" stop-color="${darken(surface, 0.78)}"/></radialGradient>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 5}" fill="none" stroke="${sLight}" stroke-width="4" opacity="0.6"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="url(#${id})"/>`);
        out.push(`<path d="M${f(cx - r * 0.7)} ${f(cy + r * 0.72)}A${r} ${r} 0 0 0 ${f(cx + r * 0.72)} ${f(cy - r * 0.7)}" fill="none" stroke="${sLight}" stroke-width="3" opacity="0.45"/>`);
        break;
      }
      case 'inlay': {
        defs.push(`<linearGradient id="${id}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${lighten(ring, 0.5)}"/><stop offset="0.5" stop-color="${ring}"/><stop offset="1" stop-color="${darken(ring, 0.45)}"/></linearGradient>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="${t.board.holeColor || darken(surface, 0.62)}"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r - 6}" fill="none" stroke="#000" stroke-width="6" opacity="0.25"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 2}" fill="none" stroke="url(#${id})" stroke-width="${t.board.ringWidth || 7}"/>`);
        if (t.board.glowRing || t.era === 'fusion') out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 2}" fill="none" stroke="${ring}" stroke-width="14" opacity="0.18"/>`);
        break;
      }
      case 'beaded': {
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="${darken(surface, 0.6)}"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r - 4}" fill="none" stroke="#000" stroke-width="8" opacity="0.25"/>`);
        const n = 22;
        for (let i = 0; i < n; i++) {
          const a = (i / n) * Math.PI * 2;
          const x = cx + Math.cos(a) * (r + 7), y = cy + Math.sin(a) * (r + 7);
          out.push(`<ellipse cx="${f(x)}" cy="${f(y)}" rx="6.5" ry="8.5" transform="rotate(${f(a * 57.3)} ${f(x)} ${f(y)})" fill="${ring}"/><circle cx="${f(x - 2)}" cy="${f(y - 2.5)}" r="2.4" fill="#fff" opacity="0.6"/><ellipse cx="${f(x)}" cy="${f(y)}" rx="6.5" ry="8.5" transform="rotate(${f(a * 57.3)} ${f(x)} ${f(y)})" fill="none" stroke="${darken(ring, 0.5)}" stroke-width="1"/>`);
        }
        break;
      }
      case 'stepped': {
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 9}" fill="${darken(surface, 0.22)}"/>`);
        out.push(`<path d="M${f(cx - (r + 9) * 0.707)} ${f(cy + (r + 9) * 0.707)}A${r + 9} ${r + 9} 0 0 1 ${f(cx + (r + 9) * 0.707)} ${f(cy - (r + 9) * 0.707)}" fill="none" stroke="${sDark}" stroke-width="3" opacity="0.6"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r - 4}" fill="${darken(surface, 0.62)}"/>`);
        out.push(`<path d="M${f(cx - (r - 4) * 0.707)} ${f(cy + (r - 4) * 0.707)}A${r - 4} ${r - 4} 0 0 0 ${f(cx + (r - 4) * 0.707)} ${f(cy - (r - 4) * 0.707)}" fill="none" stroke="${sLight}" stroke-width="3" opacity="0.4"/>`);
        if (t.board.glowRing) out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 9}" fill="none" stroke="${ring}" stroke-width="2.5"/><circle cx="${cx}" cy="${cy}" r="${r + 9}" fill="none" stroke="${ring}" stroke-width="12" opacity="0.18"/>`);
        break;
      }
      case 'relief': {
        defs.push(`<linearGradient id="${id}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${lighten(ring, 0.2)}"/><stop offset="1" stop-color="${darken(ring, 0.55)}"/></linearGradient>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 10}" fill="none" stroke="#000" stroke-width="16" opacity="0.3" transform="translate(4 5)"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 8}" fill="none" stroke="url(#${id})" stroke-width="14"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="${darken(surface, 0.55)}"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r - 18}" fill="none" stroke="${ring}" stroke-width="2" opacity="0.18"/>`);
        break;
      }
      case 'socket': {
        const deep = luminance(surface) > 0.5 ? darken(surface, 0.12) : darken(surface, 0.5);
        defs.push(`<radialGradient id="${id}" cx="0.5" cy="0.4" r="0.6"><stop offset="0" stop-color="${lighten(deep, 0.06)}"/><stop offset="1" stop-color="${darken(deep, 0.25)}"/></radialGradient>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="url(#${id})"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="none" stroke="${ring}" stroke-width="2.2"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="none" stroke="${ring}" stroke-width="10" opacity="0.12"/>`);
        [0, 90, 180, 270].forEach((a) => {
          const rad = (a * Math.PI) / 180;
          out.push(`<path d="M${f(cx + Math.cos(rad) * (r + 8))} ${f(cy + Math.sin(rad) * (r + 8))}L${f(cx + Math.cos(rad) * (r + 18))} ${f(cy + Math.sin(rad) * (r + 18))}" stroke="${ring}" stroke-width="2.4" stroke-linecap="round"/>`);
        });
        break;
      }
      case 'dock': {
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 6}" fill="${darken(surface, 0.35)}"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r - 2}" fill="${darken(surface, 0.6)}"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 1}" fill="none" stroke="${ring}" stroke-width="5" stroke-dasharray="${f((2 * Math.PI * (r + 1)) / 16 - 6)} 6"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="5" fill="${ring}" opacity="0.7"/>`);
        out.push(`<path d="M${cx - 12} ${cy}H${cx - 26}M${cx + 12} ${cy}H${cx + 26}" stroke="${ring}" stroke-width="2" opacity="0.5"/>`);
        break;
      }
      case 'rim': {
        defs.push(`<radialGradient id="${id}" cx="0.42" cy="0.38" r="0.7"><stop offset="0" stop-color="#21202E"/><stop offset="1" stop-color="#111018"/></radialGradient>`);
        out.push(`<circle cx="${cx}" cy="${cy + 3}" r="${r + 6}" fill="#000" opacity="0.35"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="url(#${id})"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r - 5}" fill="none" stroke="#000" stroke-width="8" opacity="0.45"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 1}" fill="none" stroke="${ring}" stroke-width="4.5"/>`);
        out.push(`<path d="M${f(cx + (r + 1) * 0.94)} ${f(cy - (r + 1) * 0.34)}A${r + 1} ${r + 1} 0 0 1 ${f(cx - (r + 1) * 0.34)} ${f(cy + (r + 1) * 0.94)}" fill="none" stroke="${lighten(ring, 0.45)}" stroke-width="2" opacity="0.6"/>`);
        break;
      }
      case 'portal': {
        defs.push(`<radialGradient id="${id}"><stop offset="0" stop-color="#000" stop-opacity="0.95"/><stop offset="0.7" stop-color="${darken(ring, 0.8)}"/><stop offset="0.93" stop-color="${darken(ring, 0.15)}"/><stop offset="1" stop-color="${lighten(ring, 0.3)}"/></radialGradient>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r + 10}" fill="${ring}" opacity="0.14"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="url(#${id})"/>`);
        out.push(`<circle cx="${cx}" cy="${cy}" r="${r - 22}" fill="none" stroke="${ring}" stroke-width="1.2" opacity="0.35" stroke-dasharray="2 9"/>`);
        break;
      }
      default: break;
    }
    return { defs: defs.join(''), els: out.join('') };
  }

  // ------------------------------------------------------------ board
  function cornerMarkup(kind, x, y, t, rot) {
    const c = t.accent;
    const g = (inner) => `<g transform="translate(${x} ${y}) rotate(${rot})">${inner}</g>`;
    switch (kind) {
      case 'concentric': return g(`<circle r="30" fill="${darken(t.board.fc[0], 0.2)}"/><circle r="26" fill="none" stroke="${c}" stroke-width="3"/><circle r="17" fill="none" stroke="${c}" stroke-width="3"/><circle r="8" fill="${c}"/>`);
      case 'diamond': return g(`<rect x="-26" y="-26" width="52" height="52" transform="rotate(45)" fill="${darken(t.board.fc[0], 0.2)}" stroke="${c}" stroke-width="3"/><rect x="-11" y="-11" width="22" height="22" transform="rotate(45)" fill="${c}"/>`);
      case 'cross': return g(`<circle r="30" fill="${darken(t.board.fc[0], 0.25)}"/><path d="M0 -22V22M-22 0H22" stroke="${c}" stroke-width="5"/><circle cy="-22" r="5" fill="none" stroke="${c}" stroke-width="3"/><circle cy="22" r="5" fill="none" stroke="${c}" stroke-width="3"/><circle cx="-22" r="5" fill="none" stroke="${c}" stroke-width="3"/><circle cx="22" r="5" fill="none" stroke="${c}" stroke-width="3"/>`);
      case 'stud': return g(`<circle r="22" fill="${darken(c, 0.5)}"/><circle r="18" fill="${c}"/><circle cx="-6" cy="-6" r="6" fill="#fff" opacity="0.55"/>`);
      case 'triangle': return g(`<path d="M-30 -30H22L-30 22Z" fill="#000" opacity="0.22" stroke-linejoin="round"/>`);
      case 'bracket': return g(`<path d="M-20 22V-20H22" fill="none" stroke="${c}" stroke-width="4" stroke-linecap="square"/><circle cx="2" cy="2" r="3.5" fill="${c}"/>`);
      default: return '';
    }
  }

  function boardSVG(theme, opts) {
    const t = typeof theme === 'string' ? themeById(theme) : theme;
    const o = opts || {};
    if (t.board.layout === 'tabbed') return tabbedBoardSVG(t, o);
    const u = uid('bd');
    const S = BOARD.size, I = BOARD.inset, B = BOARD.band, R = t.board.radius;
    const defs = [], els = [];
    const clipO = uid(u + 'co'), clipF = uid(u + 'cf'), clipB = uid(u + 'cb'), pat = uid(u + 'p');
    const fieldR = Math.max(4, R - 30);
    defs.push(`<clipPath id="${clipO}"><rect x="${I}" y="${I}" width="${S - 2 * I}" height="${S - 2 * I}" rx="${R}"/></clipPath>`);
    defs.push(`<clipPath id="${clipF}"><rect x="${B}" y="${B}" width="${S - 2 * B}" height="${S - 2 * B}" rx="${fieldR}"/></clipPath>`);
    const b1 = 26, b2 = B - 22;
    defs.push(`<clipPath id="${clipB}"><path fill-rule="evenodd" clip-rule="evenodd" d="M${b1} ${b1}H${S - b1}V${S - b1}H${b1}ZM${b2} ${b2}V${S - b2}H${S - b2}V${b2}Z"/></clipPath>`);
    const tile = FRIEZES[t.board.frieze](t.board.fc);
    defs.push(`<pattern id="${pat}" width="${tile.w}" height="${tile.h}" patternUnits="userSpaceOnUse" x="${b1}" y="${b1}">${tile.body}</pattern>`);

    // frame material
    const frame = paintMaterial(u, t.board.frame, { shape: 'rect', x: I, y: I, w: S - 2 * I, h: S - 2 * I }, null, { glow: t.board.glow });
    defs.push(frame.defs);
    els.push(`<g clip-path="url(#${clipO})">${frame.els}`);
    els.push(`<rect x="${b1}" y="${b1}" width="${S - 2 * b1}" height="${S - 2 * b1}" fill="url(#${pat})" clip-path="url(#${clipB})" opacity="0.95"/>`);
    if (t.board.hairlines) {
      // classic: broken hairlines along each edge, leaving room for the centre marks
      const m = B / 2, a = 150, g = S / 2 - 70;
      els.push(`<path d="M${a} ${m}H${g}M${S - g} ${m}H${S - a}M${a} ${S - m}H${g}M${S - g} ${S - m}H${S - a}M${m} ${a}V${g}M${m} ${S - g}V${S - a}M${S - m} ${a}V${g}M${S - m} ${S - g}V${S - a}" stroke="${t.accent}" stroke-width="2.5"/>`);
    } else {
      els.push(`<rect x="${b1}" y="${b1}" width="${S - 2 * b1}" height="${S - 2 * b1}" fill="none" stroke="${t.accent}" stroke-width="2.5" opacity="0.8"/>`);
      els.push(`<rect x="${b2}" y="${b2}" width="${S - 2 * b2}" height="${S - 2 * b2}" fill="none" stroke="${t.accent}" stroke-width="2.5" opacity="0.8"/>`);
    }
    els.push(`</g>`);

    // field
    const plain = !!t.board.plainField;
    const field = paintMaterial(u, t.board.field, { shape: 'rect', x: B, y: B, w: S - 2 * B, h: S - 2 * B }, t.board.fieldTint, { seed: 17, glow: t.board.glow });
    defs.push(field.defs);
    els.push(`<g clip-path="url(#${clipF})">${field.els}`);
    if (t.board.fieldMotif === 'diamonds') {
      let d = '';
      for (let k = 0; k < 4; k++) {
        const cx = 512, cy = 512, s = 120 + k * 110;
        d += `<path d="M${cx} ${cy - s}L${cx + s} ${cy}L${cx} ${cy + s}L${cx - s} ${cy}Z" fill="none" stroke="${t.board.fc[1]}" stroke-width="10" opacity="0.13"/>`;
      }
      els.push(d);
    } else if (t.board.fieldMotif === 'circuit') {
      const fp = uid(u + 'fp');
      const ct = FRIEZES.circuit([t.board.fieldTint || '#000', t.board.fc[1], t.board.fc[2], t.board.fc[3]]);
      defs.push(`<pattern id="${fp}" width="${ct.w * 2}" height="${ct.h * 2}" patternUnits="userSpaceOnUse"><g transform="scale(2)">${ct.body.replace(/<rect width="72" height="72"[^>]*\/>/, '')}</g></pattern>`);
      els.push(`<rect x="${B}" y="${B}" width="${S - 2 * B}" height="${S - 2 * B}" fill="url(#${fp})" opacity="0.22"/>`);
    }
    // soft vignette so holes read against the field
    const vg = uid(u + 'vg');
    defs.push(`<radialGradient id="${vg}" cx="0.5" cy="0.5" r="0.7"><stop offset="0.6" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity="0.35"/></radialGradient>`);
    els.push(`<rect x="${B}" y="${B}" width="${S - 2 * B}" height="${S - 2 * B}" fill="url(#${vg})" opacity="${plain ? 0 : 1}"/></g>`);
    if (!plain) {
      // field edge bevel
      els.push(`<rect x="${B}" y="${B}" width="${S - 2 * B}" height="${S - 2 * B}" rx="${fieldR}" fill="none" stroke="#000" stroke-width="5" opacity="0.35"/>`);
      els.push(`<rect x="${B + 3}" y="${B + 3}" width="${S - 2 * B - 6}" height="${S - 2 * B - 6}" rx="${Math.max(2, fieldR - 3)}" fill="none" stroke="#fff" stroke-width="1.5" opacity="0.18"/>`);
    }
    if (t.board.stitch) {
      els.push(`<rect x="${I + 12}" y="${I + 12}" width="${S - 2 * I - 24}" height="${S - 2 * I - 24}" rx="${Math.max(4, R - 10)}" fill="none" stroke="${t.board.ring}" stroke-width="3" stroke-dasharray="12 9" opacity="0.8"/>`);
    }
    // outer edge
    els.push(`<rect x="${I}" y="${I}" width="${S - 2 * I}" height="${S - 2 * I}" rx="${R}" fill="none" stroke="#000" stroke-width="4" opacity="0.5"/>`);
    if (t.board.outline) els.push(`<rect x="${I + 2}" y="${I + 2}" width="${S - 2 * I - 4}" height="${S - 2 * I - 4}" rx="${R - 2}" fill="none" stroke="${t.board.outline}" stroke-width="4"/>`);
    els.push(`<rect x="${I + 3}" y="${I + 3}" width="${S - 2 * I - 6}" height="${S - 2 * I - 6}" rx="${Math.max(2, R - 3)}" fill="none" stroke="#fff" stroke-width="2" opacity="0.2"/>`);
    // corners
    const cc = [[B / 2 + 4, B / 2 + 4, 0], [S - B / 2 - 4, B / 2 + 4, 90], [S - B / 2 - 4, S - B / 2 - 4, 180], [B / 2 + 4, S - B / 2 - 4, 270]];
    cc.forEach(([x, y, r]) => els.push(cornerMarkup(t.board.corner, x, y, t, r)));
    if (t.board.edgeMarks) {
      const mark = (x, y, rot) => `<g transform="translate(${x} ${y}) rotate(${rot})" fill="#000" opacity="0.4"><rect x="-26" y="-7" width="14" height="14" transform="rotate(45 -19 0)"/><circle r="4"/><rect x="12" y="-7" width="14" height="14" transform="rotate(45 19 0)"/></g>`;
      els.push(mark(S / 2, B / 2, 0) + mark(S / 2, S - B / 2, 0) + mark(B / 2, S / 2, 90) + mark(S - B / 2, S / 2, 90));
    }

    // holes
    const surface = t.board.fieldTint || MATERIALS[t.board.field].base;
    const style = o.slot || t.board.slot;
    holeCenters().forEach(([x, y]) => {
      const h = holeMarkup(u, style, x, y, BOARD.holeR, t, surface);
      defs.push(h.defs); els.push(h.els);
    });

    // pieces placed on the board (lab preview only)
    if (o.layout) {
      const pts = holeCenters();
      o.layout.forEach((el, i) => {
        if (!el) return;
        const [x, y] = pts[i];
        els.push(`<g transform="translate(${x - 78} ${y - 78}) scale(${f(156 / 512)})">${pieceInner(t, el, o.pieceOpts || {})}</g>`);
      });
    }
    if (o.glow) els.push(boardGlowInner(t));
    return svgWrap(S, S, `<defs>${defs.join('')}</defs>${els.join('')}`, o.attrs);
  }

  function boardGlowInner(t) {
    if (t.board.layout === 'tabbed') return tabbedGlowInner(t);
    const S = BOARD.size, I = BOARD.inset, R = t.board.radius, c = t.accent;
    const u = uid('gl');
    return `<defs><filter id="${u}" x="-10%" y="-10%" width="120%" height="120%"><feGaussianBlur stdDeviation="9"/></filter></defs>` +
      `<rect x="${I + 2}" y="${I + 2}" width="${S - 2 * I - 4}" height="${S - 2 * I - 4}" rx="${R}" fill="none" stroke="${c}" stroke-width="18" filter="url(#${u})" opacity="0.9"/>` +
      `<rect x="${I + 2}" y="${I + 2}" width="${S - 2 * I - 4}" height="${S - 2 * I - 4}" rx="${R}" fill="none" stroke="${lighten(c, 0.5)}" stroke-width="4"/>`;
  }
  const boardGlowSVG = (theme) => svgWrap(BOARD.size, BOARD.size, boardGlowInner(typeof theme === 'string' ? themeById(theme) : theme));

  // ------------------------------------------------------------ piece shapes (projection)
  // Pieces are drawn in an oblique view: the footprint (base) sits in the hole and the top
  // face rises up the screen by the piece's height, with the side wall swept between them.
  // Sprite is 512 wide and 512 + h tall; the base centre is at (256, 248 + h), so a piece's
  // pivot (from the bottom) is 264 / (512 + h). Taller pieces overlap the row behind them.
  const PIECE_SHAPES = {
    dome: { name: 'Cabochon', h: 0, note: 'The original low dome, kept for comparison.' },
    coin: { name: 'Short cylinder', h: 30, note: 'A checker: a metal rim around an inset face of leather, enamel or stone.' },
    tall: { name: 'Tall cylinder', h: 380, note: 'A banded tower; it rises over the hole behind it and hides part of that piece.' },
    hex: { name: 'Hexagon', h: 56, note: 'A cut slab with bevelled edges, like a carved carnelian token.' },
    square: { name: 'Square', h: 50, note: 'A short tile with chamfered corners.' },
    pebble: { name: 'River pebble', h: 50, round: true, note: 'Smooth and slightly irregular, with a hand-painted glyph.' },
    tumbled: { name: 'Tumbled stone', h: 64, round: true, note: 'A random-shaped polished stone; every element gets its own silhouette.' },
    faceted: { name: 'Carved gem', h: 44, note: 'A brilliant-cut top with a thin girdle.' },
    sphere: { name: 'Bead', h: 0, note: 'A full sphere resting in the hole.' },
    vial: { name: 'Crystal vial', h: 430, note: 'A glowing crystal caged in metal, for light-based future themes.' },
  };
  const LIGHT = [-0.62, -0.78];
  const shapeOf = (t, el, o) => (o && o.shape) || (t.pieces[el] && t.pieces[el].shape) || t.pieceShape || 'dome';
  const shapeHeight = (shape) => (PIECE_SHAPES[shape] || PIECE_SHAPES.dome).h;
  const themeShape = (t) => (t.pieceStyle === 'kinetic' ? 'sphere' : t.pieceShape || 'dome');
  const pieceAspect = (t, o) => (512 + shapeHeight((o && o.shape) || themeShape(t))) / 512;
  const piecePivotY = (t, o) => 264 / (512 + shapeHeight((o && o.shape) || themeShape(t)));

  function footprint(shape, el) {
    const pts = [], R = rng(hashStr(shape + el));
    const ring = (n, fr) => { for (let i = 0; i < n; i++) { const a = (i / n) * Math.PI * 2; const r = fr(a); pts.push([Math.cos(a) * r[0], Math.sin(a) * r[1]]); } };
    switch (shape) {
      case 'hex': for (let i = 0; i < 6; i++) { const a = (i / 6) * Math.PI * 2; pts.push([Math.cos(a) * 226, Math.sin(a) * 226]); } break;
      case 'square': {
        const s = 186, c = 36;
        [[s, s, 0], [-s, s, 0.5], [-s, -s, 1], [s, -s, 1.5]].forEach(([x, y, q]) => {
          for (let k = 0; k <= 6; k++) { const a = (q + k / 12) * Math.PI; pts.push([x - Math.sign(x) * c + Math.cos(a) * c, y - Math.sign(y) * c + Math.sin(a) * c]); }
        });
        break;
      }
      case 'pebble': {
        const p1 = R() * 6, p2 = R() * 6, rot = (R() - 0.5) * 0.5;
        ring(64, (a) => { const k = 1 + 0.05 * Math.sin(2 * a + p1) + 0.035 * Math.sin(3 * a + p2); return [226 * k, 172 * k]; });
        for (const p of pts) { const [x, y] = p; p[0] = x * Math.cos(rot) - y * Math.sin(rot); p[1] = x * Math.sin(rot) + y * Math.cos(rot); }
        break;
      }
      case 'tumbled': {
        let poly = [];
        const n = 7 + Math.floor(R() * 3);
        for (let i = 0; i < n; i++) { const a = (i / n) * Math.PI * 2 + R() * 0.4; const r = 168 + R() * 52; poly.push([Math.cos(a) * r, Math.sin(a) * r * 0.86]); }
        for (let it = 0; it < 3; it++) {
          const nx = [];
          poly.forEach((p, i) => { const q = poly[(i + 1) % poly.length]; nx.push([p[0] * 0.75 + q[0] * 0.25, p[1] * 0.75 + q[1] * 0.25], [p[0] * 0.25 + q[0] * 0.75, p[1] * 0.25 + q[1] * 0.75]); });
          poly = nx;
        }
        pts.push(...poly);
        break;
      }
      case 'tall': ring(72, () => [196, 196]); break;
      case 'faceted': ring(72, () => [208, 208]); break;
      default: ring(72, () => [214, 214]);
    }
    return pts;
  }
  const shift = (pts, dx, dy) => pts.map(([x, y]) => [x + dx, y + dy]);
  const scalePts = (pts, k, cx, cy) => pts.map(([x, y]) => [cx + (x - cx) * k, cy + (y - cy) * k]);
  const polyD = (pts) => 'M' + pts.map(([x, y]) => `${f(x)} ${f(y)}`).join('L') + 'Z';
  function hull(points) {
    const p = points.slice().sort((a, b) => a[0] - b[0] || a[1] - b[1]);
    const cross = (o, a, b) => (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
    const lo = [], up = [];
    for (const q of p) { while (lo.length >= 2 && cross(lo[lo.length - 2], lo[lo.length - 1], q) <= 0) lo.pop(); lo.push(q); }
    for (let i = p.length - 1; i >= 0; i--) { const q = p[i]; while (up.length >= 2 && cross(up[up.length - 2], up[up.length - 1], q) <= 0) up.pop(); up.push(q); }
    return lo.slice(0, -1).concat(up.slice(0, -1));
  }
  // Shade each viewer-facing edge of the prism: light from the upper left.
  function sideFaces(top, h, strength) {
    let out = '';
    for (let i = 0; i < top.length; i++) {
      const a = top[i], b = top[(i + 1) % top.length];
      const ex = b[0] - a[0], ey = b[1] - a[1], len = Math.hypot(ex, ey) || 1;
      const nx = ey / len, ny = -ex / len; // outward for clockwise screen order
      if (ny <= 0.02) continue;
      const lit = -(nx * LIGHT[0]) * 0.9 - ny * 0.25;
      const col = lit > 0 ? '#fff' : '#000', op = Math.min(0.75, Math.abs(lit) * strength + (lit > 0 ? 0 : 0.18));
      out += `<path d="M${f(a[0])} ${f(a[1])}L${f(b[0])} ${f(b[1])}L${f(b[0])} ${f(b[1] + h)}L${f(a[0])} ${f(a[1] + h)}Z" fill="${col}" opacity="${f(op)}" stroke="${col}" stroke-opacity="${f(op)}" stroke-width="0.8"/>`;
    }
    return out;
  }
  // Bevel band between an outline and its inset copy, lit per edge.
  function bevelBand(outer, inner, strength) {
    let out = '';
    for (let i = 0; i < outer.length; i++) {
      const j = (i + 1) % outer.length, a = outer[i], b = outer[j];
      const ex = b[0] - a[0], ey = b[1] - a[1], len = Math.hypot(ex, ey) || 1;
      const nx = ey / len, ny = -ex / len;
      const lit = -(nx * LIGHT[0] + ny * LIGHT[1]);
      const col = lit > 0 ? '#fff' : '#000', op = Math.min(0.7, Math.abs(lit) * strength);
      out += `<path d="M${f(a[0])} ${f(a[1])}L${f(b[0])} ${f(b[1])}L${f(inner[j][0])} ${f(inner[j][1])}L${f(inner[i][0])} ${f(inner[i][1])}Z" fill="${col}" opacity="${f(op)}" stroke="${col}" stroke-opacity="${f(op)}" stroke-width="0.8"/>`;
    }
    return out;
  }
  // Glyph finishes: carved, emissive (light), gilded (gold leaf), painted (hand-applied paint).
  function glyphFinish(t, el, mode, cx, cy, gs, ink, glow, defs, u) {
    const gx = cx - gs / 2, gy = cy - gs / 2, set = t.glyphs;
    if (mode === 'emissive') {
      const b = uid(u + 'gb');
      defs.push(`<filter id="${b}" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="9"/></filter>`);
      return `<g filter="url(#${b})" opacity="0.95">${glyphMarkup(set, el, gx, gy, gs, glow || ink, 1.6)}</g>` + glyphMarkup(set, el, gx, gy, gs, lighten(ink, 0.35), 1);
    }
    if (mode === 'gilded') {
      const m = uid(u + 'gm'), g = uid(u + 'gg'), n = uid(u + 'gn');
      defs.push(`<mask id="${m}" maskUnits="userSpaceOnUse" x="0" y="0" width="512" height="1024">${glyphMarkup(set, el, gx, gy, gs, '#fff', 1.15)}</mask>`);
      defs.push(`<linearGradient id="${g}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#FFF1B8"/><stop offset="0.35" stop-color="#E7BE58"/><stop offset="0.7" stop-color="#B9852A"/><stop offset="1" stop-color="#F3D27A"/></linearGradient>`);
      defs.push(`<filter id="${n}" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency="0.35" numOctaves="2" seed="5"/><feColorMatrix type="matrix" values="0 0 0 0 0.45 0 0 0 0 0.3 0 0 0 0 0.05 1 0 0 0 -0.35"/></filter>`);
      return `<g opacity="0.55">${glyphMarkup(set, el, gx + 2, gy + 4, gs, '#000', 1.15)}</g>` +
        `<g mask="url(#${m})"><rect x="${f(gx - 10)}" y="${f(gy - 10)}" width="${gs + 20}" height="${gs + 20}" fill="url(#${g})"/><rect x="${f(gx - 10)}" y="${f(gy - 10)}" width="${gs + 20}" height="${gs + 20}" filter="url(#${n})"/></g>`;
    }
    if (mode === 'painted') {
      const d = uid(u + 'gp');
      defs.push(`<filter id="${d}" x="-10%" y="-10%" width="120%" height="120%"><feTurbulence type="fractalNoise" baseFrequency="0.06" numOctaves="2" seed="9" result="n"/><feDisplacementMap in="SourceGraphic" in2="n" scale="5"/></filter>`);
      return `<g filter="url(#${d})"><g opacity="0.95">${glyphMarkup(set, el, gx, gy, gs, ink, 1.25)}</g></g>`;
    }
    return `<g opacity="0.55">${glyphMarkup(set, el, gx - 3, gy - 4, gs, '#000', 1.15)}</g>` +
      `<g opacity="0.3">${glyphMarkup(set, el, gx + 3, gy + 4, gs, '#fff', 1.05)}</g>` + glyphMarkup(set, el, gx, gy, gs, ink, 1);
  }
  const busaraDots = (cx, cy, k, color) => {
    let d = '';
    for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) d += `<circle cx="${f(cx + (i - j) * 34 * k)}" cy="${f(cy - 51 * k + (i + j) * 34 * k)}" r="${f(14 * k)}" fill="${color}"/>`;
    return d;
  };

  function shapedPieceInner(t, el, o, shape) {
    o = o || {};
    const spec = Object.assign({}, t.pieces[el], o.override || {});
    const m = MATERIALS[spec.mat];
    const S = PIECE_SHAPES[shape], h = S.h;
    const u = uid('sp'), cx = 256, cy = 248, by = cy + h, H = 512 + h;
    const defs = [], els = [];
    const ink = spec.inlay, glow = spec.glow || (m.family === 'veined' ? ELEMENT_INFO[el].hue : null);
    const mode = o.finish || spec.finish || t.glyphMode;
    const bl = uid(u + 'bl');
    defs.push(`<filter id="${bl}" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="${o.lifted ? 18 : 11}"/></filter>`);
    const paint = (region, clipD, matId, color, seed) => {
      const c = uid(u + 'c');
      defs.push(`<clipPath id="${c}"><path d="${clipD}"/></clipPath>`);
      const p = paintMaterial(u, matId, region, color, { glow, seed: seed || 0 });
      defs.push(p.defs);
      return `<g clip-path="url(#${c})">${p.els}</g>`;
    };
    const base = shift(footprint(shape, el), cx, by);
    const top = shift(footprint(shape, el), cx, cy);

    if (shape === 'sphere') {
      const r = 206, sc = uid(u + 's'), sp = uid(u + 'k');
      els.push(`<ellipse cx="${cx + 24}" cy="${cy + 150}" rx="${r * 0.95}" ry="${r * 0.42}" fill="#000" opacity="0.5" filter="url(#${bl})"/>`);
      els.push(paint({ shape: 'circle', cx, cy, r }, `M${cx - r} ${cy}A${r} ${r} 0 1 0 ${cx + r} ${cy}A${r} ${r} 0 1 0 ${cx - r} ${cy}Z`, spec.mat, spec.color));
      defs.push(`<radialGradient id="${sc}" cx="${cx - 60}" cy="${cy - 70}" r="${r * 1.25}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#fff" stop-opacity="0.32"/><stop offset="0.35" stop-color="#fff" stop-opacity="0"/><stop offset="0.72" stop-color="#000" stop-opacity="0.25"/><stop offset="1" stop-color="#000" stop-opacity="0.8"/></radialGradient>`);
      defs.push(`<radialGradient id="${sp}"><stop offset="0" stop-color="#fff" stop-opacity="0.85"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>`);
      els.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="url(#${sc})"/>`);
      els.push(`<path d="M${cx + r * 0.55} ${cy + r * 0.72}A${r * 0.92} ${r * 0.92} 0 0 0 ${cx + r * 0.9} ${cy + r * 0.2}" fill="none" stroke="${lighten(spec.color || m.base, 0.5)}" stroke-width="6" opacity="0.35" stroke-linecap="round"/>`);
      if (!o.back) els.push(glyphFinish(t, el, mode, cx - 6, cy - 4, 186, ink, glow, defs, u));
      else els.push(busaraDots(cx, cy, 0.85, ink));
      els.push(`<ellipse cx="${cx - 70}" cy="${cy - 92}" rx="58" ry="34" fill="url(#${sp})" transform="rotate(-28 ${cx - 70} ${cy - 92})"/>`);
      return { body: `<defs>${defs.join('')}</defs>${els.join('')}`, H: 512 };
    }

    if (shape === 'vial') {
      const capR = 150, capH = 30, gr = 106, glass = uid(u + 'g'), cry = uid(u + 'y');
      els.push(`<ellipse cx="${cx + 18}" cy="${by + 40}" rx="${capR * 1.1}" ry="${capR * 0.55}" fill="#000" opacity="0.55" filter="url(#${bl})"/>`);
      const cap = (yTop) => {
        const b = shift(footprint('coin', el).map(([x, y]) => [x * capR / 214, y * capR / 214]), cx, yTop + capH);
        const tp = shift(b, 0, -capH);
        const metal = paint({ shape: 'rect', x: cx - capR - 4, y: yTop - capR - 4, w: capR * 2 + 8, h: capR * 2 + capH + 8 }, polyD(hull(b.concat(tp))), 'silver', null, 3);
        return metal + sideFaces(tp, capH, 0.9) + bevelBand(tp, scalePts(tp, 0.82, cx, yTop), 0.6) +
          `<circle cx="${cx}" cy="${yTop}" r="${capR * 0.66}" fill="none" stroke="#000" stroke-width="4" opacity="0.35"/><circle cx="${cx}" cy="${yTop}" r="${capR * 0.5}" fill="none" stroke="#000" stroke-width="3" opacity="0.3"/>` +
          `<ellipse cx="${cx - 34}" cy="${yTop - 40}" rx="44" ry="22" fill="#fff" opacity="0.35" transform="rotate(-25 ${cx - 34} ${yTop - 40})"/>`;
      };
      // base cap, glass body with crystal, cage rods, top cap
      els.push(cap(by - capH));
      defs.push(`<linearGradient id="${glass}" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#fff" stop-opacity="0.18"/><stop offset="0.2" stop-color="#fff" stop-opacity="0.45"/><stop offset="0.32" stop-color="#fff" stop-opacity="0.06"/><stop offset="0.85" stop-color="#000" stop-opacity="0.15"/><stop offset="1" stop-color="#fff" stop-opacity="0.12"/></linearGradient>`);
      const g0 = cy + capH, g1 = by - capH;
      const body = `M${cx - gr} ${g0}V${g1}A${gr} ${gr * 0.45} 0 0 0 ${cx + gr} ${g1}V${g0}Z`;
      const tint = glow || spec.color || ELEMENT_INFO[el].hue;
      els.push(`<path d="${body}" fill="${darken(tint, 0.82)}" opacity="0.9"/>`);
      const yc = (g0 + g1) / 2, ch = (g1 - g0) * 0.84;
      const crystal = `M${cx - 34} ${yc - ch / 2 + 30}L${cx} ${yc - ch / 2}L${cx + 40} ${yc - ch / 2 + 44}L${cx + 52} ${yc + ch / 2 - 40}L${cx + 8} ${yc + ch / 2}L${cx - 46} ${yc + ch / 2 - 30}Z`;
      defs.push(`<linearGradient id="${cry}" x1="0" y1="0" x2="1" y2="0.2"><stop offset="0" stop-color="${lighten(tint, 0.65)}"/><stop offset="0.5" stop-color="${lighten(tint, 0.25)}"/><stop offset="1" stop-color="${tint}"/></linearGradient>`);
      els.push(`<path d="${body}" fill="${tint}" opacity="0.55" filter="url(#${bl})"/>`);
      els.push(`<path d="${crystal}" fill="url(#${cry})" opacity="0.92"/><path d="M${cx} ${yc - ch / 2}L${cx + 6} ${yc + ch / 2 - 10}M${cx - 34} ${yc - ch / 2 + 30}L${cx - 20} ${yc + ch / 2 - 40}" stroke="#fff" stroke-width="3" opacity="0.6"/>`);
      if (!o.back) els.push(glyphFinish(t, el, 'emissive', cx + 4, yc, 150, ink || '#fff', tint, defs, u));
      else els.push(busaraDots(cx, yc, 0.7, '#fff'));
      els.push(`<path d="${body}" fill="url(#${glass})"/>`);
      [cx - capR + 16, cx + capR - 16].forEach((x) => els.push(`<rect x="${x - 9}" y="${g0}" width="18" height="${g1 - g0}" rx="6" fill="#C9A24A"/><rect x="${x - 9}" y="${g0}" width="7" height="${g1 - g0}" rx="3" fill="#fff" opacity="0.35"/>` +
        [0.25, 0.5, 0.75].map((k) => `<rect x="${x - 11}" y="${f(g0 + (g1 - g0) * k)}" width="22" height="6" rx="3" fill="#8A6A2A"/>`).join('')));
      els.push(cap(cy));
      return { body: `<defs>${defs.join('')}</defs>${els.join('')}`, H };
    }

    // prisms and stones: shadow, side wall, top face, bevel, glyph
    const sidePath = polyD(hull(base.concat(top)));
    const bbox = { shape: 'rect', x: 0, y: 0, w: 512, h: H };
    els.push(`<path d="${polyD(shift(base, 14, 22))}" fill="#000" opacity="0.5" filter="url(#${bl})"/>`);
    const rimMat = shape === 'coin' || shape === 'tall' ? (spec.rim || t.pieceRim || 'copper') : spec.mat;
    els.push(paint(bbox, sidePath, rimMat, rimMat === spec.mat ? spec.color : null, 5));
    els.push(sideFaces(top, h, S.round ? 0.8 : 1.1));
    if (shape === 'tall') {
      // banded tower: the shaft shows the piece material between metal rings
      const R = 196, shaft = (y0, y1) => `M${cx - R} ${y0}A${R} ${R} 0 0 0 ${cx + R} ${y0}V${y1}A${R} ${R} 0 0 1 ${cx - R} ${y1}Z`;
      const cyl = uid(u + 'y');
      defs.push(`<linearGradient id="${cyl}" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#000" stop-opacity="0.35"/><stop offset="0.24" stop-color="#fff" stop-opacity="0.2"/><stop offset="0.55" stop-color="#000" stop-opacity="0.08"/><stop offset="1" stop-color="#000" stop-opacity="0.62"/></linearGradient>`);
      els.push(paint(bbox, shaft(cy + 60, by - 60), spec.mat, spec.color, 9));
      els.push(`<path d="${shaft(cy + 60, by - 60)}" fill="url(#${cyl})"/>`);
      [cy + 60, by - 60].forEach((y) => els.push(`<path d="M${cx - R} ${y}A${R} ${R} 0 0 0 ${cx + R} ${y}" fill="none" stroke="#000" stroke-width="4" opacity="0.45"/>`));
    }
    if (S.round) {
      const sh = uid(u + 'r');
      defs.push(`<linearGradient id="${sh}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity="0.45"/></linearGradient>`);
      els.push(`<path d="${sidePath}" fill="url(#${sh})"/>`);
    }
    // top face
    const topD = polyD(top);
    if (shape === 'coin' || shape === 'tall') {
      const ring = scalePts(top, 0.84, cx, cy), inset = scalePts(top, 0.8, cx, cy);
      els.push(paint(bbox, topD, rimMat, null, 7));
      els.push(bevelBand(top, ring, 0.7));
      els.push(paint(bbox, polyD(inset), spec.mat, spec.color, 11));
      const isd = uid(u + 'i');
      defs.push(`<radialGradient id="${isd}" cx="${cx + 30}" cy="${cy + 40}" r="${200}" gradientUnits="userSpaceOnUse"><stop offset="0.75" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity="0.55"/></radialGradient>`);
      els.push(`<path d="${polyD(inset)}" fill="url(#${isd})"/>`);
    } else {
      els.push(paint(bbox, topD, spec.mat, spec.color, 13));
      if (S.round) {
        const rs = uid(u + 's'), hi = uid(u + 'h');
        defs.push(`<radialGradient id="${rs}" cx="${cx - 40}" cy="${cy - 50}" r="260" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#fff" stop-opacity="0.22"/><stop offset="0.5" stop-color="#fff" stop-opacity="0"/><stop offset="0.85" stop-color="#000" stop-opacity="0.22"/><stop offset="1" stop-color="#000" stop-opacity="0.5"/></radialGradient>`);
        defs.push(`<radialGradient id="${hi}"><stop offset="0" stop-color="#fff" stop-opacity="${f(0.25 + m.shine * 0.55)}"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>`);
        els.push(`<path d="${topD}" fill="url(#${rs})"/>`);
        els.push(`<ellipse cx="${cx - 70}" cy="${cy - 70}" rx="80" ry="40" fill="url(#${hi})" transform="rotate(-24 ${cx - 70} ${cy - 70})"/>`);
      } else if (shape === 'faceted') {
        const n = 16, r0 = 206, r1 = 112;
        let fac = '';
        for (let i = 0; i < n; i++) {
          const a0 = (i / n) * Math.PI * 2, a1 = ((i + 1) / n) * Math.PI * 2, am = (a0 + a1) / 2;
          const p = (r, a) => `${f(cx + Math.cos(a) * r)} ${f(cy + Math.sin(a) * r)}`;
          const lit = -(Math.cos(am) * LIGHT[0] + Math.sin(am) * LIGHT[1]);
          fac += `<path d="M${p(r1, a0)}L${p(r0, am)}L${p(r1, a1)}Z" fill="${lit > 0 ? '#fff' : '#000'}" opacity="${f(Math.abs(lit) * 0.35 + 0.05)}"/>`;
          fac += `<path d="M${p(r0, a0)}L${p(r0, a1)}L${p(r1, a1)}L${p(r0, am)}L${p(r1, a0)}Z" fill="${(i % 2) ? '#fff' : '#000'}" opacity="0.1"/>`;
        }
        const table = Array.from({ length: 8 }, (_, i) => { const a = (i / 8) * Math.PI * 2 + Math.PI / 8; return [cx + Math.cos(a) * r1, cy + Math.sin(a) * r1]; });
        els.push(fac + `<path d="${polyD(table)}" fill="#fff" opacity="0.08" stroke="#fff" stroke-opacity="0.35" stroke-width="2"/>`);
      } else {
        els.push(bevelBand(top, scalePts(top, 0.86, cx, cy), 0.85));
      }
    }
    els.push(`<path d="${topD}" fill="none" stroke="#000" stroke-width="2.5" opacity="0.45"/>`);
    const gsK = { coin: 0.78, tall: 0.8, hex: 0.92, square: 0.95, pebble: 0.8, tumbled: 0.74, faceted: 0.62 }[shape] || 0.9;
    const gs = (t.glyphs === 'ornate' ? 236 : 220) * gsK;
    if (!o.back) els.push(glyphFinish(t, el, mode, cx, cy, gs, ink, glow, defs, u));
    else els.push(busaraDots(cx, cy, gsK, ink));
    if (m.shine > 0.45 && !S.round) {
      const sp = uid(u + 'p');
      defs.push(`<linearGradient id="${sp}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity="${f(m.shine * 0.4)}"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>`);
      const c2 = uid(u + 'q');
      defs.push(`<clipPath id="${c2}"><path d="${topD}"/></clipPath>`);
      els.push(`<g clip-path="url(#${c2})"><ellipse cx="${cx - 40}" cy="${cy - 150}" rx="230" ry="110" fill="url(#${sp})"/></g>`);
    }
    return { body: `<defs>${defs.join('')}</defs>${els.join('')}`, H };
  }

  // ------------------------------------------------------------ pieces
  // 512 sprite, disc r=214 centred, room for a soft contact shadow.
  // Body markup + sprite height for any shape. Dome and kinetic keep the 512 square.
  function pieceBody(t, el, o) {
    const shape = shapeOf(t, el, o);
    if (t.pieceStyle === 'kinetic' && !(o && o.shape)) return { body: kineticPieceInner(t, el, o), H: 512 };
    if (shape === 'dome' || !PIECE_SHAPES[shape]) return { body: domePieceInner(t, el, o), H: 512 };
    return shapedPieceInner(t, el, o, shape);
  }
  // Markup placed with its base centre at (256, 248) in a 512 box, for boards and holes.
  function pieceInner(t, el, o) {
    const p = pieceBody(t, el, o);
    return p.H === 512 ? p.body : `<g transform="translate(0 ${248 - (p.H - 264)})">${p.body}</g>`;
  }
  function domePieceInner(t, el, o) {
    o = o || {};
    const spec = Object.assign({}, t.pieces[el], o.override || {});
    const m = MATERIALS[spec.mat];
    const u = uid('pc');
    const cx = 256, cy = 248, r = 214;
    const defs = [], els = [];
    const clip = uid(u + 'c');
    defs.push(`<clipPath id="${clip}"><circle cx="${cx}" cy="${cy}" r="${r}"/></clipPath>`);
    // contact shadow
    const sh = uid(u + 's');
    defs.push(`<filter id="${sh}" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="${o.lifted ? 16 : 9}"/></filter>`);
    els.push(`<ellipse cx="${cx + (o.lifted ? 14 : 6)}" cy="${cy + (o.lifted ? 30 : 14)}" rx="${r - 4}" ry="${r - 10}" fill="#000" opacity="${o.lifted ? 0.35 : 0.5}" filter="url(#${sh})"/>`);
    // side wall (thickness)
    const side = spec.color ? darken(spec.color, 0.5) : m.dark;
    els.push(`<circle cx="${cx}" cy="${cy + 10}" r="${r}" fill="${side}"/>`);
    // face
    const glowColor = spec.glow || (m.family === 'veined' ? ELEMENT_INFO[el].hue : null);
    const face = paintMaterial(u, spec.mat, { shape: 'circle', cx, cy, r }, spec.color, { glow: glowColor });
    defs.push(face.defs);
    els.push(`<g clip-path="url(#${clip})">${face.els}`);
    // bevel light
    const bv = uid(u + 'b');
    const sh2 = m.shine;
    defs.push(`<radialGradient id="${bv}" cx="${cx - 70}" cy="${cy - 90}" r="${r * 1.35}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#fff" stop-opacity="${f(0.18 + sh2 * 0.25)}"/><stop offset="0.45" stop-color="#fff" stop-opacity="0"/><stop offset="0.8" stop-color="#000" stop-opacity="0.12"/><stop offset="1" stop-color="#000" stop-opacity="${f(0.35 + sh2 * 0.15)}"/></radialGradient>`);
    els.push(`<rect x="${cx - r}" y="${cy - r}" width="${r * 2}" height="${r * 2}" fill="url(#${bv})"/></g>`);
    // rim
    els.push(`<circle cx="${cx}" cy="${cy}" r="${r - 3}" fill="none" stroke="#fff" stroke-width="5" opacity="${f(0.12 + sh2 * 0.25)}"/>`);
    els.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="none" stroke="#000" stroke-width="3" opacity="0.4"/>`);

    // era ornament ring
    const inlay = spec.inlay;
    if (t.glyphs === 'ornate') {
      const rr = r * 0.82;
      if (m.family === 'leather') {
        els.push(`<circle cx="${cx}" cy="${cy}" r="${rr + 12}" fill="none" stroke="${inlay}" stroke-width="4" stroke-dasharray="14 10" opacity="0.8"/>`);
      } else {
        let dots = '';
        const n = 28;
        for (let i = 0; i < n; i++) {
          const a = (i / n) * Math.PI * 2;
          dots += `<circle cx="${f(cx + Math.cos(a) * rr)}" cy="${f(cy + Math.sin(a) * rr)}" r="${i % 2 ? 3.2 : 5.2}" fill="${inlay}" opacity="${t.glyphMode === 'emissive' ? 0.9 : 0.85}"/>`;
        }
        els.push(`<g transform="translate(-1.5 2)" opacity="0.6">${dots.replace(/fill="[^"]+"/g, 'fill="#000"')}</g>${dots}`);
      }
    } else {
      els.push(`<circle cx="${cx}" cy="${cy}" r="${r * 0.8}" fill="none" stroke="${inlay}" stroke-width="3" opacity="0.55"/>`);
      els.push(`<path d="M${cx - r * 0.8} ${cy}H${cx - r * 0.66}M${cx + r * 0.66} ${cy}H${cx + r * 0.8}" stroke="${inlay}" stroke-width="5" stroke-linecap="round" opacity="0.8"/>`);
    }

    // glyph
    if (!o.back) {
      const gs = t.glyphs === 'ornate' ? 236 : 220;
      const gx = cx - gs / 2, gy = cy - gs / 2;
      if (t.glyphMode === 'emissive') {
        const gl = uid(u + 'g');
        defs.push(`<filter id="${gl}" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="9"/></filter>`);
        const glow = spec.glow || inlay;
        els.push(`<g filter="url(#${gl})" opacity="0.95">${glyphMarkup(t.glyphs, el, gx, gy, gs, glow, 1.6)}</g>`);
        els.push(glyphMarkup(t.glyphs, el, gx, gy, gs, lighten(inlay, 0.35), 1));
      } else {
        // carved: shadow cut, highlight lip, inlay fill
        els.push(glyphMarkup(t.glyphs, el, gx - 3, gy - 4, gs, '#000', 1.15).replace('<g ', '<g opacity="0.55" '));
        els.push(glyphMarkup(t.glyphs, el, gx + 3, gy + 4, gs, '#fff', 1.05).replace('<g ', '<g opacity="0.3" '));
        els.push(glyphMarkup(t.glyphs, el, gx, gy, gs, inlay, 1));
      }
    } else {
      // hidden piece: the Busara diamond of 16 dots, kingdom-neutral
      let d = '';
      for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) {
        const x = cx + (i - j) * 34, y = cy - 51 + (i + j) * 34;
        d += `<circle cx="${x}" cy="${y}" r="14" fill="${inlay}"/>`;
      }
      els.push(`<g transform="translate(-2 3)" opacity="0.5">${d.replace(/fill="[^"]+"/g, 'fill="#000"')}</g>${d}`);
    }

    // specular
    if (sh2 > 0.45) {
      const sp = uid(u + 'p');
      defs.push(`<linearGradient id="${sp}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity="${f(sh2 * 0.5)}"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>`);
      els.push(`<path d="M${cx - r * 0.72} ${cy - r * 0.38}A${r * 0.8} ${r * 0.8} 0 0 1 ${cx + r * 0.5} ${cy - r * 0.66}A${r * 0.95} ${r * 0.75} 0 0 0 ${cx - r * 0.72} ${cy - r * 0.38}Z" fill="url(#${sp})"/>`);
    }
    return `<defs>${defs.join('')}</defs>${els.join('')}`;
  }
  function pieceSVG(theme, el, o) {
    const p = pieceBody(typeof theme === 'string' ? themeById(theme) : theme, el, o);
    return svgWrap(512, p.H, p.body, o && o.attrs);
  }

  // Standalone material swatch, used by the material library.
  function materialSVG(matId, o) {
    const m = MATERIALS[matId];
    const t = {
      glyphs: m.era === 'past' ? 'ornate' : 'minimal',
      glyphMode: ['veined', 'glass', 'lumen', 'iridescent'].includes(m.family) ? 'emissive' : 'carved',
      pieces: { [o && o.el || 'earth']: { mat: matId, inlay: luminance(m.base) > 0.35 ? darken(m.base, 0.7) : lighten(m.base, 0.75), glow: m.family === 'veined' ? '#E8A33A' : null } },
    };
    return pieceSVG(t, (o && o.el) || 'earth', o);
  }

  // ------------------------------------------------------------ state overlays
  // Slot overlays: 208x208 viewBox = one hole pitch, hole r=82 at centre.
  const SLOT_STATES = {
    hover: 'Pointer over an empty slot',
    legal: 'Legal placement (no orthogonal neighbour on this board)',
    blocked: 'Blocked: an orthogonal neighbour already holds a resource',
    selected: 'Chosen destination',
    target: 'Targeted by a weapon or power',
  };
  function slotStateSVG(theme, state) {
    const t = typeof theme === 'string' ? themeById(theme) : theme;
    const c = 104, r = BOARD.holeR, a = t.accent, d = t.danger;
    const past = t.era === 'past';
    const u = uid('ss');
    const blur = `<filter id="${u}" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="5"/></filter>`;
    let b = '';
    switch (state) {
      case 'hover':
        b = `<circle cx="${c}" cy="${c}" r="${r}" fill="${t.light}" opacity="0.14"/><circle cx="${c}" cy="${c}" r="${r + 4}" fill="none" stroke="${t.light}" stroke-width="3" opacity="0.6"/>`;
        break;
      case 'legal':
        if (past) {
          let dots = '';
          for (let i = 0; i < 24; i++) { const an = (i / 24) * Math.PI * 2; dots += `<circle cx="${f(c + Math.cos(an) * (r + 6))}" cy="${f(c + Math.sin(an) * (r + 6))}" r="${i % 2 ? 3 : 5}" fill="${a}"/>`; }
          b = `<circle cx="${c}" cy="${c}" r="${r - 4}" fill="${a}" opacity="0.16"/><g filter="url(#${u})" opacity="0.7">${dots}</g>${dots}<circle cx="${c}" cy="${c}" r="9" fill="${a}" opacity="0.8"/>`;
        } else {
          const circ = 2 * Math.PI * (r + 6);
          b = `<circle cx="${c}" cy="${c}" r="${r - 4}" fill="${a}" opacity="0.14"/><circle cx="${c}" cy="${c}" r="${r + 6}" fill="none" stroke="${a}" stroke-width="10" opacity="0.5" filter="url(#${u})" stroke-dasharray="${f(circ / 8 - 10)} 10"/><circle cx="${c}" cy="${c}" r="${r + 6}" fill="none" stroke="${lighten(a, 0.3)}" stroke-width="4" stroke-dasharray="${f(circ / 8 - 10)} 10"/><circle cx="${c}" cy="${c}" r="6" fill="${a}"/>`;
        }
        break;
      case 'blocked':
        b = `<circle cx="${c}" cy="${c}" r="${r - 2}" fill="#000" opacity="0.35"/><circle cx="${c}" cy="${c}" r="${r + 4}" fill="none" stroke="${d}" stroke-width="3" stroke-dasharray="6 8" opacity="0.9"/>` +
          `<path d="M${c - 18} ${c - 18}L${c + 18} ${c + 18}M${c + 18} ${c - 18}L${c - 18} ${c + 18}" stroke="${d}" stroke-width="7" stroke-linecap="round" opacity="0.85"/>`;
        break;
      case 'selected':
        b = `<circle cx="${c}" cy="${c}" r="${r + 8}" fill="none" stroke="${a}" stroke-width="12" opacity="0.55" filter="url(#${u})"/><circle cx="${c}" cy="${c}" r="${r + 5}" fill="none" stroke="${a}" stroke-width="5"/><circle cx="${c}" cy="${c}" r="${r + 15}" fill="none" stroke="${a}" stroke-width="2" opacity="0.7"/>`;
        break;
      case 'target': {
        const k = r + 14, l = 26;
        const br = [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([sx, sy]) => `<path d="M${c + sx * k} ${c + sy * (k - l)}V${c + sy * k}H${c + sx * (k - l)}" fill="none" stroke="${d}" stroke-width="6" stroke-linecap="${past ? 'round' : 'square'}"/>`).join('');
        b = `<circle cx="${c}" cy="${c}" r="${r}" fill="${d}" opacity="0.14"/><g filter="url(#${u})" opacity="0.6">${br}</g>${br}`;
        break;
      }
      default: break;
    }
    return svgWrap(208, 208, `<defs>${blur}</defs>${b}`);
  }

  // Piece overlays: 512 viewBox, same frame as pieceSVG.
  const PIECE_STATES = {
    selected: 'Picked up to move, trade or forge',
    threatened: 'Marked for removal by a disaster or weapon',
    shielded: 'Protected (Celestial Dome, King\'s Necklace)',
  };
  function pieceStateSVG(theme, state) {
    const t = typeof theme === 'string' ? themeById(theme) : theme;
    const cx = 256, cy = 248, r = 214, a = t.accent, d = t.danger;
    const u = uid('ps');
    const blur = `<filter id="${u}" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="10"/></filter>`;
    let b = '';
    switch (state) {
      case 'selected':
        b = `<circle cx="${cx}" cy="${cy}" r="${r + 18}" fill="none" stroke="${a}" stroke-width="22" opacity="0.6" filter="url(#${u})"/><circle cx="${cx}" cy="${cy}" r="${r + 14}" fill="none" stroke="${lighten(a, 0.25)}" stroke-width="9"/>`;
        break;
      case 'threatened': {
        const crack = `M${cx - 120} ${cy - 150}L${cx - 70} ${cy - 60}L${cx - 100} ${cy + 10}L${cx - 30} ${cy + 80}M${cx - 70} ${cy - 60}L${cx + 10} ${cy - 90}M${cx + 150} ${cy + 90}L${cx + 80} ${cy + 40}L${cx + 100} ${cy - 20}`;
        b = `<circle cx="${cx}" cy="${cy}" r="${r + 10}" fill="none" stroke="${d}" stroke-width="10" stroke-dasharray="26 16"/>` +
          `<path d="${crack}" fill="none" stroke="#000" stroke-width="10" opacity="0.6" stroke-linejoin="round"/><path d="${crack}" fill="none" stroke="${lighten(d, 0.4)}" stroke-width="4" stroke-linejoin="round"/>` +
          `<circle cx="${cx + 160}" cy="${cy - 160}" r="44" fill="${d}" stroke="#fff" stroke-width="6"/><path d="M${cx + 142} ${cy - 178}L${cx + 178} ${cy - 142}M${cx + 178} ${cy - 178}L${cx + 142} ${cy - 142}" stroke="#fff" stroke-width="10" stroke-linecap="round"/>`;
        break;
      }
      case 'shielded': {
        const hexes = [];
        for (let i = 0; i < 6; i++) {
          const an = (i / 6) * Math.PI * 2 - Math.PI / 2;
          hexes.push(`${f(cx + Math.cos(an) * (r + 26))},${f(cy + Math.sin(an) * (r + 26))}`);
        }
        b = `<circle cx="${cx}" cy="${cy}" r="${r + 24}" fill="${t.light}" opacity="0.12"/><polygon points="${hexes.join(' ')}" fill="none" stroke="${t.light}" stroke-width="6" opacity="0.85" stroke-linejoin="round"/>` +
          `<polygon points="${hexes.join(' ')}" fill="none" stroke="${a}" stroke-width="18" opacity="0.35" filter="url(#${u})"/>` +
          `<path d="M${cx - r * 0.6} ${cy - r * 0.75}A${r} ${r} 0 0 1 ${cx + r * 0.6} ${cy - r * 0.75}" fill="none" stroke="#fff" stroke-width="7" opacity="0.6" stroke-linecap="round"/>`;
        break;
      }
      default: break;
    }
    return svgWrap(512, 512, `<defs>${blur}</defs>${b}`);
  }

  // A single hole on a patch of board surface, optionally with a piece and a state overlay.
  // 208x208 viewBox = one hole pitch. Used by the lab's hole/state matrix.
  function holeSVG(theme, style, o) {
    const t = typeof theme === 'string' ? themeById(theme) : theme;
    o = o || {};
    const u = uid('hp');
    const surface = t.board.fieldTint || MATERIALS[t.board.field].base;
    const field = paintMaterial(u, t.board.field, { shape: 'rect', x: 0, y: 0, w: 208, h: 208 }, t.board.fieldTint, { seed: 17, glow: t.board.glow });
    const h = holeMarkup(u, style || t.board.slot, 104, 104, BOARD.holeR, t, surface);
    let body = `<defs>${field.defs}${h.defs}</defs>${field.els}${h.els}`;
    if (o.piece) body += `<g transform="translate(26 26) scale(${f(156 / 512)})">${pieceInner(t, o.piece, o.pieceOpts || {})}</g>`;
    if (o.state) body += slotStateSVG(t, o.state).replace(/^<svg[^>]*>/, '<g>').replace(/<\/svg>$/, '</g>');
    return svgWrap(208, 208, body);
  }

  // ------------------------------------------------------------ Panther Court renderers
  // Tabbed slate board: body with folder tabs top and bottom, gold border, corner triangles,
  // broken gold hairlines with dark pill marks. Hole geometry is the shared BOARD grid.
  const TAB = { x0: 44, y0: 60, x1: 980, y1: 964, rx: 34, tabX0: 330, tabX1: 694, tabH: 42 };
  const tabPath = (top) => {
    const { tabX0: a, tabX1: b, tabH: h, y0, y1 } = TAB, r = 14;
    return top
      ? `M${a} ${y0 + 4}V${y0 - h + r}Q${a} ${y0 - h} ${a + r} ${y0 - h}H${b - r}Q${b} ${y0 - h} ${b} ${y0 - h + r}V${y0 + 4}Z`
      : `M${a} ${y1 - 4}V${y1 + h - r}Q${a} ${y1 + h} ${a + r} ${y1 + h}H${b - r}Q${b} ${y1 + h} ${b} ${y1 + h - r}V${y1 - 4}Z`;
  };
  function tabbedBoardSVG(t, o) {
    const u = uid('tb'), S = BOARD.size;
    const { x0, y0, x1, y1, rx } = TAB, w = x1 - x0, h = y1 - y0;
    const gold = t.board.fc[1], defs = [], els = [];
    const body = uid(u + 'b'), clip = uid(u + 'c'), rim = uid(u + 'g'), sheen = uid(u + 's');
    defs.push(`<linearGradient id="${body}" x1="0" y1="0" x2="0.35" y2="1"><stop offset="0" stop-color="#3D3D58"/><stop offset="0.55" stop-color="#31314A"/><stop offset="1" stop-color="#26263A"/></linearGradient>`);
    defs.push(`<linearGradient id="${rim}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${lighten(gold, 0.25)}"/><stop offset="0.5" stop-color="${gold}"/><stop offset="1" stop-color="${darken(gold, 0.25)}"/></linearGradient>`);
    defs.push(`<radialGradient id="${sheen}" cx="0.3" cy="0.2" r="0.9"><stop offset="0" stop-color="#8C86C8" stop-opacity="0.16"/><stop offset="1" stop-color="#000" stop-opacity="0"/></radialGradient>`);
    defs.push(`<clipPath id="${clip}"><rect x="${x0}" y="${y0}" width="${w}" height="${h}" rx="${rx}"/></clipPath>`);
    // tabs sit behind the body so they read as part of one folder shape
    [true, false].forEach((top) => els.push(`<path d="${tabPath(top)}" fill="#2A2A3F" stroke="url(#${rim})" stroke-width="4"/>`));
    els.push(`<rect x="${x0}" y="${y0 + 8}" width="${w}" height="${h}" rx="${rx}" fill="#000" opacity="0.4"/>`);
    els.push(`<rect x="${x0}" y="${y0}" width="${w}" height="${h}" rx="${rx}" fill="url(#${body})"/>`);
    const grain = noiseLayer(u, { x: x0, y: y0, w, h, freq: '0.9', oct: 2, seed: 7, table: '0 0 0 0 0.55', color: '#000', op: 0.35 });
    const flecks = noiseLayer(u, { x: x0, y: y0, w, h, freq: '0.7', oct: 2, seed: 11, table: '0 0 0 0 0 0.6', color: '#9C97D6', op: 0.18 });
    defs.push(grain.def, flecks.def);
    els.push(`<g clip-path="url(#${clip})">${grain.el}${flecks.el}<rect x="${x0}" y="${y0}" width="${w}" height="${h}" fill="url(#${sheen})"/></g>`);
    els.push(`<rect x="${x0 + 2}" y="${y0 + 2}" width="${w - 4}" height="${h - 4}" rx="${rx - 2}" fill="none" stroke="url(#${rim})" stroke-width="4.5"/>`);
    els.push(`<rect x="${x0 + 8}" y="${y0 + 8}" width="${w - 16}" height="${h - 16}" rx="${rx - 8}" fill="none" stroke="#14141F" stroke-width="2.5" opacity="0.8"/>`);
    // corner triangles
    const c = 24, L = 66;
    [[x0 + c, y0 + c, 1, 1], [x1 - c, y0 + c, -1, 1], [x1 - c, y1 - c, -1, -1], [x0 + c, y1 - c, 1, -1]].forEach(([x, y, sx, sy]) => {
      els.push(`<path d="M${x} ${y}H${x + sx * L}L${x} ${y + sy * L}Z" fill="#1A1A28" opacity="0.9"/>`);
      els.push(`<path d="M${x + sx * L} ${y}L${x} ${y + sy * L}" stroke="#4A4A66" stroke-width="1.5" opacity="0.7"/>`);
    });
    // hairlines + marks
    const hy = [y0 + 44, y1 - 44], vx = [x0 + 34, x1 - 34], g = 3;
    let lines = '';
    hy.forEach((y) => { lines += `M150 ${y}H468M556 ${y}H874`; });
    vx.forEach((x) => { lines += `M${x} 196V470M${x} 554V828`; });
    els.push(`<path d="${lines}" stroke="${gold}" stroke-width="${g}" stroke-linecap="round" opacity="0.92"/>`);
    hy.forEach((y) => els.push(`<rect x="486" y="${y - 5}" width="20" height="10" rx="5" fill="#16161F"/><rect x="518" y="${y - 5}" width="20" height="10" rx="5" fill="#16161F"/>`));
    vx.forEach((x) => els.push(`<circle cx="${x}" cy="498" r="5.5" fill="#16161F"/><circle cx="${x}" cy="526" r="5.5" fill="#16161F"/>`));
    // holes
    holeCenters().forEach(([x, y]) => {
      const hm = holeMarkup(u, t.board.slot, x, y, BOARD.holeR, t, '#2E2E44');
      defs.push(hm.defs); els.push(hm.els);
    });
    if (o.layout) {
      const pts = holeCenters();
      o.layout.forEach((el, i) => {
        if (!el) return;
        const [x, y] = pts[i];
        els.push(`<g transform="translate(${x - 78} ${y - 78}) scale(${f(156 / 512)})">${pieceInner(t, el, o.pieceOpts || {})}</g>`);
      });
    }
    if (o.glow) els.push(tabbedGlowInner(t));
    return svgWrap(S, S, `<defs>${defs.join('')}</defs>${els.join('')}`, o.attrs);
  }
  // Active board: thick gold rim with glow, top tab filled gold, as on the reference's active board.
  function tabbedGlowInner(t) {
    const u = uid('tg'), c = t.board.glowColor || t.accent;
    const { x0, y0, x1, y1, rx } = TAB, w = x1 - x0, h = y1 - y0;
    const fill = uid(u + 'f');
    return `<defs><filter id="${u}" x="-10%" y="-10%" width="120%" height="120%"><feGaussianBlur stdDeviation="10"/></filter>` +
      `<linearGradient id="${fill}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${lighten(c, 0.25)}"/><stop offset="1" stop-color="${darken(c, 0.15)}"/></linearGradient></defs>` +
      `<rect x="${x0}" y="${y0}" width="${w}" height="${h}" rx="${rx}" fill="none" stroke="${c}" stroke-width="20" filter="url(#${u})" opacity="0.85"/>` +
      `<path d="${tabPath(true)}" fill="url(#${fill})" stroke="${lighten(c, 0.4)}" stroke-width="3"/>` +
      `<path d="${tabPath(false)}" fill="none" stroke="${c}" stroke-width="10" filter="url(#${u})" opacity="0.6"/>` +
      `<rect x="${x0 + 2}" y="${y0 + 2}" width="${w - 4}" height="${h - 4}" rx="${rx - 2}" fill="none" stroke="${lighten(c, 0.2)}" stroke-width="8"/>`;
  }

  // Kinetic bead: gunmetal bezel etched with script ticks, nanoweave (or raw ore) shell,
  // a charge ring in the element colour bleeding to purple, and a glowing angular glyph.
  function kineticPieceInner(t, el, o) {
    o = o || {};
    const spec = Object.assign({}, t.pieces[el], o.override || {});
    const u = uid('kp'), cx = 256, cy = 248, r = 214, ri = 184;
    const glow = spec.glow || ELEMENT_INFO[el].hue, kinetic = '#9B5CFF';
    const defs = [], els = [];
    const sh = uid(u + 'h'), bez = uid(u + 'z'), clip = uid(u + 'c'), ring = uid(u + 'r'), core = uid(u + 'o'), blur = uid(u + 'b'), shade = uid(u + 's'), gloss = uid(u + 'g');
    defs.push(`<filter id="${sh}" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="${o.lifted ? 16 : 9}"/></filter>`);
    defs.push(`<filter id="${blur}" x="-40%" y="-40%" width="180%" height="180%"><feGaussianBlur stdDeviation="9"/></filter>`);
    defs.push(`<linearGradient id="${bez}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#F1F2F8"/><stop offset="0.28" stop-color="#8C90A6"/><stop offset="0.52" stop-color="#3A3D4C"/><stop offset="0.76" stop-color="#C9CCDA"/><stop offset="1" stop-color="#474A5A"/></linearGradient>`);
    defs.push(`<clipPath id="${clip}"><circle cx="${cx}" cy="${cy}" r="${ri}"/></clipPath>`);
    defs.push(`<linearGradient id="${ring}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${lighten(glow, 0.2)}"/><stop offset="0.5" stop-color="${glow}"/><stop offset="1" stop-color="${kinetic}"/></linearGradient>`);
    defs.push(`<radialGradient id="${core}"><stop offset="0" stop-color="${glow}" stop-opacity="0.55"/><stop offset="0.7" stop-color="${glow}" stop-opacity="0.1"/><stop offset="1" stop-color="${glow}" stop-opacity="0"/></radialGradient>`);
    defs.push(`<radialGradient id="${shade}" cx="${cx}" cy="${cy}" r="${ri}" gradientUnits="userSpaceOnUse"><stop offset="0.7" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity="0.65"/></radialGradient>`);
    defs.push(`<linearGradient id="${gloss}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity="0.38"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>`);
    els.push(`<ellipse cx="${cx + 24}" cy="${cy + 150}" rx="${r * 0.95}" ry="${r * 0.42}" fill="#000" opacity="${o.lifted ? 0.35 : 0.55}" filter="url(#${sh})"/>`);
    // bezel with lathe rings
    els.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="url(#${bez})"/>`);
    const R = rng(hashStr(el + 'bezel'));
    let lathe = '';
    for (let rr = ri + 2; rr < r; rr += 2.2) lathe += `<circle cx="${cx}" cy="${cy}" r="${f(rr)}" fill="none" stroke="${R() > 0.5 ? '#fff' : '#000'}" stroke-width="1" opacity="${f(0.04 + R() * 0.1)}"/>`;
    els.push(lathe);
    els.push(`<circle cx="${cx}" cy="${cy}" r="${r - 1}" fill="none" stroke="#000" stroke-width="2.5" opacity="0.5"/>`);
    // shell
    const shell = paintMaterial(u, spec.mat, { shape: 'circle', cx, cy, r: ri }, null, { glow: spec.mat === 'kinetic_ore' ? glow : null });
    defs.push(shell.defs);
    els.push(`<g clip-path="url(#${clip})">${shell.els}<rect x="${cx - ri}" y="${cy - ri}" width="${ri * 2}" height="${ri * 2}" fill="url(#${shade})"/></g>`);
    els.push(`<circle cx="${cx}" cy="${cy}" r="${ri}" fill="none" stroke="#000" stroke-width="5" opacity="0.7"/>`);
    // etched script ticks on the bezel
    let ticks = '';
    for (let i = 0; i < 48; i++) {
      if (i % 12 === 0) continue;
      const a = (i / 48) * Math.PI * 2, l = i % 3 === 0 ? 14 : 7, r0 = ri + 7;
      ticks += `M${f(cx + Math.cos(a) * r0)} ${f(cy + Math.sin(a) * r0)}L${f(cx + Math.cos(a) * (r0 + l))} ${f(cy + Math.sin(a) * (r0 + l))}`;
    }
    els.push(`<path d="${ticks}" stroke="#1C1D27" stroke-width="3" stroke-linecap="round" opacity="0.75"/>`);
    if (!o.back) {
      // charge ring + core light
      els.push(`<circle cx="${cx}" cy="${cy}" r="152" fill="none" stroke="url(#${ring})" stroke-width="24" opacity="0.55" filter="url(#${blur})"/>`);
      els.push(`<circle cx="${cx}" cy="${cy}" r="152" fill="none" stroke="url(#${ring})" stroke-width="7"/>`);
      els.push(`<circle cx="${cx}" cy="${cy}" r="132" fill="#08080D" opacity="0.55"/>`);
      els.push(`<circle cx="${cx}" cy="${cy}" r="132" fill="url(#${core})"/>`);
      const gs = 196, gx = cx - gs / 2, gy = cy - gs / 2;
      els.push(`<g filter="url(#${blur})" opacity="0.95">${glyphMarkup(t.glyphs, el, gx, gy, gs, glow, 1.7)}</g>`);
      els.push(glyphMarkup(t.glyphs, el, gx, gy, gs, lighten(spec.inlay || glow, 0.35), 1));
    } else {
      // hidden: the Busara sixteen-dot diamond in kinetic purple on the bare weave
      let d = '';
      for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) d += `<circle cx="${cx + (i - j) * 32}" cy="${cy - 48 + (i + j) * 32}" r="12" fill="${kinetic}"/>`;
      els.push(`<g filter="url(#${blur})" opacity="0.8">${d}</g>${d.replace(new RegExp(kinetic, 'g'), '#D9C4FF')}`);
    }
    // spherical shading over the whole bead: the bezel reads as a band round its equator
    const sph = uid(u + 'q');
    defs.push(`<radialGradient id="${sph}" cx="${cx - 60}" cy="${cy - 70}" r="${r * 1.28}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#fff" stop-opacity="0.16"/><stop offset="0.4" stop-color="#fff" stop-opacity="0"/><stop offset="0.78" stop-color="#000" stop-opacity="0.25"/><stop offset="1" stop-color="#000" stop-opacity="0.7"/></radialGradient>`);
    els.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="url(#${sph})"/>`);
    els.push(`<ellipse cx="${cx - 74}" cy="${cy - 96}" rx="62" ry="34" fill="url(#${gloss})" transform="rotate(-28 ${cx - 74} ${cy - 96})"/>`);
    return `<defs>${defs.join('')}</defs>${els.join('')}`;
  }

  // ------------------------------------------------------------ interface kit (Panther Court)
  // Nine-slice friendly UI sprites matching the reference mockups. border = [left, bottom, right, top]
  // in pixels, written to the Unity .meta so Image.Type = Sliced stretches them cleanly.
  const KIT = {
    steel: ['#C3B6EE', '#7E6FB8', '#2F3149', '#5C5F8C', '#A99BD9'],
    gold: '#E9A340', orange: '#FF8A1F', ember: '#E8590C', purple: '#9B5CFF', violet: '#C08BFF',
    leather: '#221D1A', wood: '#2B231E', woodLight: '#5A3A26', slate: '#1C1D2E', ink: '#0F0F16',
  };
  const kitDefs = (u) => {
    const s = KIT.steel;
    return `<linearGradient id="${u}steel" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${s[0]}"/><stop offset="0.25" stop-color="${s[3]}"/><stop offset="0.5" stop-color="${s[2]}"/><stop offset="0.78" stop-color="${s[1]}"/><stop offset="1" stop-color="${s[4]}"/></linearGradient>` +
      `<linearGradient id="${u}gold" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${lighten(KIT.gold, 0.3)}"/><stop offset="1" stop-color="${darken(KIT.gold, 0.2)}"/></linearGradient>` +
      `<linearGradient id="${u}metal" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#F1F2F8"/><stop offset="0.3" stop-color="#8C90A6"/><stop offset="0.55" stop-color="#3A3D4C"/><stop offset="0.8" stop-color="#C9CCDA"/><stop offset="1" stop-color="#474A5A"/></linearGradient>` +
      `<filter id="${u}glow" x="-30%" y="-60%" width="160%" height="220%"><feGaussianBlur stdDeviation="6"/></filter>` +
      `<filter id="${u}soft" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="3"/></filter>`;
  };
  const woodFill = (u, x, y, w, h, base, seed) => {
    const a = noiseLayer(u, { x, y, w, h, freq: '0.004 0.09', oct: 4, seed: seed || 3, table: '0 0.2 0.7 0.1 0 0.4 0.8 0.15 0', color: darken(base, 0.55), op: 0.85 });
    const b = noiseLayer(u, { x, y, w, h, freq: '0.006 0.22', oct: 2, seed: (seed || 3) + 5, table: '0 0 0 0.4 0 0 0.35 0', color: lighten(base, 0.3), op: 0.4 });
    return { defs: a.def + b.def, els: `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${base}"/>${a.el}${b.el}` };
  };
  const leatherFill = (u, x, y, w, h) => {
    const a = noiseLayer(u, { x, y, w, h, freq: '0.35', oct: 3, seed: 21, table: '0 0 0.3 0 0.45 0', color: '#000', op: 0.8 });
    const b = noiseLayer(u, { x, y, w, h, freq: '0.02', oct: 3, seed: 22, table: '0 0.3 0 0.2', color: '#4A3E36', op: 0.5 });
    return { defs: a.def + b.def, els: `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${KIT.leather}"/>${a.el}${b.el}` };
  };
  // outlined triangles alternating up and down, each with an inner echo, like the reference friezes
  const triangleRow = (x0, x1, yTop, yBot, step, stroke, sw) => {
    let d = '';
    const inset = (yBot - yTop) * 0.28;
    for (let x = x0, i = 0; x < x1; x += step / 2, i++) {
      const up = i % 2 === 0;
      const a = up ? `M${x} ${yBot}L${x + step / 2} ${yTop}L${x + step} ${yBot}Z` : `M${x} ${yTop}L${x + step / 2} ${yBot}L${x + step} ${yTop}Z`;
      const ix = step * 0.22;
      const b = up ? `M${x + ix} ${yBot - inset * 0.55}L${x + step / 2} ${yTop + inset}L${x + step - ix} ${yBot - inset * 0.55}Z` : `M${x + ix} ${yTop + inset * 0.55}L${x + step / 2} ${yBot - inset}L${x + step - ix} ${yTop + inset * 0.55}Z`;
      d += a + b;
    }
    return `<path d="${d}" fill="none" stroke="${stroke}" stroke-width="${sw}" stroke-linejoin="round"/>`;
  };
  const roundedPill = (x, y, w, h) => `M${x + h / 2} ${y}H${x + w - h / 2}A${h / 2} ${h / 2} 0 0 1 ${x + w - h / 2} ${y + h}H${x + h / 2}A${h / 2} ${h / 2} 0 0 1 ${x + h / 2} ${y}Z`;

  const UI_KIT = {
    // Iridescent steel device bezel; the centre is transparent so any panel shows through.
    frame_device: { w: 512, h: 512, border: [128, 128, 128, 136], svg: (u) => {
      const ring = 'M16 72Q16 24 64 24H180L200 8H312L332 24H448Q496 24 496 72V440Q496 496 440 496H72Q16 496 16 440Z' + 'M60 92Q60 64 88 64H424Q452 64 452 92V420Q452 452 420 452H92Q60 452 60 420Z';
      return `<path d="${ring}" fill="url(#${u}steel)" fill-rule="evenodd"/>` +
        `<path d="M16 72Q16 24 64 24H180L200 8H312L332 24H448" fill="none" stroke="#fff" stroke-width="2.5" opacity="0.55"/>` +
        `<path d="M60 92Q60 64 88 64H424Q452 64 452 92V420Q452 452 420 452H92Q60 452 60 420Z" fill="none" stroke="#0B0B12" stroke-width="7"/>` +
        `<path d="M60 92Q60 64 88 64H424Q452 64 452 92V420Q452 452 420 452H92Q60 452 60 420Z" fill="none" stroke="${KIT.violet}" stroke-width="1.5" opacity="0.5" transform="translate(0 2)"/>` +
        `<path d="M16 440Q16 496 72 496H440Q496 496 496 440" fill="none" stroke="#000" stroke-width="3" opacity="0.4"/>`;
    } },
    panel_leather: { w: 256, h: 256, border: [48, 48, 48, 48], svg: (u) => {
      const l = leatherFill(u, 0, 0, 256, 256);
      return `<defs><clipPath id="${u}c"><rect x="4" y="4" width="248" height="248" rx="22"/></clipPath>${l.defs}</defs><g clip-path="url(#${u}c)">${l.els}</g>` +
        `<rect x="4" y="4" width="248" height="248" rx="22" fill="none" stroke="#000" stroke-width="3" opacity="0.6"/>` +
        `<rect x="18" y="18" width="220" height="220" rx="14" fill="none" stroke="${KIT.gold}" stroke-width="2.5"/>`;
    } },
    panel_wood: { w: 256, h: 256, border: [40, 40, 40, 40], svg: (u) => {
      const w = woodFill(u, 0, 0, 256, 256, KIT.wood, 4);
      return `<defs><clipPath id="${u}c"><rect x="4" y="4" width="248" height="248" rx="18"/></clipPath>${w.defs}</defs><g clip-path="url(#${u}c)">${w.els}</g>` +
        `<rect x="5" y="5" width="246" height="246" rx="17" fill="none" stroke="#fff" stroke-width="1.5" opacity="0.12"/>` +
        `<rect x="4" y="4" width="248" height="248" rx="18" fill="none" stroke="#000" stroke-width="3" opacity="0.65"/>`;
    } },
    panel_neon: { w: 512, h: 512, border: [104, 104, 104, 104], svg: (u) => {
      const p = 'M64 40H200L216 28H296L312 40H448Q472 40 472 64V200L484 216V296L472 312V448Q472 472 448 472H312L296 484H216L200 472H64Q40 472 40 448V312L28 296V216L40 200V64Q40 40 64 40Z';
      let ticks = '';
      for (let i = 0; i < 4; i++) ticks += `<rect x="${236 + i * 12}" y="490" width="6" height="6" rx="3" fill="${KIT.violet}"/>`;
      return `<rect x="8" y="8" width="496" height="496" rx="40" fill="${KIT.slate}"/>` +
        `<path d="${p}" fill="none" stroke="${KIT.purple}" stroke-width="12" filter="url(#${u}glow)" opacity="0.8"/>` +
        `<path d="${p}" fill="none" stroke="${KIT.violet}" stroke-width="3.5"/>` + ticks;
    } },
    frieze_neon: { w: 2048, h: 160, border: [0, 0, 0, 0], svg: (u) => {
      const g = `<linearGradient id="${u}fg" x1="0" y1="0" x2="2048" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#F5A23A"/><stop offset="0.45" stop-color="#F28A3C"/><stop offset="0.62" stop-color="#C36BE0"/><stop offset="1" stop-color="${KIT.purple}"/></linearGradient>`;
      const row = triangleRow(0, 2048, 28, 132, 168, `url(#${u}fg)`, 7);
      return `<defs>${g}</defs><g filter="url(#${u}glow)" opacity="0.85">${row}</g>${row}` +
        `<path d="M0 12H2048M0 148H2048" stroke="url(#${u}fg)" stroke-width="4" opacity="0.8"/>`;
    } },
    frieze_carved: { w: 1024, h: 128, border: [0, 0, 0, 0], svg: (u) => {
      const w = woodFill(u, 0, 0, 1024, 128, '#3A2C22', 9);
      let tri = '', hi = '';
      for (let x = 0, i = 0; x < 1024; x += 64, i++) {
        const up = i % 2 === 0;
        tri += up ? `M${x} 104L${x + 64} 104L${x + 32} 24Z` : `M${x} 24L${x + 64} 24L${x + 32} 104Z`;
        hi += up ? `M${x + 32} 24L${x} 104` : `M${x} 24L${x + 64} 24`;
      }
      return `<defs>${w.defs}</defs>${w.els}<path d="${tri}" fill="#000" opacity="0.38"/><path d="${tri}" fill="none" stroke="#000" stroke-width="3" opacity="0.5" transform="translate(1.5 2)"/>` +
        `<path d="${hi}" stroke="#E2B98F" stroke-width="2" opacity="0.35"/><path d="M0 8H1024M0 120H1024" stroke="#000" stroke-width="6" opacity="0.5"/><path d="M0 12H1024" stroke="#fff" stroke-width="1.5" opacity="0.15"/>`;
    } },
    button_neon: { w: 640, h: 200, border: [140, 60, 140, 60], svg: (u) => {
      const outer = 'M60 24H580L612 56V136L580 168H60L28 136V56Z';
      let dots = '';
      for (let i = 0; i < 5; i++) dots += `<circle cx="${292 + i * 14}" cy="182" r="4" fill="${KIT.orange}"/>`;
      return `<path d="${outer}" fill="#2A1A14" stroke="${KIT.orange}" stroke-width="8" stroke-linejoin="round"/>` +
        `<path d="M70 40H570L594 64V128L570 152H70L46 128V64Z" fill="none" stroke="${darken(KIT.orange, 0.2)}" stroke-width="2.5"/>` +
        `<path d="M46 76Q34 96 46 116M594 76Q606 96 594 116" fill="none" stroke="${KIT.orange}" stroke-width="3"/>` +
        `<path d="${roundedPill(80, 52, 480, 88)}" fill="${KIT.purple}" opacity="0.7" filter="url(#${u}glow)"/>` +
        `<linearGradient id="${u}pg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#4A4462"/><stop offset="0.5" stop-color="#2A2638"/><stop offset="1" stop-color="#1A1824"/></linearGradient>` +
        `<path d="${roundedPill(80, 52, 480, 88)}" fill="url(#${u}pg)" stroke="${KIT.violet}" stroke-width="5"/>` +
        `<path d="${roundedPill(96, 58, 448, 30)}" fill="#fff" opacity="0.08"/>` +
        `<rect x="270" y="170" width="100" height="24" rx="6" fill="#2A1A14" stroke="${KIT.orange}" stroke-width="3"/>` + dots;
    } },
    button_glow: { w: 640, h: 180, border: [150, 60, 150, 60], svg: (u) => {
      const cradle = 'M8 120Q90 120 120 64Q140 24 190 24H450Q500 24 520 64Q550 120 632 120V172H8Z';
      const caps = 'M150 128Q160 48 210 44H430Q480 48 490 128Q492 150 470 150H170Q148 150 150 128Z';
      return `<path d="${cradle}" fill="url(#${u}steel)"/><path d="${cradle}" fill="none" stroke="#000" stroke-width="3" opacity="0.4"/>` +
        `<path d="${caps}" fill="${KIT.orange}" filter="url(#${u}glow)" opacity="0.9"/>` +
        `<radialGradient id="${u}og" cx="0.5" cy="0.35" r="0.75"><stop offset="0" stop-color="#FFC266"/><stop offset="0.55" stop-color="${KIT.orange}"/><stop offset="1" stop-color="#8A2A05"/></radialGradient>` +
        `<path d="${caps}" fill="url(#${u}og)" stroke="#2A1206" stroke-width="4"/>` +
        `<path d="M176 70Q190 54 214 54H426Q450 54 464 70" fill="none" stroke="#fff" stroke-width="5" opacity="0.45" stroke-linecap="round"/>`;
    } },
    button_pill: { w: 320, h: 112, border: [56, 40, 56, 40], svg: (u) => {
      return `<linearGradient id="${u}bp" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#55505E"/><stop offset="1" stop-color="#2A2730"/></linearGradient>` +
        `<path d="${roundedPill(8, 14, 304, 88)}" fill="#000" opacity="0.45" transform="translate(0 4)"/>` +
        `<path d="${roundedPill(8, 10, 304, 88)}" fill="url(#${u}bp)"/>` +
        `<path d="${roundedPill(20, 14, 280, 30)}" fill="#fff" opacity="0.1"/>`;
    } },
    button_outline: { w: 320, h: 112, border: [56, 40, 56, 40], svg: (u) =>
      `<path d="${roundedPill(10, 12, 300, 88)}" fill="#2A211C"/><path d="${roundedPill(10, 12, 300, 88)}" fill="none" stroke="url(#${u}gold)" stroke-width="5"/>` +
      `<path d="${roundedPill(18, 18, 284, 28)}" fill="#fff" opacity="0.05"/>` },
    button_ring: { w: 256, h: 256, border: [0, 0, 0, 0], svg: (u) =>
      `<circle cx="128" cy="134" r="112" fill="#000" opacity="0.5" filter="url(#${u}soft)"/>` +
      `<circle cx="128" cy="128" r="112" fill="url(#${u}metal)"/>` +
      `<circle cx="128" cy="128" r="84" fill="#141419" stroke="#000" stroke-width="5"/>` +
      `<radialGradient id="${u}rg" cx="0.45" cy="0.35"><stop offset="0" stop-color="#2E2D38"/><stop offset="1" stop-color="#0E0E13"/></radialGradient>` +
      `<circle cx="128" cy="128" r="80" fill="url(#${u}rg)"/>` +
      `<path d="M50 96A84 84 0 0 1 160 46" fill="none" stroke="#fff" stroke-width="3" opacity="0.5"/>` },
    knob_power: { w: 384, h: 384, border: [0, 0, 0, 0], svg: (u) => {
      const arc = (r, a0, a1) => { const p = (a) => `${f(192 + Math.cos(a) * r)} ${f(192 + Math.sin(a) * r)}`; return `M${p(a0)}A${r} ${r} 0 ${a1 - a0 > Math.PI ? 1 : 0} 1 ${p(a1)}`; };
      const a0 = Math.PI * 0.62, a1 = Math.PI * 2.32;
      return `<linearGradient id="${u}ka" x1="0" y1="1" x2="1" y2="0"><stop offset="0" stop-color="#FFC266"/><stop offset="1" stop-color="${KIT.ember}"/></linearGradient>` +
        `<path d="${arc(172, a0, a1)}" fill="none" stroke="${KIT.orange}" stroke-width="22" stroke-linecap="round" filter="url(#${u}glow)" opacity="0.7"/>` +
        `<path d="${arc(172, a0, a1)}" fill="none" stroke="url(#${u}ka)" stroke-width="14" stroke-linecap="round"/>` +
        `<circle cx="192" cy="200" r="132" fill="#000" opacity="0.5" filter="url(#${u}soft)"/>` +
        `<circle cx="192" cy="192" r="132" fill="url(#${u}metal)"/>` +
        `<circle cx="192" cy="192" r="100" fill="#121217" stroke="#000" stroke-width="6"/>` +
        `<path d="M192 64V88" stroke="#fff" stroke-width="5" stroke-linecap="round" opacity="0.8"/>`;
    } },
    tile_avatar: { w: 256, h: 256, border: [0, 0, 0, 0], svg: (u) => {
      const w = woodFill(u, 0, 0, 256, 256, KIT.woodLight, 13);
      return `<defs><clipPath id="${u}c"><rect x="12" y="12" width="232" height="232" rx="30"/></clipPath>${w.defs}</defs>` +
        `<rect x="12" y="20" width="232" height="232" rx="30" fill="#000" opacity="0.5"/>` +
        `<g clip-path="url(#${u}c)">${w.els}</g><rect x="13" y="13" width="230" height="230" rx="29" fill="none" stroke="#fff" stroke-width="2" opacity="0.2"/>` +
        `<circle cx="128" cy="116" r="78" fill="#3A2C26"/><circle cx="128" cy="116" r="78" fill="none" stroke="${KIT.orange}" stroke-width="8"/>` +
        `<circle cx="128" cy="98" r="26" fill="#7B655B"/><path d="M80 176Q84 132 128 132Q172 132 176 176Q160 190 128 190Q96 190 80 176Z" fill="#7B655B"/>`;
    } },
    tile_avatar_idle: { w: 256, h: 256, border: [0, 0, 0, 0], svg: (u) => UI_KIT.tile_avatar.svg(u).replace(new RegExp(`stroke="${KIT.orange}" stroke-width="8"`), 'stroke="#8C90A6" stroke-width="8"').replace(new RegExp(KIT.woodLight, 'g'), '#4A3A30') },
    card_kingdom: { w: 640, h: 320, border: [90, 60, 90, 80], svg: (u) => {
      const w = woodFill(u, 0, 0, 640, 320, '#3A2A20', 17);
      return `<defs><clipPath id="${u}c"><rect x="8" y="24" width="624" height="288" rx="34"/></clipPath>${w.defs}</defs>` +
        `<g clip-path="url(#${u}c)">${w.els}</g>` +
        `<rect x="8" y="24" width="624" height="288" rx="34" fill="none" stroke="#000" stroke-width="3" opacity="0.6"/>` +
        `<rect x="20" y="36" width="600" height="264" rx="26" fill="none" stroke="url(#${u}gold)" stroke-width="4"/>`;
    } },
    label_tab: { w: 320, h: 96, border: [44, 36, 44, 36], svg: (u) =>
      `<rect x="8" y="10" width="304" height="76" rx="18" fill="#1E1A17"/><rect x="8" y="10" width="304" height="76" rx="18" fill="none" stroke="url(#${u}gold)" stroke-width="5"/>` +
      `<rect x="16" y="16" width="288" height="20" rx="10" fill="#fff" opacity="0.05"/>` },
    inset_dark: { w: 256, h: 256, border: [40, 40, 40, 40], svg: (u) =>
      `<rect x="6" y="6" width="244" height="244" rx="18" fill="#121117"/>` +
      `<rect x="6" y="6" width="244" height="244" rx="18" fill="none" stroke="#000" stroke-width="8" opacity="0.7" filter="url(#${u}soft)"/>` +
      `<path d="M24 250H232" stroke="#fff" stroke-width="2" opacity="0.12"/>` },
    chip_virtue: { w: 320, h: 96, border: [0, 0, 0, 0], svg: (u) =>
      `<rect x="54" y="22" width="200" height="52" rx="26" fill="#121219" stroke="#2C2C3A" stroke-width="2"/>` +
      `<circle cx="50" cy="48" r="38" fill="url(#${u}metal)"/><circle cx="50" cy="48" r="26" fill="#1A1A22" stroke="#000" stroke-width="3"/>` +
      `<path d="M270 40L284 24L298 40ZM270 56L284 72L298 56Z" fill="#B9BCCB"/>` },
    icon_add: { w: 128, h: 128, border: [0, 0, 0, 0], svg: (u) =>
      `<rect x="14" y="14" width="100" height="100" rx="14" fill="none" stroke="${KIT.gold}" stroke-width="10" filter="url(#${u}glow)" opacity="0.6"/>` +
      `<rect x="14" y="14" width="100" height="100" rx="14" fill="#1A1410" stroke="url(#${u}gold)" stroke-width="6"/>` +
      `<path d="M64 36V92M36 64H92" stroke="url(#${u}gold)" stroke-width="9" stroke-linecap="round"/>` },
    dots_kinetic: { w: 512, h: 48, border: [0, 0, 0, 0], svg: (u) => {
      let d = '';
      for (let i = 0; i < 9; i++) d += `<circle cx="${32 + i * 56}" cy="24" r="${i % 2 ? 6 : 10}" fill="${KIT.violet}"/>`;
      return `<g filter="url(#${u}glow)">${d}</g>${d}`;
    } },
    ornament_diamonds: { w: 64, h: 256, border: [0, 0, 0, 0], svg: (u) => {
      const dia = (y, s) => `<path d="M32 ${y - s}L${32 + s} ${y}L32 ${y + s}L${32 - s} ${y}Z" fill="#1A1410" stroke="url(#${u}gold)" stroke-width="3.5"/>`;
      return `<path d="M32 0V96M32 160V256" stroke="${KIT.gold}" stroke-width="3"/>${dia(108, 16)}${dia(128, 12)}${dia(148, 16)}`;
    } },
  };
  function uiKitSVG(name) {
    const k = UI_KIT[name], u = uid('ui');
    return svgWrap(k.w, k.h, `<defs>${kitDefs(u)}</defs>${k.svg(u)}`);
  }

  // ------------------------------------------------------------ sprite list
  // Everything export-sprites.mjs writes for one theme. size = output pixels.
  function spriteList(themeId) {
    const t = themeById(themeId);
    const list = [
      { file: 'board.png', size: 1024, svg: () => boardSVG(t) },
      { file: 'board_turn_glow.png', size: 1024, svg: () => boardGlowSVG(t) },
    ];
    // Piece sprites keep the footprint 256 wide and grow upward with the shape's height.
    const ph = Math.round(256 * pieceAspect(t));
    ELEMENTS.forEach((el) => list.push(t.raster
      ? { file: `piece_${el}.png`, size: 256, raster: t.raster[el] }
      : { file: `piece_${el}.png`, w: 256, h: ph, svg: () => pieceSVG(t, el) }));
    list.push({ file: 'piece_hidden.png', w: 256, h: ph, svg: () => pieceSVG(t, 'earth', { back: true }) });
    Object.keys(PIECE_STATES).forEach((s) => list.push({ file: `piece_state_${s}.png`, size: 256, svg: () => pieceStateSVG(t, s) }));
    Object.keys(SLOT_STATES).forEach((s) => list.push({ file: `slot_state_${s}.png`, size: 256, svg: () => slotStateSVG(t, s) }));
    if (t.uiKit) Object.entries(UI_KIT).forEach(([name, k]) => list.push({ file: `ui/${name}.png`, w: k.w, h: k.h, border: k.border, svg: () => uiKitSVG(name) }));
    return list;
  }

  const api = {
    ELEMENTS, ELEMENT_INFO, GLYPHS, MATERIALS, THEMES, SLOT_STYLES, SLOT_STATES, PIECE_STATES, BOARD, FRIEZES,
    CLASSIC, ALL_THEMES, UI_KIT, uiKitSVG, PIECE_SHAPES, pieceAspect, piecePivotY, themeShape, themeById, boardSVG, boardGlowSVG, pieceSVG, holeSVG, materialSVG, slotStateSVG, pieceStateSVG, spriteList,
    color: { mix, lighten, darken, luminance },
  };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.BusaraThemes = api;
})(typeof window !== 'undefined' ? window : globalThis);
