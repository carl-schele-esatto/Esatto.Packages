import { manifests as propertyEditors } from "./manifest.js";

// Collate all manifests for this package. Loaded from umbraco-package.json as a bundle.
export const manifests: Array<UmbExtensionManifest> = [
  ...propertyEditors,
];
