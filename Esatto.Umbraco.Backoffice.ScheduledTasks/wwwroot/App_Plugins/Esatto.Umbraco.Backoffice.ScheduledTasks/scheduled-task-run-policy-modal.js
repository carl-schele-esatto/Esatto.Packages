import { css, html, live, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { errorMessage, setRunPolicy } from '@esatto/scheduled-tasks/api.js';
import { INTERVAL_UNITS, describeRunPolicy, splitInterval, termsOf, toSeconds } from '@esatto/scheduled-tasks/format.js';

const UNITS = [...INTERVAL_UNITS].reverse().filter((u) => u.unit !== 'weeks');
const MAX_RETRIES = 10;
const MAX_RETRY_DELAY = 86400;
const MAX_TIMEOUT = 7 * 86400;

/**
 * Sidebar for a task's retries and time limit (its run policy). Opened by the dashboard with
 * `data: { task, onSavedAfterClose }`; submits `{ task, message }` after saving.
 */
class EsattoScheduledTaskRunPolicyModal extends UmbModalBaseElement {
    static properties = {
        _retries: { state: true },
        _delay: { state: true },
        _limited: { state: true },
        _timeout: { state: true },
        _error: { state: true },
        _saving: { state: true },
    };

    #seeded = false;

    constructor() {
        super();
        this._retries = '0';
        this._delay = { value: '1', unit: 'minutes' };
        this._limited = false;
        this._timeout = { value: '10', unit: 'minutes' };
        this._error = '';
        this._saving = false;
    }

    get #t() {
        return termsOf(this.localize);
    }

    connectedCallback() {
        super.connectedCallback();
        // Seed once; re-attaching must not throw away unsaved edits.
        if (this.#seeded) return;
        this.#seeded = true;

        const policy = this.data.task.runPolicy;
        if (!policy) return;
        this._retries = String(policy.retries);
        this._delay = this.#split(policy.retryDelaySeconds);
        this._limited = policy.timeoutSeconds != null;
        if (policy.timeoutSeconds != null) this._timeout = this.#split(policy.timeoutSeconds);
    }

    #split(seconds) {
        const { value, unit } = splitInterval(seconds);
        // Weeks aren't offered here: 7 days reads better for a wait or a limit.
        return unit === 'weeks' ? { value: String(value * 7), unit: 'days' } : { value: String(value), unit };
    }

    /** The values as the API takes them, or null with _error set. */
    #policy() {
        const t = this.#t;
        const retries = Number(this._retries);
        if (!Number.isInteger(retries) || retries < 0 || retries > MAX_RETRIES) {
            this._error = t('invalidRetries');
            return null;
        }

        const retryDelaySeconds = toSeconds(this._delay.value, this._delay.unit);
        if (Number.isNaN(retryDelaySeconds) || retryDelaySeconds < 1 || retryDelaySeconds > MAX_RETRY_DELAY) {
            this._error = t('invalidRetryDelay');
            return null;
        }

        let timeoutSeconds = 0;
        if (this._limited) {
            timeoutSeconds = toSeconds(this._timeout.value, this._timeout.unit);
            if (Number.isNaN(timeoutSeconds) || timeoutSeconds < 1 || timeoutSeconds > MAX_TIMEOUT) {
                this._error = t('invalidTimeout');
                return null;
            }
        }

        return { retries, retryDelaySeconds, timeoutSeconds };
    }

    async #save(policy) {
        const t = this.#t;
        this._saving = true;
        this._error = '';
        const { data, error } = await setRunPolicy(this, this.data.task.id, policy);
        this._saving = false;
        if (error) {
            this._error = errorMessage(error, t('savePolicyFailed'));
            return;
        }

        const reset = policy === null || data?.task?.runPolicy?.isCustomised === false;
        this.#finish({ task: data?.task, message: t(reset ? 'policyReset' : 'policySaved') });
    }

    #onSave() {
        const policy = this.#policy();
        if (policy) this.#save(policy);
    }

    /** As the schedule sidebar: a result whose sidebar was closed meanwhile goes to the opener's callback. */
    #finish(result) {
        if (!this.isConnected || !this.modalContext || this.modalContext.isResolved?.()) {
            this.data?.onSavedAfterClose?.(result);
            return;
        }
        this.value = result;
        this._submitModal();
    }

    #renderUnitSelect(current, onChange, label) {
        const t = this.#t;
        return html`
            <select class="text-input" aria-label=${label} @change=${(e) => onChange(e.target.value)}>
                ${UNITS.map((u) => html`<option value=${u.unit} ?selected=${u.unit === current}>${t(u.term)}</option>`)}
            </select>`;
    }

    render() {
        const t = this.#t;
        const task = this.data?.task;
        const policy = task?.runPolicy;
        const defaults = policy
            ? describeRunPolicy(policy.defaultRetries, policy.defaultRetryDelaySeconds, policy.defaultTimeoutSeconds, t)
            : '';

        return html`
            <umb-body-layout headline=${t('runPolicyHeadline', task?.name ?? '')}>
                <uui-box>
                    <p class="intro">${t('runPolicyIntro')}</p>

                    <div class="field">
                        <label for="retries">${t('retriesLabel')}</label>
                        <input
                            id="retries"
                            class="text-input number-input"
                            type="number"
                            min="0"
                            max=${MAX_RETRIES}
                            step="1"
                            aria-describedby="retries-help"
                            .value=${live(this._retries)}
                            @input=${(e) => { this._retries = e.target.value; this._error = ''; }}>
                        <p id="retries-help" class="muted">${t('retriesHelp')}</p>
                    </div>

                    <div class="field">
                        <label for="retry-delay">${t('retryDelayLabel')}</label>
                        <div class="row">
                            <input
                                id="retry-delay"
                                class="text-input number-input"
                                type="number"
                                step="any"
                                ?disabled=${Number(this._retries) === 0}
                                .value=${live(this._delay.value)}
                                @input=${(e) => { this._delay = { ...this._delay, value: e.target.value }; this._error = ''; }}>
                            ${this.#renderUnitSelect(this._delay.unit, (unit) => { this._delay = { ...this._delay, unit }; }, t('unit'))}
                        </div>
                        <p class="muted">${t(task?.kind === 'distributed' ? 'distributedRetryNote' : 'recurringRetryNote')}</p>
                    </div>

                    <div class="field">
                        <span class="label">${t('timeLimitLabel')}</span>
                        <uui-toggle
                            label=${t('timeLimitToggle')}
                            .checked=${this._limited}
                            @change=${(e) => { this._limited = e.target.checked; this._error = ''; }}></uui-toggle>
                        ${this._limited
                            ? html`
                                <div class="row">
                                    <input
                                        id="timeout"
                                        class="text-input number-input"
                                        type="number"
                                        step="any"
                                        aria-label=${t('timeLimitLabel')}
                                        .value=${live(this._timeout.value)}
                                        @input=${(e) => { this._timeout = { ...this._timeout, value: e.target.value }; this._error = ''; }}>
                                    ${this.#renderUnitSelect(this._timeout.unit, (unit) => { this._timeout = { ...this._timeout, unit }; }, t('unit'))}
                                </div>`
                            : nothing}
                        <p class="muted">${t('timeLimitHelp')}</p>
                    </div>

                    ${defaults ? html`<p class="muted">${t('taskDefaults', defaults)}</p>` : nothing}
                    ${this._error ? html`<p role="alert" class="error">${this._error}</p>` : nothing}
                </uui-box>

                <div slot="actions">
                    ${policy?.isCustomised
                        ? html`<uui-button
                            look="secondary"
                            label=${t('useDefaults')}
                            ?disabled=${this._saving}
                            @click=${() => this.#save(null)}></uui-button>`
                        : nothing}
                    <uui-button label=${t('cancel')} ?disabled=${this._saving} @click=${() => this._rejectModal()}></uui-button>
                    <uui-button
                        look="primary"
                        color="positive"
                        label=${t('save')}
                        .state=${this._saving ? 'waiting' : undefined}
                        ?disabled=${this._saving}
                        @click=${() => this.#onSave()}></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    static styles = css`
        .intro { margin-block-start: 0; }
        .field { display: flex; flex-direction: column; gap: var(--uui-size-space-2); margin-block-end: var(--uui-size-space-5); }
        .field label, .field .label { font-weight: var(--uui-font-weight-bold, 700); }
        .row { display: flex; gap: var(--uui-size-space-3); align-items: center; }
        .text-input {
            box-sizing: border-box;
            padding: var(--uui-size-space-2, 6px) var(--uui-size-space-3, 9px);
            border: 1px solid var(--uui-color-border, #d8d7d9);
            border-radius: var(--uui-border-radius, 3px);
            font: inherit;
            color: inherit;
            background: var(--uui-color-surface, #fff);
            max-width: 22rem;
        }
        .text-input:focus { outline: none; border-color: var(--uui-color-focus, #3879ff); box-shadow: 0 0 0 1px var(--uui-color-focus, #3879ff); }
        .text-input:disabled { opacity: 0.55; }
        .number-input { width: 7rem; }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-font-size-small, 0.875em); margin: 0; }
        .error { color: var(--uui-color-danger); }
    `;
}

customElements.define('esatto-scheduled-task-run-policy-modal', EsattoScheduledTaskRunPolicyModal);
export default EsattoScheduledTaskRunPolicyModal;
