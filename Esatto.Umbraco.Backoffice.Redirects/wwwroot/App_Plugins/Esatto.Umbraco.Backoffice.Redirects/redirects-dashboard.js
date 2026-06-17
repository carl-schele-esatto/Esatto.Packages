import { LitElement, css, html } from '@umbraco-cms/backoffice/external/lit';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute, UmbApiError } from '@umbraco-cms/backoffice/resources';

// Management API endpoints require bearer auth. umbHttpClient is the
// pre-configured Bellissima OpenAPI client (token + credentials:'include');
// tryExecute wraps the request promise and returns { data, error }.
// security must be declared explicitly — without it, umbHttpClient does not
// attach the bearer token and the request 401s.
const API_BASE = '/umbraco/management/api/v1/backoffice/redirects';
const SECURITY = [{ scheme: 'bearer', type: 'http' }];

class BackofficeRedirectsDashboard extends UmbElementMixin(LitElement) {
    static properties = {
        _rows: { state: true },
        _oldPath: { state: true },
        _newUrl: { state: true },
        _error: { state: true },
        _busy: { state: true },
        _loaded: { state: true },
        _editingId: { state: true },
        _editOldPath: { state: true },
        _editNewUrl: { state: true },
        _editError: { state: true },
        _searchDraft: { state: true },
        _searchTerm: { state: true },
    };

    #notifications;

    constructor() {
        super();
        this._rows = [];
        this._oldPath = '';
        this._newUrl = '';
        this._error = '';
        this._busy = false;
        this._loaded = false;
        this._editingId = null;
        this._editOldPath = '';
        this._editNewUrl = '';
        this._editError = '';
        this._searchDraft = '';
        this._searchTerm = '';

        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notifications = ctx; });
    }

    connectedCallback() {
        super.connectedCallback();
        this.#loadRows();
    }

    async #loadRows() {
        this._busy = true;
        try {
            const { data, error } = await tryExecute(
                this,
                umbHttpClient.get({ url: API_BASE, security: SECURITY }),
            );
            if (error) throw error;
            this._rows = data ?? [];
        } catch (err) {
            this.#notifications?.peek('danger', { data: { message: err.message ?? 'Load failed.' } });
        } finally {
            this._loaded = true;
            this._busy = false;
        }
    }

    async #add() {
        this._error = '';
        if (!this._oldPath.trim()) {
            this._error = 'Old URL is required.';
            return;
        }
        this._busy = true;
        try {
            const { error } = await tryExecute(
                this,
                umbHttpClient.post({
                    url: API_BASE,
                    body: { oldPath: this._oldPath, newUrl: this._newUrl },
                    security: SECURITY,
                }),
                { disableNotifications: true },
            );
            if (error) {
                if (UmbApiError.isUmbApiError(error) && error.status === 400) {
                    this._error = error.problemDetails?.title ?? 'Invalid input.';
                    return;
                }
                throw error;
            }

            this._oldPath = '';
            this._newUrl = '';
            await this.#loadRows();
        } catch (err) {
            this.#notifications?.peek('danger', { data: { message: err.message ?? 'Create failed.' } });
        } finally {
            this._busy = false;
        }
    }

    #startEdit(row) {
        this._editingId = row.id;
        this._editOldPath = row.oldPath;
        this._editNewUrl = row.newUrl ?? '';
        this._editError = '';
    }

    #cancelEdit() {
        this._editingId = null;
        this._editOldPath = '';
        this._editNewUrl = '';
        this._editError = '';
    }

    async #saveEdit(row) {
        this._editError = '';
        if (!this._editOldPath.trim()) {
            this._editError = 'Old URL is required.';
            return;
        }
        this._busy = true;
        try {
            const { error } = await tryExecute(
                this,
                umbHttpClient.put({
                    url: `${API_BASE}/${row.id}`,
                    body: { oldPath: this._editOldPath, newUrl: this._editNewUrl },
                    security: SECURITY,
                }),
                { disableNotifications: true },
            );
            if (error) {
                if (UmbApiError.isUmbApiError(error) && error.status === 400) {
                    this._editError = error.problemDetails?.title ?? 'Invalid input.';
                    return;
                }
                throw error;
            }
            this.#cancelEdit();
            await this.#loadRows();
        } catch (err) {
            this.#notifications?.peek('danger', { data: { message: err.message ?? 'Save failed.' } });
        } finally {
            this._busy = false;
        }
    }

    async #delete(row) {
        this._busy = true;
        try {
            await umbConfirmModal(this, {
                headline: 'Delete redirect?',
                content: `${row.oldPath} → ${row.newUrl || '(draft)'}`,
                confirmLabel: 'Delete',
                color: 'danger',
            });
        } catch {
            this._busy = false;
            return; // user cancelled
        }

        try {
            const { error } = await tryExecute(
                this,
                umbHttpClient.delete({ url: `${API_BASE}/${row.id}`, security: SECURITY }),
            );
            if (error) throw error;
            if (this._editingId === row.id) this.#cancelEdit();
            await this.#loadRows();
        } catch (err) {
            this.#notifications?.peek('danger', { data: { message: err.message ?? 'Delete failed.' } });
        } finally {
            this._busy = false;
        }
    }

    #applySearch() {
        this._searchTerm = (this._searchDraft ?? '').trim().toLowerCase();
    }

    #clearSearch() {
        this._searchDraft = '';
        this._searchTerm = '';
    }

    #filteredRows() {
        const term = this._searchTerm;
        if (!term) return this._rows;
        return this._rows.filter((r) => {
            const oldHit = (r.oldPath ?? '').toLowerCase().includes(term);
            const newHit = (r.newUrl ?? '').toLowerCase().includes(term);
            return oldHit || newHit;
        });
    }

    #renderRow(row) {
        const isEditing = this._editingId === row.id;
        const isDraft = !row.newUrl || !row.newUrl.trim();

        if (isEditing) {
            return html`
                <uui-table-row class="editing">
                    <uui-table-cell>
                        <input
                            class="edit-input"
                            type="text"
                            aria-label="Old URL"
                            placeholder="/old-path"
                            .value=${this._editOldPath}
                            ?disabled=${this._busy}
                            @input=${(e) => { this._editOldPath = e.target.value; }}>
                    </uui-table-cell>
                    <uui-table-cell>
                        <input
                            class="edit-input"
                            type="text"
                            aria-label="New URL"
                            placeholder="/new-path or https://example.com/x"
                            .value=${this._editNewUrl}
                            ?disabled=${this._busy}
                            @input=${(e) => { this._editNewUrl = e.target.value; }}>
                        ${this._editError ? html`<p role="alert" class="edit-error">${this._editError}</p>` : ''}
                    </uui-table-cell>
                    <uui-table-cell>
                        <div class="row-actions">
                            <uui-button look="secondary" ?disabled=${this._busy} @click=${() => this.#cancelEdit()}>Cancel</uui-button>
                            <uui-button look="primary" ?disabled=${this._busy} @click=${() => this.#saveEdit(row)}>Save</uui-button>
                        </div>
                    </uui-table-cell>
                </uui-table-row>
            `;
        }

        return html`
            <uui-table-row class=${isDraft ? 'draft' : ''}>
                <uui-table-cell>${row.oldPath}</uui-table-cell>
                <uui-table-cell>
                    ${isDraft
                        ? html`<span class="draft-badge">Draft — no target set</span>`
                        : row.newUrl}
                </uui-table-cell>
                <uui-table-cell>
                    <div class="row-actions">
                        <uui-button look="secondary" aria-label=${`Edit redirect ${row.oldPath}`} ?disabled=${this._busy} @click=${() => this.#startEdit(row)}>Edit</uui-button>
                        <uui-button look="secondary" color="danger" aria-label=${`Delete redirect ${row.oldPath}`} ?disabled=${this._busy} @click=${() => this.#delete(row)}>Delete</uui-button>
                    </div>
                </uui-table-cell>
            </uui-table-row>
        `;
    }

    render() {
        if (!this._loaded) {
            return html`<uui-box headline="Redirects"><p>Loading…</p></uui-box>`;
        }

        return html`
            <uui-box headline="Redirects">
                <p>Redirect dead URLs (that no longer resolve to a page) to a new URL. Matches are exact, case-insensitive, and preserve query strings.<br/>Responses are 301 (permanent). Leave <strong>New URL</strong> empty to save a <em>draft</em> — the row is listed here but no redirect fires until a target is set.</p>

                <uui-form>
                    <form class="form search-form" @submit=${(e) => { e.preventDefault(); this.#applySearch(); }}>
                        <div class="form-field">
                            <label for="redirects-search">Search</label>
                            <input
                                id="redirects-search"
                                class="text-input"
                                type="search"
                                placeholder="Filter by any part of old or new URL (e.g. 'pea' matches 'appear')"
                                .value=${this._searchDraft}
                                ?disabled=${this._busy}
                                @input=${(e) => { this._searchDraft = e.target.value; }}>
                        </div>
                        ${this._searchTerm
                            ? html`<uui-button look="secondary" type="button" ?disabled=${this._busy} @click=${() => this.#clearSearch()}>Clear</uui-button>`
                            : ''}
                        <uui-button look="primary" type="submit" ?disabled=${this._busy}>Search</uui-button>
                    </form>
                </uui-form>

                <uui-form>
                    <form class="form" @submit=${(e) => { e.preventDefault(); this.#add(); }}>
                        <div class="form-field">
                            <label for="redirects-old-url">Old URL</label>
                            <input
                                id="redirects-old-url"
                                class="text-input"
                                type="text"
                                placeholder="/old-path"
                                .value=${this._oldPath}
                                ?disabled=${this._busy}
                                aria-describedby=${this._error ? 'redirects-error' : ''}
                                aria-invalid=${this._error ? 'true' : 'false'}
                                @input=${(e) => { this._oldPath = e.target.value; }}>
                        </div>
                        <div class="form-field">
                            <label for="redirects-new-url">New URL <span class="optional">(optional — draft if empty)</span></label>
                            <input
                                id="redirects-new-url"
                                class="text-input"
                                type="text"
                                placeholder="/new-path or https://example.com/x"
                                .value=${this._newUrl}
                                ?disabled=${this._busy}
                                aria-describedby=${this._error ? 'redirects-error' : ''}
                                aria-invalid=${this._error ? 'true' : 'false'}
                                @input=${(e) => { this._newUrl = e.target.value; }}>
                        </div>
                        <uui-button look="primary" type="submit" ?disabled=${this._busy}>Add</uui-button>
                    </form>
                </uui-form>

                ${this._error ? html`<p id="redirects-error" role="alert" class="error">${this._error}</p>` : ''}

                ${(() => {
                    const filtered = this.#filteredRows();
                    if (this._rows.length === 0) {
                        return this._busy ? '' : html`<p class="empty">No redirects configured.</p>`;
                    }
                    if (filtered.length === 0) {
                        return html`<p class="empty">No matches for <strong>${this._searchTerm}</strong>.</p>`;
                    }
                    return html`
                        ${this._searchTerm ? html`<p class="match-count">${filtered.length} of ${this._rows.length} redirects match.</p>` : ''}
                        <uui-table>
                            <uui-table-head>
                                <uui-table-head-cell>Old URL</uui-table-head-cell>
                                <uui-table-head-cell>New URL</uui-table-head-cell>
                                <uui-table-head-cell></uui-table-head-cell>
                            </uui-table-head>
                            ${filtered.map((row) => this.#renderRow(row))}
                        </uui-table>
                    `;
                })()}
            </uui-box>
        `;
    }

    static styles = css`
        :host { display: block; padding: var(--uui-size-space-5); }
        uui-box p { margin-block-end: var(--uui-size-space-6); }
        .form { display: flex; gap: var(--uui-size-space-3); align-items: flex-end; margin-block-end: var(--uui-size-space-5); }
        uui-button { min-width: 5.5rem; }
        .form-field { display: flex; flex-direction: column; gap: var(--uui-size-space-2); flex: 1; }
        .form-field label { font-weight: var(--uui-font-weight-bold, 700); }
        .form-field label .optional { font-weight: normal; color: var(--uui-color-text-alt); font-size: var(--uui-font-size-small, 0.875em); }
        .form-field .text-input { width: 100%; }
        .text-input,
        .edit-input {
            box-sizing: border-box;
            padding: var(--uui-size-space-2, 6px) var(--uui-size-space-3, 9px);
            border: 1px solid var(--uui-color-border, #d8d7d9);
            border-radius: var(--uui-border-radius, 3px);
            font: inherit;
            color: inherit;
            background: var(--uui-color-surface, #fff);
        }
        .text-input:focus,
        .edit-input:focus {
            outline: none;
            border-color: var(--uui-color-focus, #3879ff);
            box-shadow: 0 0 0 1px var(--uui-color-focus, #3879ff);
        }
        .text-input:disabled,
        .edit-input:disabled { opacity: 0.6; }
        .error { color: var(--uui-color-danger); margin-block-end: var(--uui-size-space-3); }
        .edit-error { color: var(--uui-color-danger); margin: var(--uui-size-space-2) 0 0; font-size: var(--uui-font-size-small, 0.875em); }
        .empty { color: var(--uui-color-text-alt); font-style: italic; margin-block-start: var(--uui-size-space-3); }
        .match-count { color: var(--uui-color-text-alt); margin-block-start: var(--uui-size-space-3); margin-block-end: 0; font-size: var(--uui-font-size-small, 0.875em); }
        uui-table { margin-block-start: var(--uui-size-space-3); table-layout: fixed; width: 100%; }
        uui-table-head-cell:nth-child(1),
        uui-table-cell:nth-child(1),
        uui-table-head-cell:nth-child(2),
        uui-table-cell:nth-child(2) { width: 42%; }
        uui-table-head-cell:nth-child(3),
        uui-table-cell:nth-child(3) { width: 16%; text-align: right; }
        uui-table-cell { word-break: break-word; vertical-align: middle; }
        .row-actions { display: flex; gap: var(--uui-size-space-2); justify-content: flex-end; }
        .draft-badge { color: var(--uui-color-text-alt); font-style: italic; }
        uui-table-row.draft uui-table-cell:first-child { color: var(--uui-color-text-alt); }
        uui-table-row.editing .edit-input { width: 100%; }
    `;
}

customElements.define('backoffice-redirects-dashboard', BackofficeRedirectsDashboard);
export default BackofficeRedirectsDashboard;
