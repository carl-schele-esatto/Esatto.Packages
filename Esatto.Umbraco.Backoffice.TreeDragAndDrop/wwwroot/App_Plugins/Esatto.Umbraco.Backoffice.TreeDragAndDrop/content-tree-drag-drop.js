import { r, a as d } from "./core-CrszW97q.js";
import { DocumentService as n } from "@umbraco-cms/backoffice/external/backend-api";
const i = {
  entityType: "document",
  rootEntityType: "document-root",
  innerTag: "umb-document-tree-item",
  move(o, t, e) {
    return r(o, n.putDocumentByIdMove({
      path: { id: t },
      body: { target: e ? { id: e } : null }
    }));
  },
  sort(o, t, e) {
    return r(o, n.putDocumentSort({
      body: {
        parent: t ? { id: t } : null,
        sorting: e.map((m, u) => ({ id: m, sortOrder: u }))
      }
    }));
  }
};
d(i);
//# sourceMappingURL=content-tree-drag-drop.js.map
