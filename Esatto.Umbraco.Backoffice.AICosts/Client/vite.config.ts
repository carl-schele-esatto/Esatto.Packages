import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    lib: {
      entry: { 'ai-costs-view': 'src/ai-costs-view.element.ts' },
      formats: ['es'],
      // Must match the element path in public/umbraco-package.json.
      fileName: (_format, entryName) => `${entryName}.js`,
    },
    outDir: '../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.AICosts',
    emptyOutDir: true, // public/umbraco-package.json is copied in afterwards
    sourcemap: true,
    rollupOptions: { external: [/^@umbraco/] },
  },
});
