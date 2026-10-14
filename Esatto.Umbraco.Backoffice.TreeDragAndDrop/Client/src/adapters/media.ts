// Media (Umb.Section.Media) tree adapter — media items can be ordered.

import { MediaService } from '@umbraco-cms/backoffice/external/backend-api';
import type { TreeAdapter } from '../core/types';
import { run } from './run';

export const mediaAdapter: TreeAdapter = {
  entityType: 'media',
  rootEntityType: 'media-root',
  innerTag: 'umb-media-tree-item',

  move(host, unique, parentUnique) {
    return run(host, MediaService.putMediaByIdMove({
      path: { id: unique },
      body: { target: parentUnique ? { id: parentUnique } : null },
    }));
  },

  sort(host, parentUnique, orderedUniques) {
    return run(host, MediaService.putMediaSort({
      body: {
        parent: parentUnique ? { id: parentUnique } : null,
        sorting: orderedUniques.map((id, sortOrder) => ({ id, sortOrder })),
      },
    }));
  },
};
