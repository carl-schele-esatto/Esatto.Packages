// Content (Umb.Section.Content) tree adapter — documents can be ordered.

import { DocumentService } from '@umbraco-cms/backoffice/external/backend-api';
import type { TreeAdapter } from '../core/types';
import { run } from './run';

export const contentAdapter: TreeAdapter = {
  entityType: 'document',
  rootEntityType: 'document-root',
  innerTag: 'umb-document-tree-item',

  move(host, unique, parentUnique) {
    return run(host, DocumentService.putDocumentByIdMove({
      path: { id: unique },
      body: { target: parentUnique ? { id: parentUnique } : null },
    }));
  },

  sort(host, parentUnique, orderedUniques) {
    return run(host, DocumentService.putDocumentSort({
      body: {
        parent: parentUnique ? { id: parentUnique } : null,
        sorting: orderedUniques.map((id, sortOrder) => ({ id, sortOrder })),
      },
    }));
  },
};
