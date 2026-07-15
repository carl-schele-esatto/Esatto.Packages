import { umbLocalizationManager as l } from "@umbraco-cms/backoffice/localization-api";
import { umbHttpClient as u } from "@umbraco-cms/backoffice/http-client";
const f = 100, p = /* @__PURE__ */ new Set(["ar", "he", "fa", "ur", "ps", "syr", "dv"]);
function g(t) {
  return /[.\-]/.test(t) ? t.replace(/[.\-]/g, "_") : null;
}
function m(t) {
  const e = t.split(/[-_]/)[0]?.toLowerCase() ?? "";
  return p.has(e) ? "rtl" : "ltr";
}
function y(t) {
  const e = [];
  for (const [n, r] of Object.entries(t.cultures ?? {})) {
    const i = {
      $code: n,
      $dir: m(n),
      $weight: f
    };
    for (const [o, a] of Object.entries(r ?? {})) {
      if (typeof a != "string" || a.length === 0)
        continue;
      i[o] = a;
      const s = g(o);
      s && s !== o && (i[s] = a);
    }
    e.push(i);
  }
  return e;
}
let c = !1;
async function d(t, e) {
  if (c)
    return !1;
  const { data: n, error: r } = await e();
  if (r || !n)
    return console.warn("[Esatto.DictionaryLocalization] fetch failed; #Key labels will not resolve until next reload.", r), !1;
  const o = y(n);
  return t.registerManyLocalizations(o), c = !0, !0;
}
const L = "/umbraco/management/api/v1/backoffice/dictionary-localization/all";
function h() {
  return u.get({
    security: [{ scheme: "bearer", type: "http" }],
    url: L
  });
}
const w = async () => {
  await d(
    l,
    h
  );
};
export {
  w as onInit
};
//# sourceMappingURL=esatto-umbraco-backoffice-dictionary-localization.js.map
