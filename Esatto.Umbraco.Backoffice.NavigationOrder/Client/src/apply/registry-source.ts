import { umbExtensionsRegistry } from '@umbraco-cms/backoffice/extension-registry';
import { MANAGED_TYPES, type ManagedManifest } from '../core/types';
import type { ManifestSource } from './applier';
import { withKindWeight } from './kind-weight';

const isManaged = (manifest: { type: string }): boolean => (MANAGED_TYPES as readonly string[]).includes(manifest.type);

/**
 * The applier's view of umbExtensionsRegistry. `extensions` gives the raw manifests (no kind merged
 * in), which is what must be registered again. A weight change needs unregister + register: the bars'
 * initializers keep one controller per alias and only re-sort when an extension comes or goes; both
 * changes land in one animation frame, so the bar redraws once.
 */
export function createRegistrySource(): ManifestSource {
  let latest: ManagedManifest[] = [];
  const listeners = new Set<() => void>();

  umbExtensionsRegistry.extensions.subscribe((manifests) => {
    latest = (manifests as unknown as ManagedManifest[])
      .filter(isManaged)
      .map((m) => (m.kind ? withKindWeight(m, umbExtensionsRegistry.getByAlias(m.alias) as { weight?: number } | undefined) : m));
    listeners.forEach((listener) => listener());
  });

  return {
    current: () => latest,
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    replace(manifest, weight) {
      umbExtensionsRegistry.unregister(manifest.alias);
      umbExtensionsRegistry.register({ ...manifest, weight } as unknown as Parameters<typeof umbExtensionsRegistry.register>[0]);
    },
  };
}
