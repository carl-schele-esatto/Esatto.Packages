// Shapes shared by the ordering core, the applier and the drag code. Kept free of runtime
// @umbraco-cms imports so vitest can load them without the backoffice.

export type ManagedType = 'section' | 'dashboard' | 'sectionView';
export type TabType = Exclude<ManagedType, 'section'>;

export const MANAGED_TYPES: readonly ManagedType[] = ['section', 'dashboard', 'sectionView'];

export interface ConditionConfig {
  alias: string;
  match?: string;
  oneOf?: string[];
}

/** The parts of a section/dashboard/sectionView manifest this package reads. The rest is carried along untouched. */
export interface ManagedManifest {
  type: ManagedType;
  alias: string;
  name?: string;
  weight?: number;
  conditions?: ConditionConfig[];
  meta?: { label?: string; pathname?: string };
  [key: string]: unknown;
}

/** One section's saved tab order: dashboard and section-view aliases, first to last. */
export interface TabOrder {
  section: string;
  aliases: string[];
}

/** The saved order, as the API sends it. `sections: null` = Umbraco's default top-bar order. */
export interface NavigationOrderData {
  sections: string[] | null;
  tabs: TabOrder[];
}

export const EMPTY_ORDER: NavigationOrderData = { sections: null, tabs: [] };

/** One orderable bar: the top bar, or one tab group (dashboards or section views) of a section. */
export type Bar = { kind: 'sections' } | { kind: 'tabs'; section: string; type: TabType };

/** A manifest's original weight (before this package changed it). */
export type WeightLookup = (alias: string) => number;
