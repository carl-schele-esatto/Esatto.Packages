import { describe, expect, it } from 'vitest';
import { NavigationOrderApplier, type ManifestSource } from '../src/apply/applier';
import { EMPTY_ORDER, type ManagedManifest } from '../src/core/types';
import { dashboard, section } from './fixtures';

/** A registry stand-in: `replace` unregisters and re-registers like umbExtensionsRegistry, notifying after each step. */
function fakeRegistry(initial: ManagedManifest[]) {
  let manifests = [...initial];
  const listeners = new Set<() => void>();
  const replaced: { alias: string; weight: number }[] = [];
  const notify = () => listeners.forEach((listener) => listener());
  const source: ManifestSource = {
    current: () => manifests,
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    replace(manifest, weight) {
      replaced.push({ alias: manifest.alias, weight });
      manifests = manifests.filter((m) => m.alias !== manifest.alias);
      notify();
      manifests = [...manifests, { ...manifest, weight }];
      notify();
    },
  };
  return {
    source,
    replaced,
    notify,
    add(manifest: ManagedManifest) {
      manifests = [...manifests, manifest];
      notify();
    },
    weightOf: (alias: string) => manifests.find((m) => m.alias === alias)?.weight,
  };
}

const settle = () => Promise.resolve();

describe('NavigationOrderApplier', () => {
  it('captures the original weights before changing anything', () => {
    const registry = fakeRegistry([section('A', 1000), section('B', 900)]);
    const applier = new NavigationOrderApplier(registry.source);
    applier.start();

    applier.setOrder({ sections: ['B', 'A'], tabs: [] });

    expect(registry.replaced).toEqual([
      { alias: 'A', weight: 10 },
      { alias: 'B', weight: 20 },
    ]);
    expect(applier.originalWeight('A')).toBe(1000);
    expect(applier.originalWeight('B')).toBe(900);
  });

  it('applies the order to manifests registered later', async () => {
    const registry = fakeRegistry([section('A', 1000), section('B', 900)]);
    const applier = new NavigationOrderApplier(registry.source);
    applier.start();
    applier.setOrder({ sections: ['B', 'A'], tabs: [] });

    registry.add(section('C', 5000));
    await settle();

    expect(registry.weightOf('C')).toBe(10);
    expect(applier.originalWeight('C')).toBe(5000);
    expect(applier.effectiveList({ kind: 'sections' })).toEqual(['B', 'A', 'C']);
  });

  it('settles: nothing is re-registered when nothing changed', async () => {
    const registry = fakeRegistry([section('A', 1000), section('B', 900), section('AI'), dashboard('All', null, 5)]);
    const applier = new NavigationOrderApplier(registry.source);
    applier.start();
    applier.setOrder({ sections: ['B', 'A', 'AI'], tabs: [{ section: 'A', aliases: ['All'] }] });
    const count = registry.replaced.length;

    registry.notify();
    await settle();
    applier.setOrder(applier.order);

    expect(registry.replaced.length).toBe(count);
  });

  it('leaves a manifest alone when its weight already matches', () => {
    const registry = fakeRegistry([section('A', 30), section('B', 20), section('AI')]);
    const applier = new NavigationOrderApplier(registry.source);
    applier.start();

    applier.setOrder(EMPTY_ORDER); // AI has no weight: that counts as its original 0
    applier.setOrder({ sections: ['A', 'B', 'AI'], tabs: [] }); // A and B already weigh 30 and 20

    expect(registry.replaced).toEqual([{ alias: 'AI', weight: 10 }]);
  });

  it('restores the original weights when the order is cleared', () => {
    const registry = fakeRegistry([section('A', 1000), section('B', 900)]);
    const applier = new NavigationOrderApplier(registry.source);
    applier.start();
    applier.setOrder({ sections: ['B', 'A'], tabs: [] });

    applier.setOrder(EMPTY_ORDER);

    expect(registry.weightOf('A')).toBe(1000);
    expect(registry.weightOf('B')).toBe(900);
  });

  it('lists every member of a bar, whether or not the user can see it', () => {
    const registry = fakeRegistry([section('Content', 1000), section('Hidden', 950), section('Forms', 0)]);
    const applier = new NavigationOrderApplier(registry.source);
    applier.start();

    expect(applier.effectiveList({ kind: 'sections' })).toEqual(['Content', 'Hidden', 'Forms']);
  });

  it('does not start a nested apply from the notifications its own changes cause', async () => {
    const registry = fakeRegistry([section('A', 1000), section('B', 900), section('C', 800)]);
    const applier = new NavigationOrderApplier(registry.source);
    applier.start();

    applier.setOrder({ sections: ['C', 'B', 'A'], tabs: [] });
    await settle();

    expect(registry.replaced.map((r) => r.alias)).toEqual(['A', 'B', 'C']);
  });
});
