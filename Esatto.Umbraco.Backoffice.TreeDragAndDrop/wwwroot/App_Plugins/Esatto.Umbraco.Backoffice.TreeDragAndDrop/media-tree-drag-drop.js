import { a as m, r as i, b as l } from "./core-BZVFXrQ_.js";
import { MediaService as o } from "@umbraco-cms/backoffice/external/backend-api";
import { c as n } from "./core-By19G-8s.js";
const y = {
  entityType: "media",
  rootEntityType: "media-root",
  innerTag: "umb-media-tree-item",
  move(t, e, r) {
    return i(t, o.putMediaByIdMove({
      path: { id: e },
      body: { target: r ? { id: r } : null }
    }));
  },
  sort(t, e, r) {
    return i(t, o.putMediaSort({
      body: {
        parent: e ? { id: e } : null,
        sorting: r.map((d, a) => ({ id: d, sortOrder: a }))
      }
    }));
  },
  childOrder(t, e) {
    return n((r, d) => m(t, e ? o.getTreeMediaChildren({ query: { parentId: e, skip: r, take: d } }) : o.getTreeMediaRoot({ query: { skip: r, take: d } })));
  }
};
l(y);
//# sourceMappingURL=media-tree-drag-drop.js.map
