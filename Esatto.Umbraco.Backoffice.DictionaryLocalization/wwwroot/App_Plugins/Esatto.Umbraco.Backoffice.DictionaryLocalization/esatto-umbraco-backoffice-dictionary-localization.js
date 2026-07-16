import { UmbLocalizationController as u, umbLocalizationManager as f } from "@umbraco-cms/backoffice/localization-api";
import { umbHttpClient as p } from "@umbraco-cms/backoffice/http-client";
const d = 100, g = /* @__PURE__ */ new Set(["ar", "he", "fa", "ur", "ps", "syr", "dv"]);
function h(e) {
  return /[.\-]/.test(e) ? e.replace(/[.\-]/g, "_") : null;
}
function m(e) {
  const n = e.split(/[-_]/)[0]?.toLowerCase() ?? "";
  return g.has(n) ? "rtl" : "ltr";
}
function y(e) {
  const n = [];
  for (const [t, o] of Object.entries(e.cultures ?? {})) {
    const i = {
      $code: t,
      $dir: m(t),
      $weight: d
    };
    for (const [r, s] of Object.entries(o ?? {})) {
      if (typeof s != "string" || s.length === 0)
        continue;
      i[r] = s;
      const a = h(r);
      a && a !== r && (i[a] = s);
    }
    n.push(i);
  }
  return n;
}
let c = !1;
async function L(e, n) {
  if (c)
    return !1;
  const { data: t, error: o } = await n();
  if (o || !t)
    return console.warn("[Esatto.DictionaryLocalization] fetch failed; #Key labels will not resolve until next reload.", o), !1;
  const r = y(t);
  return e.registerManyLocalizations(r), c = !0, !0;
}
const b = "/umbraco/management/api/v1/backoffice/dictionary-localization/all";
function T() {
  return p.get({
    security: [{ scheme: "bearer", type: "http" }],
    url: b
  });
}
const w = /#\w[\w.-]*/g;
function z(e) {
  const n = [];
  let t = e;
  for (; t.length > 0; ) {
    n.push(t);
    const o = Math.max(t.lastIndexOf("."), t.lastIndexOf("-"));
    if (o <= 0) break;
    t = t.slice(0, o);
  }
  return n;
}
function A(e, n) {
  return typeof e != "string" ? "" : e.replace(w, (t) => {
    const o = t.slice(1);
    for (const i of z(o)) {
      const r = n(i);
      if (r !== null)
        return r + o.slice(i.length);
    }
    return t;
  });
}
const l = "__esattoDottedTokenPatch";
function I(e) {
  const n = e.prototype;
  n[l] || (n.string = function(t, ...o) {
    return A(t, (i) => {
      const r = this.term(i, ...o);
      return r === i ? null : r;
    });
  }, n[l] = !0);
}
const _ = async () => {
  I(u), await L(
    f,
    T
  );
};
export {
  _ as onInit
};
//# sourceMappingURL=esatto-umbraco-backoffice-dictionary-localization.js.map
