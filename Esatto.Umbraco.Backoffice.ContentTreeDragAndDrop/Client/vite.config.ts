import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/index.ts", // side-effecting backofficeEntryPoint
      formats: ["es"],
      // Must match the path the manifest references:
      // /App_Plugins/Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/content-tree-drag-drop.js
      fileName: "content-tree-drag-drop",
    },
    outDir: "../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop", // built assets land here
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      // Keep @umbraco-cms/* as bare specifiers so they resolve against
      // Umbraco's runtime import map (never bundled).
      external: [/^@umbraco/],
    },
  },
});
