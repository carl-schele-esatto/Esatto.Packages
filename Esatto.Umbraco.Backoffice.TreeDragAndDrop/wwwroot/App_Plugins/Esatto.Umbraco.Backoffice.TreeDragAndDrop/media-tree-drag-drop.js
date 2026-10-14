import { r as i, a as m } from "./core-CrszW97q.js";
import { MediaService as o } from "@umbraco-cms/backoffice/external/backend-api";
const n = {
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
        sorting: r.map((a, d) => ({ id: a, sortOrder: d }))
      }
    }));
  }
};
m(n);
//# sourceMappingURL=media-tree-drag-drop.js.map
