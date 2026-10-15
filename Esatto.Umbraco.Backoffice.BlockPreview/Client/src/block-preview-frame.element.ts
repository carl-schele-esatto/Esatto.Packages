// Generic Block List custom view: shows the block's real frontend rendering — the site's
// Razor partial inside the site's preview layout — in a sandboxed, auto-sized iframe.
// A slim label bar above it opens the block editor; the preview itself is interactive.

import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UMB_BLOCK_ENTRY_CONTEXT } from '@umbraco-cms/backoffice/block';
import type { UmbBlockEditorCustomViewConfiguration, UmbBlockEditorCustomViewElement } from '@umbraco-cms/backoffice/block-custom-view';
import { UMB_CONTENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/content';
import { UMB_PROPERTY_CONTEXT, UMB_PROPERTY_DATASET_CONTEXT } from '@umbraco-cms/backoffice/property';
import { UMB_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/workspace';
import { buildRenderRequest, newNonce, requestFingerprint, type BlockData } from './render-request';
import { readHeight } from './frame-messages';
import { renderBlockPreview, type RenderOutcome } from './api';
import { isPreviewableWorkspace, toViewState, type ViewState } from './view-state';

const DEBOUNCE_MS = 400;

@customElement('esatto-block-preview-frame')
export class EsattoBlockPreviewFrameElement extends UmbLitElement implements UmbBlockEditorCustomViewElement {
  @property({ attribute: false }) content?: Record<string, unknown>;
  @property({ attribute: false }) settings?: Record<string, unknown>;
  @property({ attribute: false }) label?: string;
  @property({ attribute: false }) icon?: string;
  @property({ attribute: false }) config?: UmbBlockEditorCustomViewConfiguration;
  @property({ attribute: false }) contentInvalid?: boolean;
  @property({ attribute: false }) settingsInvalid?: boolean;

  @state() private _view: ViewState = { kind: 'loading' };
  @state() private _height = 0;

  #entry?: typeof UMB_BLOCK_ENTRY_CONTEXT.TYPE;
  #workspace?: typeof UMB_CONTENT_WORKSPACE_CONTEXT.TYPE;
  #property?: typeof UMB_PROPERTY_CONTEXT.TYPE;
  #dataset?: typeof UMB_PROPERTY_DATASET_CONTEXT.TYPE;
  #timer?: ReturnType<typeof setTimeout>;
  #lastFingerprint?: string;
  #currentNonce = '';
  // The nearest workspace: the document's, or a block workspace when this Block List is nested
  // inside a block (UMB_CONTENT_WORKSPACE_CONTEXT would skip past that to the document).
  #nearestWorkspaceType?: string;

  constructor() {
    super();
    this.consumeContext(UMB_BLOCK_ENTRY_CONTEXT, (ctx) => { this.#entry = ctx; this.#schedule(0); });
    this.consumeContext(UMB_WORKSPACE_CONTEXT, (ctx) => { this.#nearestWorkspaceType = ctx?.getEntityType(); this.#schedule(0); });
    this.consumeContext(UMB_CONTENT_WORKSPACE_CONTEXT, (ctx) => {
      this.#workspace = ctx;
      this.#schedule(0);
      // After "Save the page once…" the editor saves; the block data didn't change, so re-render explicitly.
      this.observe(ctx?.isNew, (isNew) => {
        if (isNew === false && this._view.kind === 'unsaved') this.#schedule(0);
      }, '_observeIsNew');
    });
    this.consumeContext(UMB_PROPERTY_CONTEXT, (ctx) => { this.#property = ctx; this.#schedule(0); });
    this.consumeContext(UMB_PROPERTY_DATASET_CONTEXT, (ctx) => { this.#dataset = ctx; this.#schedule(0); });
  }

  connectedCallback(): void {
    super.connectedCallback();
    window.addEventListener('message', this.#onMessage);
  }

  disconnectedCallback(): void {
    super.disconnectedCallback();
    window.removeEventListener('message', this.#onMessage);
    clearTimeout(this.#timer);
  }

  protected updated(changed: Map<PropertyKey, unknown>): void {
    if (changed.has('content') || changed.has('settings')) this.#schedule(DEBOUNCE_MS);
  }

  #schedule(delay: number): void {
    clearTimeout(this.#timer);
    this.#timer = setTimeout(() => void this.#renderSafely(), delay);
  }

  // Never leave a block on the placeholder because of an unexpected exception.
  async #renderSafely(): Promise<void> {
    try {
      await this.#render();
    } catch (err) {
      this.#lastFingerprint = undefined;
      this._view = { kind: 'error', message: (err as Error)?.message ?? 'The preview could not be loaded.' };
    }
  }

  async #render(): Promise<void> {
    if (this.#nearestWorkspaceType !== undefined && !isPreviewableWorkspace(this.#nearestWorkspaceType)) {
      this._view = { kind: 'plain' };
      return;
    }
    const content = this.#entry?.getContent() as BlockData | undefined;
    const documentKey = this.#workspace?.getUnique();
    const documentTypeKey = this.#workspace?.structure.getOwnerContentTypeUnique();
    const propertyAlias = this.#property?.getAlias();
    if (!content || !documentKey || !documentTypeKey || !propertyAlias) return; // contexts not ready yet

    const variant = this.#dataset?.getVariantId();
    const nonce = newNonce();
    const body = buildRenderRequest({
      documentKey,
      documentTypeKey,
      propertyAlias,
      culture: variant?.culture,
      segment: variant?.segment,
      nonce,
      content,
      settings: this.#entry?.getSettings() as BlockData | undefined,
    });

    const fingerprint = requestFingerprint(body);
    if (fingerprint === this.#lastFingerprint) return;
    this.#lastFingerprint = fingerprint;

    const { data, error } = await tryExecute(this, renderBlockPreview(body), { disableNotifications: true });
    if (fingerprint !== this.#lastFingerprint) return; // a newer render started meanwhile
    if (error || !data) {
      this.#lastFingerprint = undefined; // retry on the next change
      this._view = { kind: 'error', message: 'The preview could not be loaded.' };
      return;
    }
    const { view, keepFingerprint } = toViewState(data as RenderOutcome, nonce);
    if (!keepFingerprint) this.#lastFingerprint = undefined;
    if (view.kind === 'rendered') this.#currentNonce = view.nonce;
    this._view = view;
  }

  #onMessage = (event: MessageEvent): void => {
    const frame = this.shadowRoot?.querySelector('iframe');
    const height = readHeight(event, frame?.contentWindow, this.#currentNonce);
    if (height !== null) this._height = height;
  };

  #renderBar() {
    const invalid = this.contentInvalid || this.settingsInvalid;
    return html`
      <div class="bar ${invalid ? 'invalid' : ''}">
        <a class="label" href=${this.config?.editContentPath ?? nothing}>
          ${this.icon ? html`<umb-icon name=${this.icon}></umb-icon>` : nothing}
          <span>${this.label}</span>
        </a>
        ${this.config?.showSettingsEdit
          ? html`<a class="settings" href=${this.config.editSettingsPath ?? nothing} title="Settings"><umb-icon name="icon-settings"></umb-icon></a>`
          : nothing}
      </div>`;
  }

  #renderBody() {
    switch (this._view.kind) {
      case 'loading':
        return html`<div class="placeholder"></div>`;
      case 'rendered':
        return html`<iframe
          title=${this.label ?? 'Block preview'}
          sandbox="allow-scripts"
          .srcdoc=${this._view.html}
          style="height:${this._height || 80}px"></iframe>`;
      case 'unsaved':
        return html`<div class="note">Save the page once to see a preview.</div>`;
      case 'error':
        return html`<div class="note warning">Preview failed: ${this._view.message}</div>`;
      default:
        return nothing; // 'plain': label bar only, like Umbraco's own row
    }
  }

  render() {
    return html`${this.#renderBar()}${this.#renderBody()}`;
  }

  static styles = css`
    :host { display: block; border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); overflow: hidden; background: var(--uui-color-surface); }
    .bar { display: flex; align-items: center; min-height: 40px; padding: 0 var(--uui-size-space-4); gap: var(--uui-size-space-2); }
    .bar.invalid { color: var(--uui-color-danger); }
    .label { display: flex; align-items: center; gap: var(--uui-size-space-2); flex: 1; color: inherit; text-decoration: none; font-weight: 600; }
    .label:hover { text-decoration: underline; }
    .settings { color: inherit; }
    iframe { display: block; width: 100%; border: 0; border-top: 1px solid var(--uui-color-border); }
    .placeholder { min-height: 80px; border-top: 1px solid var(--uui-color-border); background: var(--uui-color-surface-alt); }
    .note { padding: var(--uui-size-space-3) var(--uui-size-space-4); border-top: 1px solid var(--uui-color-border); color: var(--uui-color-text-alt); font-size: 0.9em; }
    .note.warning { color: var(--uui-color-warning-standalone); }
  `;
}

export default EsattoBlockPreviewFrameElement;

declare global {
  interface HTMLElementTagNameMap {
    'esatto-block-preview-frame': EsattoBlockPreviewFrameElement;
  }
}
