import { defineConfig } from 'vite';

// The backoffice dashboard and its English texts, built into the package's wwwroot.
// public/umbraco-package.json is copied in afterwards.
export default defineConfig({
  build: {
    lib: {
      entry: {
        'build-dashboard': 'src/build-dashboard.element.ts',
        'lang/en': 'src/lang/en.ts',
      },
      formats: ['es'],
      // Must match the js paths in public/umbraco-package.json.
      fileName: (_format, entryName) => `${entryName}.js`,
    },
    outDir: '../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Build',
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: { external: [/^@umbraco/] },
  },
});
