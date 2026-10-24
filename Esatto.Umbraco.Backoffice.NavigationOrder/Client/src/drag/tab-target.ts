import { membersOf } from '../core/groups';
import type { Bar, ManagedManifest, TabType } from '../core/types';

// Recognising Umbraco's tabs from an event's composedPath(). This is the part that depends on the
// backoffice's markup (verified on 18.2.1):
//   top bar     <uui-tab-group id="tabs"> <uui-tab data-mark="section-link:<alias>" href="section/<pathname>">
//   section     <uui-tab-group id="dashboards"> <uui-tab href="<router>/section/<pathname>/dashboard/<path>">
//               <uui-tab-group id="views">      <uui-tab href="<router>/section/<pathname>/view/<path>">
// A dashboard's or view's <path> is meta.pathname, or pathFolderName(meta.label ?? name ?? alias).

/** A node of a composed path: an element, or a shadow root (no localName). */
export interface PathNode {
  localName?: string;
  id?: string;
  getAttribute?(name: string): string | null;
}

export interface TabTarget {
  tab: PathNode;
  /** The tab's <uui-tab-group>: its children are the bar's tabs in display order. */
  group: PathNode;
  bar: Bar;
  alias: string;
}

const SECTION_LINK = 'section-link:';
const TAB_HREF = /\/section\/([^/]+)\/(dashboard|view)\/([^/?#]+)\/?$/;
const GROUP_TYPES: Record<string, TabType> = { dashboards: 'dashboard', views: 'sectionView' };

/** The tab an event happened on, which bar it belongs to and its manifest alias; null when it isn't one of ours. */
export function findTabTarget(path: readonly PathNode[], manifests: readonly ManagedManifest[], folderName: (name: string) => string): TabTarget | null {
  const tabIndex = path.findIndex((n) => n.localName === 'uui-tab');
  if (tabIndex < 0) return null;
  const tab = path[tabIndex];
  const group = path.slice(tabIndex + 1).find((n) => n.localName === 'uui-tab-group');
  if (!group) return null;

  if (group.id === 'tabs') {
    const mark = tab.getAttribute?.('data-mark') ?? '';
    const alias = mark.startsWith(SECTION_LINK) ? mark.slice(SECTION_LINK.length) : '';
    return manifests.some((m) => m.type === 'section' && m.alias === alias) ? { tab, group, bar: { kind: 'sections' }, alias } : null;
  }

  const type = group.id ? GROUP_TYPES[group.id] : undefined;
  const match = TAB_HREF.exec(tab.getAttribute?.('href') ?? '');
  if (!type || !match || (match[2] === 'dashboard') !== (type === 'dashboard')) return null;

  const sectionPath = decodeURIComponent(match[1]);
  const section = manifests.find((m) => m.type === 'section' && m.meta?.pathname === sectionPath);
  if (!section) return null;

  const bar: Bar = { kind: 'tabs', section: section.alias, type };
  const tabPath = decodeURIComponent(match[3]);
  const manifest = membersOf(manifests, bar).find((m) => (m.meta?.pathname || folderName(m.meta?.label ?? m.name ?? m.alias)) === tabPath);
  return manifest ? { tab, group, bar, alias: manifest.alias } : null;
}

const sameBar = (a: Bar, b: Bar) =>
  a.kind === 'sections' ? b.kind === 'sections' : b.kind === 'tabs' && a.section === b.section && a.type === b.type;

/**
 * What a dragover means. 'ignore': not our drag. 'move': another tab of the dragged tab's bar.
 * 'none': our drag anywhere else — still claimed (preventDefault), or the browser would open the
 * dragged tab's link and reload the backoffice when it's dropped there.
 */
export function dragOverEffect(source: TabTarget | undefined, target: TabTarget | null): 'ignore' | 'none' | 'move' {
  if (!source) return 'ignore';
  return target && target.alias !== source.alias && sameBar(target.bar, source.bar) ? 'move' : 'none';
}

/** Dropping on the left half of a tab places before it, on the right half after it. */
export function dropPosition(clientX: number, rect: { left: number; width: number }): 'before' | 'after' {
  return clientX < rect.left + rect.width / 2 ? 'before' : 'after';
}

/** Alt+Shift+ArrowLeft → -1, Alt+Shift+ArrowRight → 1, anything else → 0. */
export function keyboardStep(event: { key: string; altKey: boolean; shiftKey: boolean; ctrlKey: boolean; metaKey: boolean }): -1 | 0 | 1 {
  if (!event.altKey || !event.shiftKey || event.ctrlKey || event.metaKey) return 0;
  return event.key === 'ArrowLeft' ? -1 : event.key === 'ArrowRight' ? 1 : 0;
}
