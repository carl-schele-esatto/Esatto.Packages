import { defineConfig } from 'vite';

// The backoffice dashboard. The public-site scripts are built separately by scripts/build-public.mjs,
// and axe-core + fonts are copied in by scripts/copy-vendor.mjs (both run after this build, because
// emptyOutDir wipes the folder first).
export default defineConfig({
  build: {
    lib: {
      entry: { 'accessibility-dashboard': 'src/accessibility-dashboard.element.ts' },
      formats: ['es'],
      // Must match the element path in public/umbraco-package.json.
      fileName: (_format, entryName) => `${entryName}.js`,
    },
    outDir: '../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility',
    emptyOutDir: true, // public/umbraco-package.json is copied in afterwards
    sourcemap: true,
    rollupOptions: { external: [/^@umbraco/] },
  },
});
