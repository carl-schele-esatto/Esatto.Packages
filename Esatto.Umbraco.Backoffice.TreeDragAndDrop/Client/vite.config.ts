import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      // One side-effecting backofficeEntryPoint per tree. Shared core code is
      // emitted as a chunk that every entry imports by the same URL, so the
      // browser evaluates it once: one attachShadow patch, one host.
      entry: {
        dictionary: "src/entries/dictionary.ts",
      },
      formats: ["es"],
      // Must match the js path the C# manifest reader emits:
      // /App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop/<tree>-tree-drag-drop.js
      fileName: (_format, entryName) => `${entryName}-tree-drag-drop.js`,
    },
    outDir: "../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      // Keep @umbraco-cms/* as bare specifiers so they resolve against
      // Umbraco's runtime import map (never bundled).
      external: [/^@umbraco/],
      output: { chunkFileNames: "core-[hash].js" },
    },
  },
});
