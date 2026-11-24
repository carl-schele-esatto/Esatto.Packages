import { css as W, state as w, customElement as K, html as n, nothing as l } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as V } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as Y } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient as G } from "@umbraco-cms/backoffice/http-client";
const q = "/umbraco/management/api/v1/esatto/build-info", J = () => [{ scheme: "bearer", type: "http" }], Q = () => G.get({ url: q, security: J() }), X = /^\{(\d+)\}$/;
function c(t, e) {
  return t.split(/(\{\d+\})/).flatMap((i) => {
    if (!i) return [];
    const o = X.exec(i)?.[1];
    return o !== void 0 && Number(o) < e.length ? [e[Number(o)]] : [i];
  });
}
const Z = "—", T = 6e4, $ = 60 * T, z = 24 * $, tt = 7, b = new Intl.RelativeTimeFormat("en", { numeric: "auto" });
function E(t, e) {
  return new Intl.DateTimeFormat(e, { dateStyle: "medium", timeStyle: "short" }).format(new Date(t));
}
function k(t, e = {}) {
  if (!t?.trim()) return Z;
  const i = new Date(t);
  return Number.isNaN(i.getTime()) ? t : `${E(t, e.locale)} (${et(i, e.now)})`;
}
function et(t, e = /* @__PURE__ */ new Date()) {
  const i = t.getTime() - e.getTime(), o = Math.abs(i);
  return o < $ ? b.format(Math.trunc(i / T), "minute") : o < 2 * z ? b.format(Math.trunc(i / $), "hour") : b.format(Math.trunc(i / z), "day");
}
function _(t) {
  return t.slice(0, tt);
}
const it = /^[0-9a-f]{7,40}$/i;
function M(t) {
  const e = t?.indexOf("+") ?? -1;
  if (!t || e < 0) return null;
  const i = t.slice(e + 1);
  return it.test(i) ? i : null;
}
function nt(t) {
  if (!t) return null;
  try {
    const e = new URL(t);
    return e.protocol === "http:" || e.protocol === "https:" ? e.href : null;
  } catch {
    return null;
  }
}
function C(t) {
  return t.pipeline ? "pipeline" : t.local ? "local" : "unknown";
}
const f = (t) => t?.trim() ? t : null;
function y(t) {
  const e = f(t.sinceBuildNumber);
  if (e) return { type: "build", number: e, url: f(t.sinceBuildUrl) };
  const i = f(t.sinceCommit);
  return i ? { type: "commit", sha: i } : null;
}
function at(t) {
  const e = t.commits.length;
  return f(t.sinceCommit) ? e === 0 ? { kind: "none", since: y(t) } : { kind: "some", count: e, truncated: t.truncated, since: y(t) } : { kind: "noPrevious", count: e };
}
function st(t, e, i) {
  return {
    running: !!i && t.sha.startsWith(i),
    notPushed: e.unpushedCommits.includes(t.sha)
  };
}
var ot = Object.defineProperty, rt = Object.getOwnPropertyDescriptor, N = (t) => {
  throw TypeError(t);
}, B = (t, e, i, o) => {
  for (var r = o > 1 ? void 0 : o ? rt(e, i) : e, h = t.length - 1, m; h >= 0; h--)
    (m = t[h]) && (r = (o ? m(e, i, r) : m(r)) || r);
  return o && r && ot(e, i, r), r;
}, lt = (t, e, i) => e.has(t) || N("Cannot " + i), ut = (t, e, i) => e.has(t) ? N("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, i), s = (t, e, i) => (lt(t, e, "access private method"), i), a, D, S, g, U, P, A, O, R, j, v, L, F, x, I;
const ct = "https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.Build", H = "build-info.json", dt = "—", u = (t) => n`<code>${t}</code>`;
function p(t, e) {
  const i = nt(t);
  return i ? n`<a href=${i} target="_blank" rel="noopener">${e}</a>` : e;
}
let d = class extends V {
  constructor() {
    super(...arguments), ut(this, a), this._info = null, this._failed = !1;
  }
  connectedCallback() {
    super.connectedCallback(), s(this, a, D).call(this);
  }
  render() {
    if (this._failed)
      return n`<uui-box headline=${this.localize.term("esattoBuild_title")}><p class="error">${this.localize.term("esattoBuild_loadFailed")}</p></uui-box>`;
    if (!this._info) return n`<uui-loader></uui-loader>`;
    const t = this._info;
    return n`
      ${s(this, a, S).call(this, t)} ${s(this, a, O).call(this, t)}
      <div class="columns">
        <uui-box headline=${this.localize.term("esattoBuild_environmentTitle")}>
          <dl>
            <dt>${this.localize.term("esattoBuild_environment")}</dt><dd>${t.environment}</dd>
            <dt>${this.localize.term("esattoBuild_instance")}</dt><dd>${u(t.instance)}</dd>
            <dt>${this.localize.term("esattoBuild_runningSince")}</dt><dd>${k(t.runningSinceUtc)}</dd>
            <dt>${this.localize.term("esattoBuild_umbraco")}</dt><dd>${t.umbracoVersion}</dd>
          </dl>
        </uui-box>
        <uui-box headline=${this.localize.term("esattoBuild_packagesTitle")}>
          <dl>
            ${t.packages.map((e) => n`<dt>${e.name}</dt><dd>${e.version ?? dt}</dd>`)}
          </dl>
        </uui-box>
      </div>
    `;
  }
};
a = /* @__PURE__ */ new WeakSet();
D = async function() {
  const { data: t, error: e } = await Y(this, Q(), { disableNotifications: !0 });
  if (e || !t) {
    this._failed = !0;
    return;
  }
  this._failed = !1, this._info = t;
};
S = function(t) {
  switch (C(t)) {
    case "pipeline":
      return s(this, a, U).call(this, t.pipeline);
    case "local":
      return s(this, a, P).call(this, t.local, M(t.appVersion));
    default:
      return s(this, a, A).call(this);
  }
};
g = function(t, e) {
  return n`
      <div class="headline">
        <span class="label">${this.localize.term("esattoBuild_deployedBuild")}</span>
        ${t}
        ${e ? n`<uui-tag look="secondary" title=${this.localize.term("esattoBuild_branchTitle")}>${this.localize.term("esattoBuild_branch", e)}</uui-tag>` : l}
      </div>
    `;
};
U = function(t) {
  return n`
      <uui-box>
        ${s(this, a, g).call(this, p(t.buildUrl, n`<span class="number">${t.buildNumber}</span>`), t.branch)}
        <dl>
          <dt>${this.localize.term("esattoBuild_commit")}</dt>
          <dd>
            ${p(t.commitUrl, u(_(t.commit)))}${t.commitMessage ? n` <span class="message">${t.commitMessage}</span>` : l}
          </dd>
          <dt>${this.localize.term("esattoBuild_built")}</dt><dd>${k(t.builtAtUtc)}</dd>
          ${t.targetEnvironment ? n`<dt>${this.localize.term("esattoBuild_pipelineTarget")}</dt><dd>${t.targetEnvironment}</dd>` : l}
        </dl>
      </uui-box>
    `;
};
P = function(t, e) {
  return n`
      <uui-box>
        ${s(this, a, g).call(this, n`<span class="number">${this.localize.term("esattoBuild_localBuild")}</span>`, t.branch)}
        <p>${c(this.localize.term("esattoBuild_localNotDeployed"), [u(H)])}</p>
        ${e ? n`<dl><dt>${this.localize.term("esattoBuild_builtFrom")}</dt><dd>${u(_(e))}</dd></dl>` : l}
      </uui-box>
    `;
};
A = function() {
  return n`
      <uui-box>
        ${s(this, a, g).call(this, n`<span class="number">${this.localize.term("esattoBuild_unknownBuild")}</span>`)}
        <p>
          ${c(this.localize.term("esattoBuild_unknownNotFound"), [
    u(H),
    p(ct, this.localize.term("esattoBuild_readmeLink"))
  ])}
        </p>
      </uui-box>
    `;
};
O = function(t) {
  switch (C(t)) {
    case "pipeline":
      return t.pipeline.changes ? s(this, a, R).call(this, t.pipeline.changes) : l;
    case "local":
      return s(this, a, L).call(this, t.local, M(t.appVersion));
    default:
      return l;
  }
};
R = function(t) {
  return n`
      <uui-box headline=${this.localize.term("esattoBuild_changesTitle")} class="changes">
        <p class="summary">${s(this, a, j).call(this, at(t))}</p>
        ${s(this, a, x).call(this, t.commits)}
      </uui-box>
    `;
};
j = function(t) {
  switch (t.kind) {
    case "noPrevious":
      return this.localize.term("esattoBuild_changesNoPrevious", t.count);
    case "none":
      return c(this.localize.term("esattoBuild_changesNone"), [s(this, a, v).call(this, t.since)]);
    case "some":
      return c(this.localize.term("esattoBuild_changesSome", t.count, t.truncated), [s(this, a, v).call(this, t.since)]);
  }
};
v = function(t) {
  return t ? t.type === "build" ? c(this.localize.term("esattoBuild_sinceBuild"), [p(t.url, t.number)]) : c(this.localize.term("esattoBuild_sinceCommit"), [u(_(t.sha))]) : l;
};
L = function(t, e) {
  const i = this.localize.term("esattoBuild_localSummary", t.commits.length, !!t.branch, t.unpushedCommits.length, t.uncommittedFiles);
  return n`
      <uui-box headline=${this.localize.term("esattoBuild_localChangesTitle")} class="changes">
        <p class="summary">${c(i, [u(t.branch ?? "")])}</p>
        ${s(this, a, x).call(this, t.commits, (o) => s(this, a, F).call(this, o, t, e))}
      </uui-box>
    `;
};
F = function(t, e, i) {
  const { running: o, notPushed: r } = st(t, e, i), h = o ? n` <uui-tag look="primary">${this.localize.term("esattoBuild_tagRunning")}</uui-tag>` : l, m = r ? n` <uui-tag look="outline">${this.localize.term("esattoBuild_tagNotPushed")}</uui-tag>` : l;
  return n`${h}${m}`;
};
x = function(t, e) {
  return t.length ? n`<ol>${t.map((i) => s(this, a, I).call(this, i, e?.(i)))}</ol>` : l;
};
I = function(t, e = l) {
  const i = t.author ? ` · ${t.author}` : "", o = t.dateUtc ? ` · ${E(t.dateUtc)}` : "", r = n`<span class="meta">${p(t.url, u(_(t.sha)))}${i}${o}${e}</span>`;
  return n`
      <li>
        ${t.body?.trim() ? n`<details>
              <summary><span class="subject">${t.subject}</span>${r}</summary>
              <p class="body">${t.body}</p>
            </details>` : n`<div class="subject-row"><span class="subject">${t.subject}</span>${r}</div>`}
      </li>
    `;
};
d.styles = [
  W`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      uui-box + uui-box,
      uui-box + .columns {
        margin-top: var(--uui-size-layout-1);
      }

      /* The boxes in the column row sit side by side: no top margin, or the second one would sit lower. */
      .columns > uui-box {
        margin-top: 0;
      }

      .summary {
        margin-top: 0;
      }

      .changes ol {
        list-style: none;
        margin: 0;
        padding: 0;
      }

      .changes li {
        padding: var(--uui-size-3) 0;
        border-top: 1px solid var(--uui-color-divider);
        /* A very long subject without spaces (a URL, a hash) would otherwise run out of the box. */
        overflow-wrap: anywhere;
      }

      .changes summary {
        cursor: pointer;
      }

      .subject {
        font-weight: 700;
      }

      .subject + .meta {
        display: block;
        margin-top: var(--uui-size-1);
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size, 0.875rem);
      }

      /* The commit body is plain text with line breaks. */
      .body {
        margin: var(--uui-size-3) 0 0;
        white-space: pre-line;
        max-width: 80ch;
      }

      .columns {
        display: grid;
        gap: var(--uui-size-layout-1);
        grid-template-columns: repeat(auto-fit, minmax(20rem, 1fr));
      }

      .headline {
        display: flex;
        flex-wrap: wrap;
        align-items: baseline;
        gap: var(--uui-size-3);
        margin-bottom: var(--uui-size-5);
      }

      .label {
        color: var(--uui-color-text-alt);
      }

      .number {
        font-size: var(--uui-type-h3-size, 1.5rem);
        font-weight: 700;
        color: var(--uui-color-text);
      }

      a {
        color: var(--uui-color-interactive);
      }
      a:hover {
        color: var(--uui-color-interactive-emphasis);
      }

      dl {
        display: grid;
        grid-template-columns: max-content 1fr;
        gap: var(--uui-size-2) var(--uui-size-6);
        margin: 0;
      }

      dt {
        color: var(--uui-color-text-alt);
      }

      dd {
        margin: 0;
        overflow-wrap: anywhere;
      }

      .message {
        color: var(--uui-color-text-alt);
      }

      .error {
        color: var(--uui-color-danger);
      }
    `
];
B([
  w()
], d.prototype, "_info", 2);
B([
  w()
], d.prototype, "_failed", 2);
d = B([
  K("esatto-build-dashboard")
], d);
const _t = d;
export {
  d as EsattoBuildDashboardElement,
  _t as default
};
//# sourceMappingURL=build-dashboard.js.map
