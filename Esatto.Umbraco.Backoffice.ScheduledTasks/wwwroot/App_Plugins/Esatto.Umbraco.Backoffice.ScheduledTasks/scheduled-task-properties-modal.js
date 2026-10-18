import { css, html, nothing, repeat } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement, umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { UmbDataPathPropertyValueQuery, UmbValidationContext } from '@umbraco-cms/backoffice/validation';
import '@umbraco-cms/backoffice/property'; // <umb-property-dataset>, <umb-property>
import { errorMessage, getProperties, setProperties } from '@esatto/scheduled-tasks/api.js';
import { termsOf } from '@esatto/scheduled-tasks/format.js';

const dataPath = (alias) => `$.values[${UmbDataPathPropertyValueQuery({ alias })}].value`;

// [alias, messages] pairs from a ProblemDetails `errors`: { alias: [...] } as the API sends it, or
// the [{ "$.alias": [...] }] shape Umbraco gives a ValidationProblemDetails.
const fieldErrors = (errors) =>
    (Array.isArray(errors) ? errors.flatMap((e) => Object.entries(e ?? {})) : Object.entries(errors ?? {}))
        .map(([key, messages]) => [key.replace(/^\$\./, ''), messages]);

// A toggle is always on or off; Umbraco's toggle reads mandatory as "must be on", which the server
// doesn't (and doesn't send), so never pass it.
const isMandatory = (d) => !!d.validation?.mandatory && d.propertyEditorUiAlias !== 'Umb.PropertyEditorUi.Toggle';

/**
 * Sidebar for a task's properties (see IScheduledTaskWithProperties in the package). Renders each
 * property with Umbraco's own property editor UI inside a property dataset, the same pattern
 * Umbraco's own property settings modals use. Opened by the dashboard with `data: { task }`;
 * submits `{ task, message }` after saving.
 */
class EsattoScheduledTaskPropertiesModal extends UmbModalBaseElement {
    static properties = {
        _form: { state: true },
        _loading: { state: true },
        _saving: { state: true },
        _error: { state: true },
    };

    // Own validation context: the property editors' validators register here, not with whatever
    // opened the modal.
    #validation = new UmbValidationContext(this);

    // The dataset's value, [{ alias, value }]. Not reactive: the dataset owns it while editing.
    #values = [];
    #loadStarted = false;

    get #t() {
        return termsOf(this.localize);
    }

    constructor() {
        super();
        this._form = null;
        this._loading = true;
        this._saving = false;
        this._error = '';
    }

    connectedCallback() {
        super.connectedCallback();
        // Load once; re-attaching the element must not throw away unsaved edits.
        if (this.#loadStarted) return;
        this.#loadStarted = true;
        this.#load();
    }

    async #load() {
        const { data, error } = await getProperties(this, this.data.task.id);
        if (error) {
            this._error = errorMessage(error, this.#t('loadPropertiesFailed'));
        } else {
            this.#values = data.values.map((v) => ({ alias: v.alias, value: v.value }));
            this._form = data;
        }
        this._loading = false;
    }

    #onChange(event) {
        this.#values = event.target.value;
    }

    async #save() {
        // The last save's server messages go: validate() rejects while any message is left, and
        // one the fields didn't clear would otherwise block every later save.
        this.#validation.messages.removeMessagesByType('server');
        try {
            await this.#validation.validate(); // rejects when a field is invalid; the fields show why
        } catch {
            return;
        }

        this._saving = true;
        this._error = '';
        const values = this.#values.map((v) => ({ alias: v.alias, value: v.value ?? null }));
        const { data, error } = await setProperties(this, this.data.task.id, values);
        this._saving = false;

        if (error) {
            this.#showErrors(error);
            return;
        }

        this.value = { task: data?.task, message: this.#t(data?.task?.hasCustomProperties === false ? 'propertiesReset' : 'propertiesSaved') };
        this._submitModal();
    }

    async #resetToDefaults() {
        try {
            await umbConfirmModal(this, {
                headline: this.#t('resetPropertiesHeadline'),
                content: this.#t('resetPropertiesContent', this.data.task.name),
                confirmLabel: this.#t('reset'),
                color: 'danger',
            });
        } catch {
            return;
        }

        this._saving = true;
        const { data, error } = await setProperties(this, this.data.task.id, null);
        this._saving = false;

        if (error) {
            this._error = errorMessage(error, this.#t('resetPropertiesFailed'));
            return;
        }

        this.value = { task: data?.task, message: this.#t('propertiesReset') };
        this._submitModal();
    }

    /** Puts the server's per-field problems on the fields, and the summary at the top. */
    #showErrors(error) {
        this._error = errorMessage(error, this.#t('savePropertiesFailed'));
        const aliases = new Set(this._form?.definitions.filter((d) => !d.error).map((d) => d.alias) ?? []);
        for (const [alias, messages] of fieldErrors(error?.problemDetails?.errors)) {
            // Only on fields that are shown: a message no field clears would keep failing validate().
            if (aliases.has(alias)) {
                this.#validation.messages.addMessage('server', dataPath(alias), [].concat(messages).join(' '));
            }
        }
    }

    #renderForm() {
        const form = this._form;
        if (!form) return html`<uui-box><p role="alert" class="error">${this._error}</p></uui-box>`;

        const editable = form.definitions.filter((d) => !d.error);
        const broken = form.definitions.filter((d) => d.error);

        return html`
            <uui-box>
                <p class="intro">
                    ${this.#t('propertiesIntro')}
                    ${form.isCustomised ? nothing : html`<br><span class="muted">${this.#t('allDefaults')}</span>`}
                </p>
                ${form.warnings.map((w) => html`<p class="warning">${w}</p>`)}
                ${this._error ? html`<p role="alert" class="error">${this._error}</p>` : nothing}

                <umb-property-dataset .value=${this.#values} @change=${this.#onChange}>
                    ${repeat(
                        editable,
                        (d) => d.alias,
                        (d) => html`
                            <umb-property
                                data-path=${dataPath(d.alias)}
                                alias=${d.alias}
                                label=${d.label}
                                description=${d.description ?? ''}
                                property-editor-ui-alias=${d.propertyEditorUiAlias}
                                .appearance=${{ labelOnTop: true }}
                                .config=${d.config}
                                .validation=${{ mandatory: isMandatory(d) }}></umb-property>
                        `,
                    )}
                </umb-property-dataset>

                ${broken.length
                    ? html`
                        <div class="broken">
                            <strong>${this.#t('notEditable')}</strong>
                            <ul>${broken.map((d) => html`<li><code>${d.alias}</code>: ${d.error}</li>`)}</ul>
                        </div>`
                    : nothing}
            </uui-box>
        `;
    }

    render() {
        const task = this.data?.task;
        return html`
            <umb-body-layout headline=${this.#t('propertiesHeadline', task?.name ?? '')}>
                ${this._loading ? html`<uui-loader></uui-loader>` : this.#renderForm()}
                <div slot="actions">
                    ${this._form?.isCustomised
                        ? html`<uui-button
                            look="secondary"
                            color="danger"
                            label=${this.#t('resetToDefaults')}
                            ?disabled=${this._saving}
                            @click=${() => this.#resetToDefaults()}></uui-button>`
                        : nothing}
                    <uui-button label=${this.#t('cancel')} ?disabled=${this._saving} @click=${() => this._rejectModal()}></uui-button>
                    <uui-button
                        look="primary"
                        color="positive"
                        label=${this.#t('save')}
                        .state=${this._saving ? 'waiting' : undefined}
                        ?disabled=${this._saving || this._loading || !this._form}
                        @click=${() => this.#save()}></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    static styles = css`
        .intro { margin-block-start: 0; }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-font-size-small, 0.875em); }
        .warning { color: var(--uui-color-warning-standalone, #8a6d00); }
        .error { color: var(--uui-color-danger); }
        .broken { margin-block-start: var(--uui-size-space-5); color: var(--uui-color-text-alt); }
        .broken ul { margin: var(--uui-size-space-2) 0 0; padding-inline-start: var(--uui-size-space-5); }
        umb-property { --uui-size-layout-1: var(--uui-size-space-4); }
    `;
}

customElements.define('esatto-scheduled-task-properties-modal', EsattoScheduledTaskPropertiesModal);
export default EsattoScheduledTaskPropertiesModal;
