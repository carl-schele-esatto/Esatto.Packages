import { defineConfig } from 'vite';

// The backoffice dashboard, built into the package's wwwroot. public/umbraco-package.json is copied in.
export default defineConfig({
  build: {
    lib: {
      entry: { 'seo-dashboard': 'src/seo-dashboard.element.ts' },
      formats: ['es'],
      // Must match the element path in public/umbraco-package.json.
      fileName: (_format, entryName) => `${entryName}.js`,
    },
    outDir: '../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Seo',
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: { external: [/^@umbraco/] },
  },
});
