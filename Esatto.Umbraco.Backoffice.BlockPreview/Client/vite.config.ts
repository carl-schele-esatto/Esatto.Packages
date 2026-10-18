import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: { "block-preview-frame": "src/block-preview-frame.element.ts" },
      formats: ["es"],
      // Must match the element path the C# manifest reader emits:
      // /App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview/block-preview-frame.js
      fileName: (_format, entryName) => `${entryName}.js`,
    },
    outDir: "../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview",
    emptyOutDir: true, // public/ (legacy elements, lib/, umbraco-package.json) is copied in afterwards
    sourcemap: true,
    rollupOptions: { external: [/^@umbraco/] },
  },
});
