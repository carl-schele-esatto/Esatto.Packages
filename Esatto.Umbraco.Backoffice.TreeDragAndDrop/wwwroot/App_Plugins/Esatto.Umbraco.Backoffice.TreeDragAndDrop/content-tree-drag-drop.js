import { r as m, a as u, b as c } from "./core-B7lI1kZv.js";
import { DocumentService as n } from "@umbraco-cms/backoffice/external/backend-api";
import { c as i } from "./core-By19G-8s.js";
const l = {
  entityType: "document",
  rootEntityType: "document-root",
  innerTag: "umb-document-tree-item",
  move(r, t, e) {
    return m(r, n.putDocumentByIdMove({
      path: { id: t },
      body: { target: e ? { id: e } : null }
    }));
  },
  sort(r, t, e) {
    return m(r, n.putDocumentSort({
      body: {
        parent: t ? { id: t } : null,
        sorting: e.map((o, d) => ({ id: o, sortOrder: d }))
      }
    }));
  },
  childOrder(r, t) {
    return i((e, o) => u(r, t ? n.getTreeDocumentChildren({ query: { parentId: t, skip: e, take: o } }) : n.getTreeDocumentRoot({ query: { skip: e, take: o } })));
  }
};
c(l);
//# sourceMappingURL=content-tree-drag-drop.js.map
