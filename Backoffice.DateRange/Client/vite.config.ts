import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts", // Bundle registers one or more manifests
      formats: ["es"],
      fileName: "backoffice-date-range",
    },
    outDir: "../wwwroot/App_Plugins/Backoffice.DateRange", // built assets land here
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
