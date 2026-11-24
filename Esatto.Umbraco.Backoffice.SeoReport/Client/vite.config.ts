import { defineConfig } from 'vitest/config';

// The backoffice dashboard and its English texts, built into the package's wwwroot. public/umbraco-package.json is copied in.
export default defineConfig({
  test: {
    server: {
      // localize.umbraco.test.ts runs Umbraco's own localization controller, which has to go through the same module pipeline as the
      // test so that the two modules it reaches for (lit's unsafeHTML, the core utils) can be replaced by stand-ins there.
      deps: { inline: [/@umbraco-cms\/backoffice\/dist-cms\/libs\/localization-api/] },
    },
  },
  build: {
    lib: {
      entry: {
        'seo-report-dashboard': 'src/seo-report-dashboard.element.ts',
        'lang/en': 'src/lang/en.ts',
      },
      formats: ['es'],
      // Must match the element and js paths in public/umbraco-package.json.
      fileName: (_format, entryName) => `${entryName}.js`,
    },
    outDir: '../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.SeoReport',
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
      // No content hash in chunk names: the lazy chart chunk is always chart-bundle.js, so a rebuild never leaves the package
      // pointing at a file name that no longer exists (NU5019). Umbraco serves App_Plugins without fingerprints anyway.
      output: { chunkFileNames: '[name].js' },
    },
  },
});
