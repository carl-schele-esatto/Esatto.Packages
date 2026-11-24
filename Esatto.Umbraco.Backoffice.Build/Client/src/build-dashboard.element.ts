// Content → Build dashboard. Registered in public/umbraco-package.json.
//
// Shows WHICH build is deployed on this environment, so you can see that the right build is on Test and Live.
// Endpoint (Content section access): GET /umbraco/management/api/v1/esatto/build-info (api.ts).
//
// The first box has three states (view-state.ts, buildKind). A pipeline deployed this build: the stamp's build number, commit and time.
// No stamp but a local git history (Development): "Local build", with the commit the running app was built from. Neither: "Unknown
// build", which says where the stamp comes from. Below it: the commits in this build (or, locally, the working copy's latest commits),
// then this environment and its packages.
//
// Every text is a key esattoBuild_<key> (lang/en.ts); every date goes through format.ts, in the browser's locale.

import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { getBuildInfo } from './api';
import { fillText } from './fill-text';
import { builtFrom, formatDate, formatDateTime, safeHref, shortSha } from './format';
import type { BuildInfo, LocalChanges, PipelineBuild, PipelineChanges, PipelineCommit } from './types';
import { buildKind, localTags, summarizeChanges, type ChangesSummary, type Since } from './view-state';

const README_URL = 'https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.Build';
const STAMP_FILE = 'build-info.json';
const NO_VALUE = '—';

const code = (text: string) => html`<code>${text}</code>`;

/** `content` as a link to `url` when that is an http or https address (format.ts, safeHref), otherwise as plain text. */
function link(url: string | null | undefined, content: unknown) {
  const href = safeHref(url);
  return href ? html`<a href=${href} target="_blank" rel="noopener">${content}</a>` : content;
}

@customElement('esatto-build-dashboard')
export class EsattoBuildDashboardElement extends UmbLitElement {
  @state() private _info: BuildInfo | null = null;
  @state() private _failed = false;

  override connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    const { data, error } = await tryExecute(this, getBuildInfo(), { disableNotifications: true });
    if (error || !data) {
      this._failed = true;
      return;
    }
    this._failed = false;
    this._info = data;
  }

  override render() {
    if (this._failed) {
      return html`<uui-box headline=${this.localize.term('esattoBuild_title')}><p class="error">${this.localize.term('esattoBuild_loadFailed')}</p></uui-box>`;
    }
    if (!this._info) return html`<uui-loader></uui-loader>`;

    const info = this._info;
    return html`
      ${this.#firstBox(info)} ${this.#changesBox(info)}
      <div class="columns">
        <uui-box headline=${this.localize.term('esattoBuild_environmentTitle')}>
          <dl>
            <dt>${this.localize.term('esattoBuild_environment')}</dt><dd>${info.environment}</dd>
            <dt>${this.localize.term('esattoBuild_instance')}</dt><dd>${code(info.instance)}</dd>
            <dt>${this.localize.term('esattoBuild_runningSince')}</dt><dd>${formatDate(info.runningSinceUtc)}</dd>
            <dt>${this.localize.term('esattoBuild_umbraco')}</dt><dd>${info.umbracoVersion}</dd>
          </dl>
        </uui-box>
        <uui-box headline=${this.localize.term('esattoBuild_packagesTitle')}>
          <dl>
            ${info.packages.map((p) => html`<dt>${p.name}</dt><dd>${p.version ?? NO_VALUE}</dd>`)}
          </dl>
        </uui-box>
      </div>
    `;
  }

  // ---- The first box: which build is this? ------------------------------------------------------------------------------------

  #firstBox(info: BuildInfo) {
    switch (buildKind(info)) {
      case 'pipeline':
        return this.#pipelineBox(info.pipeline!);
      case 'local':
        return this.#localBox(info.local!, builtFrom(info.appVersion));
      default:
        return this.#unknownBox();
    }
  }

  #headline(number: unknown, branch?: string | null) {
    return html`
      <div class="headline">
        <span class="label">${this.localize.term('esattoBuild_deployedBuild')}</span>
        ${number}
        ${branch
          ? html`<uui-tag look="secondary" title=${this.localize.term('esattoBuild_branchTitle')}>${this.localize.term('esattoBuild_branch', branch)}</uui-tag>`
          : nothing}
      </div>
    `;
  }

  #pipelineBox(build: PipelineBuild) {
    return html`
      <uui-box>
        ${this.#headline(link(build.buildUrl, html`<span class="number">${build.buildNumber}</span>`), build.branch)}
        <dl>
          <dt>${this.localize.term('esattoBuild_commit')}</dt>
          <dd>
            ${link(build.commitUrl, code(shortSha(build.commit)))}${build.commitMessage ? html` <span class="message">${build.commitMessage}</span>` : nothing}
          </dd>
          <dt>${this.localize.term('esattoBuild_built')}</dt><dd>${formatDate(build.builtAtUtc)}</dd>
          ${build.targetEnvironment
            ? html`<dt>${this.localize.term('esattoBuild_pipelineTarget')}</dt><dd>${build.targetEnvironment}</dd>`
            : nothing}
        </dl>
      </uui-box>
    `;
  }

  // Built locally, the SDK puts the commit the build was made from after the "+" in the app's version.
  #localBox(local: LocalChanges, builtFromSha: string | null) {
    return html`
      <uui-box>
        ${this.#headline(html`<span class="number">${this.localize.term('esattoBuild_localBuild')}</span>`, local.branch)}
        <p>${fillText(this.localize.term('esattoBuild_localNotDeployed'), [code(STAMP_FILE)])}</p>
        ${builtFromSha
          ? html`<dl><dt>${this.localize.term('esattoBuild_builtFrom')}</dt><dd>${code(shortSha(builtFromSha))}</dd></dl>`
          : nothing}
      </uui-box>
    `;
  }

  #unknownBox() {
    return html`
      <uui-box>
        ${this.#headline(html`<span class="number">${this.localize.term('esattoBuild_unknownBuild')}</span>`)}
        <p>
          ${fillText(this.localize.term('esattoBuild_unknownNotFound'), [
            code(STAMP_FILE),
            link(README_URL, this.localize.term('esattoBuild_readmeLink')),
          ])}
        </p>
      </uui-box>
    `;
  }

  // ---- What's in this build ---------------------------------------------------------------------------------------------------

  #changesBox(info: BuildInfo) {
    switch (buildKind(info)) {
      case 'pipeline':
        return info.pipeline!.changes ? this.#pipelineChangesBox(info.pipeline!.changes) : nothing;
      case 'local':
        return this.#localChangesBox(info.local!, builtFrom(info.appVersion));
      default:
        return nothing;
    }
  }

  // The commits since the previous deployment, as the pipeline worked them out. Without a known previous deployment the list is just
  // the latest commits.
  #pipelineChangesBox(changes: PipelineChanges) {
    return html`
      <uui-box headline=${this.localize.term('esattoBuild_changesTitle')} class="changes">
        <p class="summary">${this.#changesSentence(summarizeChanges(changes))}</p>
        ${this.#commitList(changes.commits)}
      </uui-box>
    `;
  }

  #changesSentence(summary: ChangesSummary) {
    switch (summary.kind) {
      case 'noPrevious':
        return this.localize.term('esattoBuild_changesNoPrevious', summary.count);
      case 'none':
        return fillText(this.localize.term('esattoBuild_changesNone'), [this.#since(summary.since)]);
      case 'some':
        return fillText(this.localize.term('esattoBuild_changesSome', summary.count, summary.truncated), [this.#since(summary.since)]);
    }
  }

  #since(since: Since) {
    if (!since) return nothing;
    return since.type === 'build'
      ? fillText(this.localize.term('esattoBuild_sinceBuild'), [link(since.url, since.number)])
      : fillText(this.localize.term('esattoBuild_sinceCommit'), [code(shortSha(since.sha))]);
  }

  // Locally there is no previous deployment to compare with. The box shows the working copy's latest commits instead, which of them are
  // not pushed yet, and which one the running app was built from.
  #localChangesBox(local: LocalChanges, builtFromSha: string | null) {
    const summary = this.localize.term('esattoBuild_localSummary', local.commits.length, !!local.branch, local.unpushedCommits.length, local.uncommittedFiles);

    return html`
      <uui-box headline=${this.localize.term('esattoBuild_localChangesTitle')} class="changes">
        <p class="summary">${fillText(summary, [code(local.branch ?? '')])}</p>
        ${this.#commitList(local.commits, (commit) => this.#localTags(commit, local, builtFromSha))}
      </uui-box>
    `;
  }

  #localTags(commit: PipelineCommit, local: LocalChanges, builtFromSha: string | null) {
    const { running, notPushed } = localTags(commit, local, builtFromSha);
    const runningTag = running ? html` <uui-tag look="primary">${this.localize.term('esattoBuild_tagRunning')}</uui-tag>` : nothing;
    const notPushedTag = notPushed ? html` <uui-tag look="outline">${this.localize.term('esattoBuild_tagNotPushed')}</uui-tag>` : nothing;
    return html`${runningTag}${notPushedTag}`;
  }

  #commitList(commits: PipelineCommit[], tags?: (commit: PipelineCommit) => unknown) {
    return commits.length ? html`<ol>${commits.map((commit) => this.#commit(commit, tags?.(commit)))}</ol>` : nothing;
  }

  #commit(commit: PipelineCommit, tags: unknown = nothing) {
    const author = commit.author ? ` · ${commit.author}` : '';
    const date = commit.dateUtc ? ` · ${formatDateTime(commit.dateUtc)}` : '';
    const meta = html`<span class="meta">${link(commit.url, code(shortSha(commit.sha)))}${author}${date}${tags}</span>`;

    // The body is often long: collapsed, the subject is enough for an overview.
    return html`
      <li>
        ${commit.body?.trim()
          ? html`<details>
              <summary><span class="subject">${commit.subject}</span>${meta}</summary>
              <p class="body">${commit.body}</p>
            </details>`
          : html`<div class="subject-row"><span class="subject">${commit.subject}</span>${meta}</div>`}
      </li>
    `;
  }

  static override styles = [
    css`
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
    `,
  ];
}

export default EsattoBuildDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    'esatto-build-dashboard': EsattoBuildDashboardElement;
  }
}
