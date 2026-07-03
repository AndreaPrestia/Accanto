/**
 * Genera i PNG finali per store + app dagli SVG sorgenti in mobile/assets/.
 *
 * Uso:
 *   cd mobile
 *   npm install --save-dev sharp    # una tantum
 *   node scripts/gen-icons.mjs
 *
 * Output (rigenerato ad ogni run, sostituisce eventuali placeholder):
 *   assets/icon.png              1024x1024  (App Store master + fallback Android legacy)
 *   assets/adaptive-icon.png     1024x1024  (Android adaptive foreground, sfondo #f8fafc)
 *   assets/notification-icon.png   96x96    (Android notification, monocromatico bianco)
 *   assets/splash.png            1284x2778  (splash portrait, logo centrato su #f8fafc)
 *   assets/favicon.png             48x48    (solo web preview Expo)
 */

import sharp from 'sharp';
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const assets = resolve(here, '..', 'assets');

async function svgToPng(svgPath, outPath, size, options = {}) {
  const svg = await readFile(svgPath);
  const pipeline = sharp(svg, { density: 384 })
    .resize({
      width: size.width,
      height: size.height,
      fit: 'contain',
      background: options.background ?? { r: 0, g: 0, b: 0, alpha: 0 }
    })
    .png({ compressionLevel: 9 });
  const buf = await pipeline.toBuffer();
  await writeFile(outPath, buf);
  console.log(`  ok  ${outPath.replace(assets + '\\', '').replace(assets + '/', '')}  ${size.width}x${size.height}`);
}

async function buildSplash(sourceSvgPath, outPath) {
  // Splash: canvas 1284x2778 tinta #f8fafc, logo centrato al ~40% larghezza.
  const svg = await readFile(sourceSvgPath);
  const logoSize = 520; // ~40% di 1284
  const logo = await sharp(svg, { density: 384 })
    .resize({ width: logoSize, height: logoSize, fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png()
    .toBuffer();

  const canvas = sharp({
    create: {
      width: 1284,
      height: 2778,
      channels: 4,
      background: { r: 248, g: 250, b: 252, alpha: 1 } // #f8fafc (accanto-50)
    }
  });

  const composed = await canvas
    .composite([{ input: logo, gravity: 'center' }])
    .png({ compressionLevel: 9 })
    .toBuffer();

  await writeFile(outPath, composed);
  console.log(`  ok  splash.png  1284x2778`);
}

async function main() {
  console.log('Generating PNG assets from SVG sources...');

  // Icona master iOS + fallback Android: fondo pieno #475569 (già nel SVG), no trasparenza.
  await svgToPng(
    resolve(assets, 'icon-source.svg'),
    resolve(assets, 'icon.png'),
    { width: 1024, height: 1024 },
    { background: { r: 71, g: 85, b: 105, alpha: 1 } }
  );

  // Android adaptive foreground: canvas trasparente, il colore di sfondo lo mette app.config.ts.
  await svgToPng(
    resolve(assets, 'adaptive-icon-source.svg'),
    resolve(assets, 'adaptive-icon.png'),
    { width: 1024, height: 1024 },
    { background: { r: 0, g: 0, b: 0, alpha: 0 } }
  );

  // Notification icon Android: 96x96 bianco su trasparente.
  await svgToPng(
    resolve(assets, 'notification-icon-source.svg'),
    resolve(assets, 'notification-icon.png'),
    { width: 96, height: 96 },
    { background: { r: 0, g: 0, b: 0, alpha: 0 } }
  );

  // Favicon web preview.
  await svgToPng(
    resolve(assets, 'icon-source.svg'),
    resolve(assets, 'favicon.png'),
    { width: 48, height: 48 },
    { background: { r: 71, g: 85, b: 105, alpha: 1 } }
  );

  // Splash portrait 1284x2778.
  await buildSplash(resolve(assets, 'icon-source.svg'), resolve(assets, 'splash.png'));

  console.log('Done.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
