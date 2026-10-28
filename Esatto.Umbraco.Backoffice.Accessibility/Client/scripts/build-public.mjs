// Builds the public-site scripts, one Vite build per entry. A single multi-entry build would split the
// shared prefs.ts into an imported chunk, but a11y-boot.js must run as a classic synchronous <script>
// in <head> (before first paint), so every file has to be self-contained: IIFE, no imports.
import { build } from 'vite';
import { copyFile, mkdir, rm } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const outDir = path.resolve(root, '../wwwroot/esatto-accessibility');

const entries = {
  'a11y-boot': 'public-site/a11y-boot.ts',
  'a11y-panel': 'public-site/a11y-panel.ts',
};

// Start clean; copy-vendor.mjs runs afterwards and refills fonts/.
await rm(outDir, { recursive: true, force: true });
await mkdir(outDir, { recursive: true });

for (const [name, entry] of Object.entries(entries)) {
  await build({
    root,
    configFile: false,
    publicDir: false, // Client/public holds the backoffice manifest, not public-site files
    logLevel: 'warn',
    build: {
      lib: { entry: path.resolve(root, entry), name: name.replace('-', '_'), formats: ['iife'], fileName: () => `${name}.js` },
      outDir,
      emptyOutDir: false,
      sourcemap: false,
      minify: true,
    },
  });
}

await copyFile(path.resolve(root, 'public-site/a11y.css'), path.resolve(outDir, 'a11y.css'));
console.log('public-site scripts built →', outDir);
