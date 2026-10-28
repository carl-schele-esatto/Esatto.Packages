// Copies third-party files the package ships unmodified, with their licences:
// - axe-core (MPL-2.0) next to the dashboard, injected into the scanned pages' frames;
// - Atkinson Hyperlegible (OFL-1.1) for the visitor panel's "readable font" option.
import { copyFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const modules = path.resolve(root, 'node_modules');
const appPlugins = path.resolve(root, '../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility');
const fonts = path.resolve(root, '../wwwroot/esatto-accessibility/fonts');

await mkdir(appPlugins, { recursive: true });
await mkdir(fonts, { recursive: true });

await copyFile(path.join(modules, 'axe-core/axe.min.js'), path.join(appPlugins, 'axe.min.js'));
await copyFile(path.join(modules, 'axe-core/LICENSE'), path.join(appPlugins, 'axe-core.LICENSE.txt'));

const fontDir = path.join(modules, '@fontsource/atkinson-hyperlegible');
for (const weight of [400, 700]) {
  const file = `atkinson-hyperlegible-latin-${weight}-normal.woff2`;
  await copyFile(path.join(fontDir, 'files', file), path.join(fonts, file));
}
await copyFile(path.join(fontDir, 'LICENSE'), path.join(fonts, 'atkinson-hyperlegible.LICENSE.txt'));

console.log('vendor files copied');
