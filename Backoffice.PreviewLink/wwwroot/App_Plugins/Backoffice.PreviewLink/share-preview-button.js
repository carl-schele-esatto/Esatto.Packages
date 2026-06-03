import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';

// Share Preview workspace action — button + modal element.
//
// Pairs with share-preview-action.js (the api class). The api carries the
// click logic (save → mint URL); this element renders the button and the
// modal that displays the resulting URL with Copy + "Show in browser" buttons.
//
// Umbraco's extension registry sets `manifest` and `api` on this element.
// On click we call api.execute(); api invokes the onShareReady callback we
// register in `updated` once the URL is back from the server.
//
// UmbElementMixin is required: the api's controller-base chain calls
// host.addUmbController(this), which the mixin provides. Without it the
// api construction throws "addUmbController is not a function".

const SAVE_MODAL_TAG = 'umb-document-save-modal';
const MODAL_CONTAINER_TAG = 'umb-backoffice-modal-container';
const NO_MODAL_FALLBACK_MS = 300;

function findElementDeep(root, tag) {
    if (!root) return null;
    if (root.tagName && root.tagName.toLowerCase() === tag) return root;
    const sr = root.shadowRoot;
    if (sr) {
        for (const child of sr.children) {
            const found = findElementDeep(child, tag);
            if (found) return found;
        }
    }
    if (root.children) {
        for (const child of root.children) {
            const found = findElementDeep(child, tag);
            if (found) return found;
        }
    }
    return null;
}

export class BackofficePreviewLinkButtonElement extends UmbElementMixin(LitElement) {
    static properties = {
        manifest: { type: Object, attribute: false },
        api: { type: Object, attribute: false },
        _busy: { state: true },
        _inFlight: { state: true },
        _modalOpen: { state: true },
        _shareData: { state: true },
        _copyState: { state: true },
    };

    static styles = css`
        :host {
            display: inline-block;
        }
        .modal-backdrop {
            position: fixed;
            inset: 0;
            background: rgba(0, 0, 0, 0.5);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 10000;
        }
        .modal {
            background: var(--uui-color-surface, #fff);
            color: var(--uui-color-text, #000);
            border-radius: var(--uui-border-radius, 4px);
            padding: var(--uui-size-space-6, 24px);
            width: min(560px, 90vw);
            box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
        }
        .modal__title {
            margin: 0 0 var(--uui-size-space-3, 12px);
            font-size: 1.25rem;
            font-weight: 600;
        }
        .modal__url {
            width: 100%;
            font-family: monospace;
            font-size: 0.875rem;
            padding: var(--uui-size-space-2, 8px);
            border: 1px solid var(--uui-color-border, #ccc);
            border-radius: 4px;
            background: var(--uui-color-surface-alt, #f5f5f5);
            box-sizing: border-box;
            margin-block-end: var(--uui-size-space-3, 12px);
        }
        .modal__expires {
            font-size: 0.875rem;
            color: var(--uui-color-text-alt, #666);
            margin-block-end: var(--uui-size-space-4, 16px);
        }
        .modal__actions {
            display: flex;
            justify-content: flex-end;
            gap: var(--uui-size-space-2, 8px);
            flex-wrap: wrap;
        }
    `;

    constructor() {
        super();
        this._busy = false;
        this._inFlight = false;
        this._modalOpen = false;
        this._shareData = null;
        this._copyState = '';
        this.manifest = null;
        this.api = null;
    }

    updated(changed) {
        if (changed.has('api') && this.api && typeof this.api.onShareReady === 'function') {
            this.api.onShareReady((data) => {
                this._shareData = data;
                this._modalOpen = true;
                this._copyState = '';
            });
        }
    }

    async _handleClick() {
        if (this._inFlight || !this.api) return;
        this._inFlight = true;

        // Don't spin yet — modal interaction is the user's focus. Start spinner
        // when the save modal closes (or immediately if no modal — invariant doc).
        const stopWatching = this._watchSaveModalClose(() => {
            this._busy = true;
        });

        try {
            await this.api.execute();
        } catch (err) {
            console.warn('[backoffice-preview-link]', err);
        } finally {
            stopWatching();
            this._busy = false;
            this._inFlight = false;
        }
    }

    // Polls the modal container's shadow root once per frame. Fires onClose
    // when the save modal goes from present → absent. If no modal appears
    // within NO_MODAL_FALLBACK_MS, fires immediately (invariant docs save
    // directly without opening the variant-picker modal).
    _watchSaveModalClose(onClose) {
        let cancelled = false;
        let fired = false;
        let modalSeen = false;
        let rafHandle = null;
        let fallbackTimer = null;

        const cleanup = () => {
            cancelled = true;
            if (rafHandle) cancelAnimationFrame(rafHandle);
            if (fallbackTimer) clearTimeout(fallbackTimer);
        };

        const fire = () => {
            if (cancelled || fired) return;
            fired = true;
            cleanup();
            onClose();
        };

        fallbackTimer = setTimeout(() => {
            if (!modalSeen) fire();
        }, NO_MODAL_FALLBACK_MS);

        const tick = () => {
            if (cancelled) return;
            const container = findElementDeep(document.documentElement, MODAL_CONTAINER_TAG);
            const modal = container && container.shadowRoot
                ? container.shadowRoot.querySelector(SAVE_MODAL_TAG)
                : null;
            if (modal) {
                modalSeen = true;
            } else if (modalSeen) {
                fire();
                return;
            }
            rafHandle = requestAnimationFrame(tick);
        };

        rafHandle = requestAnimationFrame(tick);
        return cleanup;
    }

    async _copyUrl() {
        if (!this._shareData?.url) return;
        try {
            await navigator.clipboard.writeText(this._shareData.url);
            this._copyState = 'copied';
            setTimeout(() => { this._copyState = ''; }, 2000);
        } catch (err) {
            console.warn('[backoffice-preview-link] clipboard write failed:', err);
        }
    }

    _showInBrowser() {
        if (!this._shareData?.url) return;
        window.open(this._shareData.url, '_blank');
    }

    _closeModal() {
        this._modalOpen = false;
        this._shareData = null;
    }

    _formatExpiry(expiresAtIso) {
        try {
            const d = new Date(expiresAtIso);
            const datePart = d.toISOString().split('T')[0];
            return `Expires ${datePart} (in 7 days)`;
        } catch {
            return '';
        }
    }

    _renderModal() {
        if (!this._modalOpen || !this._shareData) return nothing;
        return html`
            <div class="modal-backdrop" @click=${(e) => { if (e.target === e.currentTarget) this._closeModal(); }}>
                <div class="modal" role="dialog" aria-labelledby="backoffice-preview-link-title">
                    <h2 id="backoffice-preview-link-title" class="modal__title">Preview link</h2>
                    <input class="modal__url" type="text" readonly .value=${this._shareData.url} @click=${(e) => e.target.select()} />
                    <p class="modal__expires">${this._formatExpiry(this._shareData.expiresAt)}</p>
                    <div class="modal__actions">
                        <uui-button look="secondary" label="Close" @click=${() => this._closeModal()}></uui-button>
                        <uui-button look="secondary" label="Show in browser" @click=${() => this._showInBrowser()}></uui-button>
                        <uui-button look="primary" label=${this._copyState === 'copied' ? 'Copied!' : 'Copy'} @click=${() => this._copyUrl()}></uui-button>
                    </div>
                </div>
            </div>
        `;
    }

    render() {
        const label = this.manifest?.meta?.label ?? 'Share preview';
        return html`
            <uui-button
                look="primary"
                color="default"
                label=${label}
                style=${this._busy ? 'opacity: 0.6;' : nothing}
                .state=${this._busy ? 'waiting' : undefined}
                @click=${() => this._handleClick()}>
                ${label}
            </uui-button>
            ${this._renderModal()}
        `;
    }
}

customElements.define('backoffice-preview-link-button', BackofficePreviewLinkButtonElement);
export default BackofficePreviewLinkButtonElement;
