import { css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { errorMessage, importSettings } from '@esatto/scheduled-tasks/api.js';
import { formatRelative, termsOf } from '@esatto/scheduled-tasks/format.js';

// The order the preview groups are listed in, and their headings.
const GROUPS = [
    { status: 'invalid', term: 'statusInvalid' },
    { status: 'changed', term: 'statusChanged' },
    { status: 'reset', term: 'statusReset' },
    { status: 'unknownTask', term: 'statusUnknownTask' },
    { status: 'unchanged', term: 'statusUnchanged' },
];

/**
 * Sidebar that imports an export file from another environment: it first shows what would change
 * (the server compares the file with the settings here), then applies it on request. Submits
 * `{ message }` after importing.
 */
class EsattoScheduledTaskImportModal extends UmbModalBaseElement {
    static properties = {
        _document: { state: true },
        _preview: { state: true },
        _resetMissing: { state: true },
        _busy: { state: true },
        _error: { state: true },
    };

    constructor() {
        super();
        this._document = null;
        this._preview = null;
        this._resetMissing = false;
        this._busy = false;
        this._error = '';
    }

    get #t() {
        return termsOf(this.localize);
    }

    async #onFile(event) {
        const file = event.target.files?.[0];
        this._document = null;
        this._preview = null;
        this._error = '';
        if (!file) return;

        try {
            this._document = JSON.parse(await file.text());
        } catch {
            this._error = this.#t('fileUnreadable');
            return;
        }
        await this.#compare();
    }

    async #compare() {
        if (!this._document) return;
        this._busy = true;
        const { data, error } = await importSettings(this, this._document, false, this._resetMissing);
        this._busy = false;
        if (error) {
            this._preview = null;
            this._error = errorMessage(error, this.#t('importFailed'));
            return;
        }
        this._error = '';
        this._preview = data;
    }

    #changeCount() {
        return (this._preview?.tasks ?? []).filter((task) => task.status === 'changed' || task.status === 'reset').length;
    }

    async #apply() {
        const t = this.#t;
        this._busy = true;
        const { data, error } = await importSettings(this, this._document, true, this._resetMissing);
        this._busy = false;
        if (error || !data?.applied) {
            this._error = error ? errorMessage(error, t('importFailed')) : t('importHasErrors');
            if (data) this._preview = data;
            return;
        }
        this.value = { message: t('importDone', this.#changeCount()) };
        this._submitModal();
    }

    #renderTask(task) {
        return html`
            <li>
                <strong>${task.name ?? task.id}</strong>
                ${task.name ? html`<code class="id">${task.id}</code>` : nothing}
                ${task.errors.length || task.changes.length
                    ? html`<ul>
                        ${task.errors.map((e) => html`<li class="error">${e}</li>`)}
                        ${task.changes.map((c) => html`<li>${c}</li>`)}
                    </ul>`
                    : nothing}
            </li>`;
    }

    #renderPreview() {
        const t = this.#t;
        const preview = this._preview;
        if (!preview) return nothing;

        const changes = this.#changeCount();
        return html`
            ${preview.hasErrors ? html`<p role="alert" class="error">${t('importHasErrors')}</p>` : nothing}
            ${!preview.hasErrors && changes === 0 ? html`<p class="muted">${t('nothingToChange')}</p>` : nothing}
            ${GROUPS.map(({ status, term }) => {
                const tasks = preview.tasks.filter((task) => task.status === status);
                if (tasks.length === 0) return nothing;
                return html`
                    <h4 class="group group-${status}">${t(term)} (${tasks.length})</h4>
                    <ul class="tasks">${tasks.map((task) => this.#renderTask(task))}</ul>`;
            })}
        `;
    }

    render() {
        const t = this.#t;
        const doc = this._document;
        const changes = this.#changeCount();

        return html`
            <umb-body-layout headline=${t('importHeadline')}>
                <uui-box>
                    <p class="intro">${t('importIntro')}</p>
                    <div class="field">
                        <label for="file">${t('chooseFile')}</label>
                        <input id="file" type="file" accept=".json,application/json" ?disabled=${this._busy} @change=${(e) => this.#onFile(e)}>
                        ${doc?.exportedFrom && doc?.exportedUtc
                            ? html`<p class="muted">${t('exportedFrom', doc.exportedFrom, formatRelative(doc.exportedUtc, Date.now(), this.localize.lang()))}</p>`
                            : nothing}
                    </div>
                    <uui-toggle
                        label=${t('resetMissingLabel')}
                        .checked=${this._resetMissing}
                        ?disabled=${this._busy}
                        @change=${(e) => { this._resetMissing = e.target.checked; this.#compare(); }}></uui-toggle>
                    ${this._error ? html`<p role="alert" class="error">${this._error}</p>` : nothing}
                </uui-box>

                ${this._busy && !this._preview
                    ? html`<uui-box><p class="muted">${t('previewing')}</p><uui-loader></uui-loader></uui-box>`
                    : this._preview ? html`<uui-box>${this.#renderPreview()}</uui-box>` : nothing}

                <div slot="actions">
                    <uui-button label=${t('cancel')} ?disabled=${this._busy} @click=${() => this._rejectModal()}></uui-button>
                    <uui-button
                        look="primary"
                        color="positive"
                        label=${t('importApply', changes)}
                        .state=${this._busy && this._preview ? 'waiting' : undefined}
                        ?disabled=${this._busy || !this._preview || this._preview.hasErrors || changes === 0}
                        @click=${() => this.#apply()}></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    static styles = css`
        uui-box + uui-box { margin-block-start: var(--uui-size-space-5); }
        .intro { margin-block-start: 0; }
        .field { display: flex; flex-direction: column; gap: var(--uui-size-space-2); margin-block-end: var(--uui-size-space-5); }
        .field label { font-weight: var(--uui-font-weight-bold, 700); }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-font-size-small, 0.875em); margin: 0; }
        .error { color: var(--uui-color-danger); }
        .group { margin: var(--uui-size-space-4) 0 var(--uui-size-space-2); }
        .group:first-child { margin-block-start: 0; }
        .group-invalid { color: var(--uui-color-danger); }
        .group-unknownTask, .group-unchanged { color: var(--uui-color-text-alt); }
        .tasks { margin: 0; padding-inline-start: var(--uui-size-space-5); }
        .tasks > li { margin-block-end: var(--uui-size-space-3); }
        .tasks ul { margin: var(--uui-size-space-1) 0 0; padding-inline-start: var(--uui-size-space-5); color: var(--uui-color-text-alt); }
        code.id { margin-inline-start: var(--uui-size-space-2); font-size: 0.85em; color: var(--uui-color-text-alt); }
    `;
}

customElements.define('esatto-scheduled-task-import-modal', EsattoScheduledTaskImportModal);
export default EsattoScheduledTaskImportModal;
