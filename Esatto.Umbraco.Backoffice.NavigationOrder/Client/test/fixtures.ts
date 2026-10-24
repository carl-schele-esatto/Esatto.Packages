import { SECTION_ALIAS_CONDITION } from '../src/core/groups';
import type { ManagedManifest, WeightLookup } from '../src/core/types';

export const section = (alias: string, weight?: number, pathname = alias.toLowerCase()): ManagedManifest => ({
  type: 'section',
  alias,
  weight,
  meta: { label: alias, pathname },
});

/** A dashboard in the given section(s); null = no section condition (shown in every section). */
export const dashboard = (alias: string, sections: string | string[] | null, weight?: number, meta: ManagedManifest['meta'] = { label: alias }): ManagedManifest => ({
  type: 'dashboard',
  alias,
  weight,
  meta,
  conditions: sections === null ? [] : [Array.isArray(sections) ? { alias: SECTION_ALIAS_CONDITION, oneOf: sections } : { alias: SECTION_ALIAS_CONDITION, match: sections }],
});

export const sectionView = (alias: string, section: string, weight?: number, meta: ManagedManifest['meta'] = { label: alias }): ManagedManifest => ({
  ...dashboard(alias, section, weight, meta),
  type: 'sectionView',
});

/** Original weights as registered (undefined counts as 0). */
export const originalOf = (manifests: ManagedManifest[]): WeightLookup => (alias) => manifests.find((m) => m.alias === alias)?.weight ?? 0;
