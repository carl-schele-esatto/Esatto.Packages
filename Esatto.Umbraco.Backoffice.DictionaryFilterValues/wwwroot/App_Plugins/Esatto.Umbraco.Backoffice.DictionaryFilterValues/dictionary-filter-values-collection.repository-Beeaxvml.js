import { UmbRepositoryBase as s } from "@umbraco-cms/backoffice/repository";
import { tryExecute as i } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient as c } from "@umbraco-cms/backoffice/http-client";
const l = "/umbraco/management/api/v1/backoffice/dictionary-filter-values";
function u(t) {
  return c.get({
    security: [{ scheme: "bearer", type: "http" }],
    url: l,
    query: t
  });
}
const p = "dictionary";
function m(t) {
  return {
    entityType: p,
    unique: t.id,
    name: t.name ?? "",
    parentUnique: t.parent ? t.parent.id : null,
    translatedIsoCodes: t.translatedIsoCodes ?? []
  };
}
class y {
  #t;
  constructor(e) {
    this.#t = e;
  }
  async getCollection(e) {
    const { data: r, error: n } = await i(this.#t, u(e));
    if (r) {
      const o = r, a = (o.items ?? []).map(m);
      return { data: { items: a, total: o.total ?? a.length } };
    }
    return { error: n };
  }
}
class h extends s {
  #t;
  constructor(e) {
    super(e), this.#t = new y(e);
  }
  async requestCollection(e) {
    return this.#t.getCollection(e);
  }
}
export {
  h as EsattoDictionaryFilterValuesCollectionRepository,
  h as default
};
//# sourceMappingURL=dictionary-filter-values-collection.repository-Beeaxvml.js.map
