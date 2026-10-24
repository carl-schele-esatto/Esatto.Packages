import { membersOf } from './groups';
import type { Bar, ManagedManifest, NavigationOrderData, TabType, WeightLookup } from './types';

const TAB_TYPES: readonly TabType[] = ['dashboard', 'sectionView'];

/** Umbraco's own order: highest original weight first, registration order on ties. */
export function defaultOrder(members: readonly ManagedManifest[], original: WeightLookup): string[] {
  return members
    .map((m, index) => ({ alias: m.alias, weight: original(m.alias), index }))
    .sort((a, b) => b.weight - a.weight || a.index - b.index)
    .map((m) => m.alias);
}

/**
 * The order a bar shows: the saved aliases that exist, in saved order, then every other member in
 * default order. Saved aliases that don't exist are skipped (and stay saved, for when they return).
 */
export function effectiveOrder(members: readonly ManagedManifest[], saved: readonly string[] | null | undefined, original: WeightLookup): string[] {
  const existing = new Set(members.map((m) => m.alias));
  const first = [...new Set(saved ?? [])].filter((alias) => existing.has(alias));
  const placed = new Set(first);
  return [...first, ...defaultOrder(members, original).filter((alias) => !placed.has(alias))];
}

/** The saved list for a bar; a section's dashboards and views share one list. Null when nothing is saved. */
export function savedListOf(order: NavigationOrderData, bar: Bar): string[] | null {
  if (bar.kind === 'sections') return order.sections;
  return order.tabs.find((tab) => tab.section === bar.section)?.aliases ?? null;
}

/**
 * The weight every manifest should have. Members of a saved list get (n - index) * 10; everything
 * else its original weight. A tab in several sections is weighed by the first saved list containing
 * it, taking sections in top-bar order.
 */
export function targetWeights(manifests: readonly ManagedManifest[], order: NavigationOrderData, original: WeightLookup): Map<string, number> {
  const weights = new Map<string, number>();
  const assign = (list: string[], overwrite: boolean) =>
    list.forEach((alias, index) => {
      if (overwrite || !weights.has(alias)) weights.set(alias, (list.length - index) * 10);
    });

  const headerOrder = effectiveOrder(membersOf(manifests, { kind: 'sections' }), order.sections, original);
  if (order.sections) assign(headerOrder, true);

  for (const section of headerOrder) {
    const saved = savedListOf(order, { kind: 'tabs', section, type: 'dashboard' });
    if (!saved) continue;
    for (const type of TAB_TYPES) {
      assign(effectiveOrder(membersOf(manifests, { kind: 'tabs', section, type }), saved, original), false);
    }
  }

  for (const manifest of manifests) {
    if (!weights.has(manifest.alias)) weights.set(manifest.alias, original(manifest.alias));
  }
  return weights;
}

/** `list` with `alias` moved before or after `target`; an unchanged copy when either is missing or they're the same. */
export function moveAlias(list: readonly string[], alias: string, target: string, position: 'before' | 'after'): string[] {
  if (alias === target || !list.includes(alias) || !list.includes(target)) return [...list];
  const without = list.filter((a) => a !== alias);
  const at = without.indexOf(target) + (position === 'after' ? 1 : 0);
  return [...without.slice(0, at), alias, ...without.slice(at)];
}

/**
 * `next` with every alias of `previous` that it lacks (an uninstalled extension) put back right after
 * the alias it followed in `previous`, or first when it led. So a package that's reinstalled later
 * returns to its saved place.
 */
export function keepUninstalled(previous: readonly string[] | null, next: readonly string[]): string[] {
  const result = [...next];
  if (!previous) return result;
  previous.forEach((alias, index) => {
    if (result.includes(alias)) return;
    const after = index === 0 ? -1 : result.indexOf(previous[index - 1]);
    result.splice(after + 1, 0, alias);
  });
  return result;
}

/**
 * A new order with one bar's list replaced. `list` (and `otherTypeList`) are full effective lists, so
 * a saved alias missing from them belongs to an uninstalled extension and is kept in place. A section's
 * tab list is stored dashboards first, then views, so the caller passes the other tab type's current list.
 */
export function withList(order: NavigationOrderData, bar: Bar, list: string[], otherTypeList: string[] = []): NavigationOrderData {
  if (bar.kind === 'sections') return { ...order, sections: keepUninstalled(order.sections, list) };

  const aliases = keepUninstalled(savedListOf(order, bar), bar.type === 'dashboard' ? [...list, ...otherTypeList] : [...otherTypeList, ...list]);
  const index = order.tabs.findIndex((tab) => tab.section === bar.section);
  const tabs = order.tabs.map((tab) => ({ ...tab, aliases: [...tab.aliases] }));
  if (index >= 0) tabs[index] = { section: bar.section, aliases };
  else tabs.push({ section: bar.section, aliases });
  return { ...order, tabs };
}
