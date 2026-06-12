import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts", // Bundle registers one or more manifests
      formats: ["es"],
      fileName: "esatto-umbraco-backoffice-customeditors",
    },
    outDir: "../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.CustomEditors", // built assets land here
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
