// One issue opened up (approved mockup): criteria, why it matters, how to fix, caveats, and every
// affected element by page — with Edit in Umbraco, View on page, false positives and Re-check.

import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { addFalsePositive, recheck, removeFalsePositive } from '../api';
import { axeTags } from '../axe-tags';
import { shared } from '../dashboard.styles';
import { levelLabel } from '../format';
import { editContentUrl, editMediaUrl, safeHref } from '../links';
import { runInFrame } from '../scan/frame-runner';
import { recheckJobs } from '../scan/scan-controller';
import type { IssueGroup, IssueNode, IssuePage, SettingsResponse } from '../types';

const AXE_URL = '/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/axe.min.js';

/** The false-positive note's column length (FalsePositiveService.MaxNoteLength). */
const NOTE_MAX = 1024;

interface PendingFalsePositive { page: IssuePage; node: IssueNode | null }

@customElement('esatto-a11y-issue-detail')
export class EsattoA11yIssueDetailElement extends UmbLitElement {
  @property({ attribute: false }) group!: IssueGroup;
  @property({ attribute: false }) siteKey = '';
  @property({ attribute: false }) settings: SettingsResponse | null = null;

  @state() private _pending: PendingFalsePositive | null = null;
  @state() private _note = '';
  @state() private _noteError = false;
  @state() private _rechecking: string | null = null;
  @state() private _message: string | null = null;

  override firstUpdated() {
    this.renderRoot.querySelector<HTMLElement>('h3')?.focus();
  }

  #changed() {
    this.dispatchEvent(new CustomEvent('changed', { bubbles: true, composed: true }));
  }

  #askFalsePositive(page: IssuePage, node: IssueNode | null) {
    this._pending = { page, node };
    this._note = '';
    this._noteError = false;
    this.updateComplete.then(() => this.renderRoot.querySelector<HTMLDialogElement>('dialog')?.showModal());
  }

  #closeDialog() {
    this.renderRoot.querySelector<HTMLDialogElement>('dialog')?.close();
    this._pending = null;
  }

  async #saveFalsePositive() {
    if (!this._pending) return;
    if (!this._note.trim()) {
      this._noteError = true;
      this.renderRoot.querySelector<HTMLTextAreaElement>('#fp-note')?.focus();
      return;
    }
    const { page, node } = this._pending;
    const { error } = await tryExecute(this, addFalsePositive(this.siteKey, {
      ruleId: this.group.ruleId, contentKey: page.contentKey, selector: node?.selector ?? null, note: this._note.trim(),
    }));
    if (error) return;
    this.#closeDialog();
    this._message = 'Marked as a false positive.';
    this.#changed();
  }

  async #undoFalsePositive(id: number) {
    const { error } = await tryExecute(this, removeFalsePositive(id));
    if (error) return;
    this._message = 'False positive removed.';
    this.#changed();
  }

  async #recheck(page: IssuePage) {
    if (!this.settings) return;
    const s = this.settings.settings;
    this._rechecking = page.contentKey;
    this._message = null;
    const host = this.renderRoot.querySelector<HTMLElement>('#frames')!;
    // Every width the scan loaded the page at, so a fix isn't hidden by findings left at another width.
    for (const job of recheckJobs(page)) {
      const outcome = await runInFrame(job, host, { axeUrl: AXE_URL, tags: axeTags(s.version, s.level, s.scan.bestPractice), settleMs: 1000, timeoutMs: 30000 });
      if (outcome.kind !== 'ok' || !outcome.result) {
        this._message = `Re-check failed at ${job.viewport} px: ${outcome.reason ?? outcome.kind}.`;
        this._rechecking = null;
        this.#changed();
        return;
      }
      const { error } = await tryExecute(this, recheck(this.siteKey, outcome.result));
      if (error) {
        this._rechecking = null;
        return;
      }
    }
    this._rechecking = null;
    this._message = `${page.name} was re-checked.`;
    this.#changed();
  }

  override render() {
    const g = this.group;
    return html`
      <div class="stack">
        <uui-button look="secondary" label="Back to all issues" @click=${() => this.dispatchEvent(new CustomEvent('back'))}>← Back to all issues</uui-button>
        <h3 tabindex="-1">${g.title}</h3>
        <p class="facts">
          ${g.criteria.length ? html`<strong>WCAG ${g.criteria.join(', ')}</strong> · Level ${levelLabel(g.level)} ·` : html`<strong>Best practice</strong> ·`}
          ${g.kind === 'incomplete' ? html`<span class="tag">Needs review</span> ·` : nothing}
          ${g.pageCount} ${g.pageCount === 1 ? 'page' : 'pages'} (${g.elementCount} ${g.elementCount === 1 ? 'element' : 'elements'}) ·
          Who fixes: <strong>${g.owner}</strong>${g.impact ? html` · Impact: ${g.impact}` : nothing}
        </p>
        ${this._message ? html`<p role="status" class="message">${this._message}</p>` : nothing}

        <uui-box>
          <div class="guide">
            <h4>Why it matters</h4><p>${g.why}</p>
            <h4>How to fix</h4><p>${g.howToFix}</p>
            ${g.caveats ? html`<h4>Caveats</h4><p>${g.caveats}</p>` : nothing}
          </div>
        </uui-box>
        ${g.kind === 'incomplete' ? html`<p class="muted">The scanner couldn't decide these automatically. Check each one by hand; mark it as a false positive if it's fine.</p>` : nothing}
        ${safeHref(g.learnMoreUrl) ? html`<p><a href=${safeHref(g.learnMoreUrl)} target="_blank" rel="noopener">${g.criteria.length ? `Read W3C's “Understanding ${g.criteria[0]}”` : 'Read more about this rule'}<span class="visually-hidden"> (opens in a new tab)</span></a></p>` : nothing}

        <h4>Where it occurs</h4>
        ${g.pages.map((p) => this.#page(p))}
        <div id="frames" aria-hidden="true"></div>
      </div>
      ${this.#dialog()}`;
  }

  #page(p: IssuePage) {
    const busy = this._rechecking === p.contentKey;
    return html`
      <uui-box>
        <div slot="headline" class="page-head">
          <span>${p.name}</span> <span class="muted">${safeHref(p.url) ? new URL(p.url).pathname : ''}</span>
        </div>
        <div class="row actions">
          <a href=${editContentUrl(p.contentKey, p.culture)}>Edit page in Umbraco</a>
          ${safeHref(p.url) ? html`<a href=${safeHref(p.url)} target="_blank" rel="noopener">View on page<span class="visually-hidden"> (opens in a new tab)</span></a>` : nothing}
          <uui-button compact look="secondary" label="Re-check this page" ?disabled=${busy} @click=${() => this.#recheck(p)}>${busy ? 'Re-checking…' : 'Re-check this page'}</uui-button>
          ${!p.nodes.some((n) => n.falsePositiveId) ? html`<uui-button compact look="secondary" label="Mark all on this page as false positive" @click=${() => this.#askFalsePositive(p, null)}></uui-button>` : nothing}
        </div>
        <ul class="nodes">
          ${p.nodes.map((n) => html`<li>
            <pre>${n.html}</pre>
            <p class="muted">Selector: <code>${n.selector}</code></p>
            <div class="row">
              ${n.mediaKey ? html`<a href=${editMediaUrl(n.mediaKey)}>Edit image in Umbraco</a>` : nothing}
              ${n.falsePositiveId
                ? html`<uui-button compact look="secondary" label="Undo false positive" @click=${() => this.#undoFalsePositive(n.falsePositiveId!)}></uui-button>`
                : html`<uui-button compact look="secondary" label="Mark as false positive" @click=${() => this.#askFalsePositive(p, n)}></uui-button>`}
            </div>
          </li>`)}
        </ul>
      </uui-box>`;
  }

  #dialog() {
    const pending = this._pending;
    return html`
      <dialog aria-labelledby="fp-title" @close=${() => (this._pending = null)}>
        ${pending ? html`
          <form method="dialog" @submit=${(e: Event) => { e.preventDefault(); this.#saveFalsePositive(); }}>
            <h3 id="fp-title">Mark as false positive</h3>
            <p>${pending.node ? html`This element on <strong>${pending.page.name}</strong>` : html`Every “${this.group.title}” finding on <strong>${pending.page.name}</strong>`} will be hidden from the report and the score, now and in later scans.</p>
            <label for="fp-note">Why is this not a problem? (required)</label>
            <textarea id="fp-note" rows="3" maxlength=${NOTE_MAX} aria-required="true" aria-invalid=${this._noteError ? 'true' : 'false'} aria-describedby=${this._noteError ? 'fp-error' : nothing}
              .value=${this._note} @input=${(e: Event) => { this._note = (e.target as HTMLTextAreaElement).value; this._noteError = false; }}></textarea>
            ${this._noteError ? html`<p id="fp-error" class="error">Write a short reason, so others know why.</p>` : nothing}
            <div class="row">
              <uui-button type="submit" look="primary" label="Mark as false positive"></uui-button>
              <uui-button look="secondary" label="Cancel" @click=${this.#closeDialog}></uui-button>
            </div>
          </form>` : nothing}
      </dialog>`;
  }

  static override styles = [shared, css`
    h3:focus { outline: none; }
    h3:focus-visible { outline: 2px solid var(--uui-color-focus); }
    .facts { margin: 0; }
    .guide { max-width: 60rem; }
    .guide h4 { margin-top: var(--uui-size-space-4); }
    .guide h4:first-child { margin-top: 0; }
    .guide p { margin: var(--uui-size-space-2) 0 0; }
    .message { padding: var(--uui-size-space-3); background: var(--uui-color-surface-alt); border-radius: var(--uui-border-radius); }
    .page-head { display: flex; gap: var(--uui-size-space-3); flex-wrap: wrap; }
    .actions { margin-bottom: var(--uui-size-space-4); }
    .nodes { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-4); }
    .nodes li { border-top: 1px solid var(--uui-color-divider); padding-top: var(--uui-size-space-3); }
    .nodes p { margin: var(--uui-size-space-2) 0; }
    dialog { border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); padding: var(--uui-size-layout-1); max-width: 36rem; width: calc(100% - 2rem); background: var(--uui-color-surface); color: var(--uui-color-text); }
    dialog::backdrop { background: rgb(0 0 0 / 0.4); }
    dialog form { display: grid; gap: var(--uui-size-space-3); }
    textarea { font: inherit; padding: var(--uui-size-space-2); border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius); background: var(--uui-color-surface); color: var(--uui-color-text); }
    textarea[aria-invalid='true'] { border-color: var(--uui-color-danger); }
    #frames { position: relative; height: 0; overflow: hidden; }
  `];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-a11y-issue-detail': EsattoA11yIssueDetailElement;
  }
}
