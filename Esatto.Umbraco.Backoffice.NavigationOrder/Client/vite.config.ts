import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    lib: {
      entry: {
        'navigation-order': 'src/entry-point.ts',
        'lang/en': 'src/lang/en.ts',
        'lang/sv': 'src/lang/sv.ts',
      },
      formats: ['es'],
      // Must match the js paths in public/umbraco-package.json.
      fileName: (_format, entryName) => `${entryName}.js`,
    },
    outDir: '../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.NavigationOrder',
    emptyOutDir: true, // public/umbraco-package.json is copied in afterwards
    sourcemap: true,
    rollupOptions: { external: [/^@umbraco/] },
  },
});
