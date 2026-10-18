import { css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement, umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { clearRuns, getRuns } from '@esatto/scheduled-tasks/api.js';
import { formatElapsed, formatInterval } from '@esatto/scheduled-tasks/format.js';

const OUTCOME_COLORS = {
    succeeded: 'positive',
    failed: 'danger',
    running: 'default',
    skipped: 'warning',
    canceled: 'warning',
};

/**
 * Sidebar listing a task's recent runs on this server. Opened by the dashboard with
 * `data: { task }`.
 */
class EsattoScheduledTaskRunsModal extends UmbModalBaseElement {
    static properties = {
        _runs: { state: true },
        _loading: { state: true },
        _error: { state: true },
    };

    constructor() {
        super();
        this._runs = [];
        this._loading = true;
        this._error = '';
    }

    connectedCallback() {
        super.connectedCallback();
        this.#load();
    }

    get #task() {
        return this.data?.task;
    }

    async #load() {
        if (!this.#task) return;
        this._loading = true;
        const { data, error } = await getRuns(this, this.#task.id);
        this._runs = data ?? [];
        this._error = error ? 'Could not load the run history.' : '';
        this._loading = false;
    }

    async #clear() {
        try {
            await umbConfirmModal(this, {
                headline: 'Clear run history?',
                content: `Forget the recorded runs of “${this.#task.name}” on this server.`,
                confirmLabel: 'Clear',
                color: 'danger',
            });
        } catch {
            return;
        }
        await clearRuns(this, this.#task.id);
        await this.#load();
    }

    /** What the job reported through IScheduledTaskProgress: a bar while running, the last step after. */
    #renderProgress(run) {
        if (run.progress == null && !run.progressMessage) return nothing;
        const percent = run.progress == null ? null : Math.round(run.progress);
        if (run.outcome === 'running') {
            return html`
                <div class="progress">
                    ${percent == null ? nothing : html`<uui-progress-bar progress=${percent}></uui-progress-bar><span class="muted">${percent}%</span>`}
                </div>
                ${run.progressMessage ? html`<p class="muted">${run.progressMessage}</p>` : nothing}`;
        }
        const last = [percent == null ? null : `${percent}%`, run.progressMessage].filter(Boolean).join(' · ');
        return html`<p class="muted">Last progress: ${last}</p>`;
    }

    #renderRun(run) {
        const started = new Date(run.startedUtc).toLocaleString();
        const trigger = run.trigger === 'manual'
            ? `Run now${run.triggeredBy ? ` by ${run.triggeredBy}` : ''}`
            : run.trigger === 'code' ? 'Triggered from code' : 'Schedule';

        return html`
            <li class="run">
                <div class="run-head">
                    <uui-tag color=${OUTCOME_COLORS[run.outcome] ?? 'default'}>${run.outcome}</uui-tag>
                    <span class="when">${started}</span>
                    ${run.durationMs != null && run.outcome !== 'skipped'
                        ? html`<span class="muted">${formatElapsed(run.durationMs)}</span>`
                        : nothing}
                </div>
                <div class="muted">
                    ${trigger} · ${run.server}
                    ${run.occurrences > 1
                        ? html` · ${run.occurrences}× until ${new Date(run.lastOccurrenceUtc).toLocaleString()}`
                        : nothing}
                </div>
                ${this.#renderProgress(run)}
                ${run.message ? html`<p class="message outcome-${run.outcome}">${run.message}</p>` : nothing}
                ${run.details
                    ? html`<details><summary>Error details</summary><pre>${run.details}</pre></details>`
                    : nothing}
            </li>
        `;
    }

    render() {
        const task = this.#task;
        return html`
            <umb-body-layout headline=${`History: ${task?.name ?? ''}`}>
                <uui-box>
                    <p class="summary">
                        ${formatInterval(task?.periodSeconds)} · ${task?.runCount ?? 0} runs and
                        ${task?.failureCount ?? 0} failures on this server since startup.
                    </p>
                    <p class="muted">
                        History lives in memory on this server (latest runs only) and resets when the site restarts.
                    </p>
                    <p class="muted"><code>${task?.typeName}</code></p>
                </uui-box>

                <uui-box headline="Runs">
                    <uui-button slot="header-actions" compact look="secondary" label="Refresh" @click=${() => this.#load()}>
                        <uui-icon name="icon-refresh"></uui-icon> Refresh
                    </uui-button>
                    ${this._error ? html`<p role="alert" class="error">${this._error}</p>` : nothing}
                    ${this._loading
                        ? html`<uui-loader></uui-loader>`
                        : this._runs.length === 0
                            ? html`<p class="empty">No runs recorded on this server yet.</p>`
                            : html`<ul class="runs">${this._runs.map((run) => this.#renderRun(run))}</ul>`}
                </uui-box>

                <div slot="actions">
                    <uui-button
                        look="secondary"
                        color="danger"
                        label="Clear history"
                        ?disabled=${this._runs.length === 0}
                        @click=${() => this.#clear()}></uui-button>
                    <uui-button look="primary" label="Close" @click=${() => this._rejectModal()}></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    static styles = css`
        .progress { display: flex; align-items: center; gap: var(--uui-size-space-3); margin-block-start: var(--uui-size-space-2); max-width: 24rem; }
        .progress uui-progress-bar { flex: 1; }
        uui-box + uui-box { margin-block-start: var(--uui-size-space-5); }
        .summary { margin-block-start: 0; }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-font-size-small, 0.875em); margin: 0; }
        p.muted + p.muted { margin-block-start: var(--uui-size-space-2); }
        .runs { list-style: none; margin: 0; padding: 0; }
        .run { padding-block: var(--uui-size-space-4); border-block-end: 1px solid var(--uui-color-divider); }
        .run:last-child { border-block-end: none; }
        .run-head { display: flex; align-items: center; gap: var(--uui-size-space-3); margin-block-end: var(--uui-size-space-1); }
        .run-head uui-tag { text-transform: capitalize; }
        .message { margin: var(--uui-size-space-2) 0 0; word-break: break-word; }
        .message.outcome-failed { color: var(--uui-color-danger); }
        details { margin-block-start: var(--uui-size-space-2); }
        summary { cursor: pointer; color: var(--uui-color-interactive); }
        pre {
            max-height: 20rem;
            overflow: auto;
            padding: var(--uui-size-space-3);
            background: var(--uui-color-surface-alt);
            border-radius: var(--uui-border-radius);
            font-size: 12px;
            white-space: pre-wrap;
            word-break: break-word;
        }
        .error { color: var(--uui-color-danger); }
        .empty { color: var(--uui-color-text-alt); font-style: italic; }
    `;
}

customElements.define('esatto-scheduled-task-runs-modal', EsattoScheduledTaskRunsModal);
export default EsattoScheduledTaskRunsModal;
