const r = "Umb.Collection.Dictionary", o = "Esatto.Repository.DictionaryFilterValues.Collection", u = {
  type: "repository",
  alias: o,
  name: "Esatto Dictionary Filter Values Collection Repository",
  api: () => import("./dictionary-filter-values-collection.repository-Beeaxvml.js")
};
let i;
function f(t) {
  if (i)
    return !0;
  const e = t.getByAlias(r);
  return e ? (e.meta?.repositoryAlias === o || (t.isRegistered(o) || t.register(u), i = e, t.unregister(r), t.register({
    ...e,
    meta: { ...e.meta, repositoryAlias: o }
  })), !0) : !1;
}
function p(t) {
  i && (t.isRegistered(r) && t.unregister(r), t.register(i), t.isRegistered(o) && t.unregister(o), i = void 0);
}
const R = (t, e) => {
  const a = e;
  let n, s = !1;
  const l = () => {
    s && n && n.unsubscribe();
  };
  n = e.byAlias(r).subscribe((c) => {
    s || !c || (s = f(a), l());
  }), l();
}, A = (t, e) => {
  p(e);
};
export {
  R as onInit,
  A as onUnload
};
//# sourceMappingURL=esatto-umbraco-backoffice-dictionary-filter-values.js.map
