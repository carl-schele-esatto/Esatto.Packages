import type { Bar, ManagedManifest } from './types';

export const SECTION_ALIAS_CONDITION = 'Umb.Condition.SectionAlias';

/** The sections a dashboard or section view appears in, from its SectionAlias conditions; 'all' without one. */
export function sectionsOfTab(manifest: ManagedManifest): string[] | 'all' {
  const sections = new Set<string>();
  for (const condition of manifest.conditions ?? []) {
    if (condition.alias !== SECTION_ALIAS_CONDITION) continue;
    if (condition.match) sections.add(condition.match);
    for (const alias of condition.oneOf ?? []) sections.add(alias);
  }
  return sections.size > 0 ? [...sections] : 'all';
}

/** The manifests a bar orders, in registration order. */
export function membersOf(manifests: readonly ManagedManifest[], bar: Bar): ManagedManifest[] {
  if (bar.kind === 'sections') return manifests.filter((m) => m.type === 'section');
  return manifests.filter((m) => {
    if (m.type !== bar.type) return false;
    const sections = sectionsOfTab(m);
    return sections === 'all' || sections.includes(bar.section);
  });
}
