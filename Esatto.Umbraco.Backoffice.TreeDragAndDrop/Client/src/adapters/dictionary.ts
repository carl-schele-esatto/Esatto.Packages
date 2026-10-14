// Dictionary (Translation section) tree adapter.
//
// Dictionary items have no sort order: the Management API has a move endpoint
// but no sort endpoint, and the tree always lists siblings alphabetically. So
// there is no `sort` here — the core treats a row's top/bottom edge as "move
// next to this item" (under its parent) instead of "insert above/below".

import { DictionaryService } from '@umbraco-cms/backoffice/external/backend-api';
import type { TreeAdapter } from '../core/types';
import { run } from './run';

export const dictionaryAdapter: TreeAdapter = {
  entityType: 'dictionary',
  // The dictionary menu item sets hideTreeRoot: true, so no row carries this
  // entity type; it is only used for top-level reload events.
  rootEntityType: 'dictionary-root',
  // The dictionary tree registers its tree items with kind: 'default'.
  innerTag: 'umb-default-tree-item',

  move(host, unique, parentUnique) {
    return run(host, DictionaryService.putDictionaryByIdMove({
      path: { id: unique },
      body: { target: parentUnique ? { id: parentUnique } : null },
    }));
  },
};
