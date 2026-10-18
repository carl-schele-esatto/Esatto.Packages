import { r as e, b as o } from "./core-B7lI1kZv.js";
import { DictionaryService as a } from "@umbraco-cms/backoffice/external/backend-api";
const n = {
  entityType: "dictionary",
  // The dictionary menu item sets hideTreeRoot: true, so no row carries this
  // entity type; it is only used for top-level reload events.
  rootEntityType: "dictionary-root",
  // The dictionary tree registers its tree items with kind: 'default'.
  innerTag: "umb-default-tree-item",
  move(r, i, t) {
    return e(r, a.putDictionaryByIdMove({
      path: { id: i },
      body: { target: t ? { id: t } : null }
    }));
  }
};
o(n);
//# sourceMappingURL=dictionary-tree-drag-drop.js.map
