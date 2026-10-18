// The one seam between the generic tree DnD core and a specific tree.

import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';

export interface TreeAdapter {
  /** Entity type reflected on <umb-tree-item entitytype="…">. */
  entityType: string;
  /** Entity type of the (hidden) tree root, used for top-level reload events. */
  rootEntityType: string;
  /** Inner kind-element <umb-tree-item> renders; used to locate the visible row. */
  innerTag: string;
  /** Re-parent `unique` under `parentUnique` (null = root). Throws on API error. */
  move(host: UmbControllerHost, unique: string, parentUnique: string | null): Promise<void>;
  /**
   * Persist a child order. Only for trees whose children can be ordered
   * (Content, Media). Absent for Dictionary, so row edges mean "next to".
   */
  sort?(host: UmbControllerHost, parentUnique: string | null, orderedUniques: string[]): Promise<void>;
  /**
   * The parent's FULL child order from the server (null = root). Required with
   * `sort`: the sort endpoint treats each sortOrder as an absolute index, and
   * the sidebar may only have rendered a window of a large parent.
   */
  childOrder?(host: UmbControllerHost, parentUnique: string | null): Promise<string[]>;
}
