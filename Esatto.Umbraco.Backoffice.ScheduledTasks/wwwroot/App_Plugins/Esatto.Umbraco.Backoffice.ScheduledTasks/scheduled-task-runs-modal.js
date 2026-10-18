import { css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement, umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { clearRuns, getChanges, getRuns } from '@esatto/scheduled-tasks/api.js';
import { formatElapsed, formatInterval, formatRelative, logLines, termsOf } from '@esatto/scheduled-tasks/format.js';

const OUTCOME_COLORS = {
    succeeded: 'positive',
    failed: 'danger',
    running: 'default',
    skipped: 'warning',
    canceled: 'warning',
};

const OUTCOME_TERMS = {
    running: 'outcomeRunning',
    succeeded: 'outcomeSucceeded',
    failed: 'outcomeFailed',
    skipped: 'outcomeSkipped',
    canceled: 'outcomeCanceled',
};

// ScheduledTaskAuditActions in the package.
const AUDIT_ACTIONS = new Set([
    'paused', 'resumed', 'intervalChanged', 'scheduleChanged', 'scheduleReset', 'propertiesChanged', 'propertiesReset',
    'runPolicyChanged', 'runStarted', 'stopRequested', 'retryCanceled', 'historyCleared', 'runReset', 'imported',
]);

/**
 * Sidebar with a task's recent runs (every server's when the history is saved in the database)
 * and its change log. Opened by the dashboard with `data: { task, historyPersisted, runHistoryLimit }`.
 */
class EsattoScheduledTaskRunsModal extends UmbModalBaseElement {
    static properties = {
        _tab: { state: true },
        _runs: { state: true },
        _changes: { state: true },
        _loading: { state: true },
        _error: { state: true },
    };

    // Responses from a load started before the latest tab switch or refresh are dropped.
    #loadSeq = 0;

    constructor() {
        super();
        this._tab = 'runs';
        this._runs = [];
        this._changes = null;
        this._loading = true;
        this._error = '';
    }

    get #t() {
        return termsOf(this.localize);
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
        const seq = ++this.#loadSeq;
        this._loading = true;
        if (this._tab === 'runs') {
            const { data, error } = await getRuns(this, this.#task.id);
            if (seq !== this.#loadSeq) return; // the tab changed, or a newer refresh is on its way
            this._runs = data ?? [];
            this._error = error ? this.#t('loadRunsFailed') : '';
        } else {
            const { data, error } = await getChanges(this, this.#task.id);
            if (seq !== this.#loadSeq) return;
            this._changes = data ?? [];
            this._error = error ? this.#t('loadChangesFailed') : '';
        }
        this._loading = false;
    }

    #switchTab(tab) {
        if (this._tab === tab) return;
        this._tab = tab;
        this._error = '';
        this.#load();
    }

    async #clear() {
        const t = this.#t;
        try {
            await umbConfirmModal(this, {
                headline: t('clearHeadline'),
                content: t('clearContent', this.#task.name),
                confirmLabel: t('clear'),
                color: 'danger',
            });
        } catch {
            return;
        }
        const { error } = await clearRuns(this, this.#task.id);
        if (error) this._error = this.#t('clearFailed');
        await this.#load();
    }

    #date(iso) {
        return this.localize.date(iso, { dateStyle: 'short', timeStyle: 'medium' });
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
        return html`<p class="muted">${this.#t('lastProgress', last)}</p>`;
    }

    /** The lines the job logged during the run, with their time in the user's time zone. */
    #renderLog(run) {
        const lines = logLines(run.log);
        if (lines.length === 0) return nothing;
        const time = (date) => (date ? this.localize.date(date, { timeStyle: 'medium' }) : '');
        return html`
            <details ?open=${run.outcome === 'running'}>
                <summary>${this.#t('logLines', lines.length)}</summary>
                <ol class="log">
                    ${lines.map((line) => html`
                        <li class="level-${(line.level ?? 'inf').toLowerCase()}">
                            ${line.time ? html`<time datetime=${line.time.toISOString()}>${time(line.time)}</time>` : nothing}
                            ${line.level ? html`<span class="level">${line.level}</span>` : nothing}
                            <span class="text">${line.text}</span>
                        </li>`)}
                </ol>
            </details>`;
    }

    #renderRun(run) {
        const t = this.#t;
        const trigger = {
            manual: run.triggeredBy ? t('triggerManualBy', run.triggeredBy) : t('triggerManual'),
            code: t('triggerCode'),
            retry: t('triggerRetry'),
        }[run.trigger] ?? t('triggerSchedule');
        const meta = [trigger, run.maxAttempts > 1 ? t('attempt', run.attempt, run.maxAttempts) : null, run.server]
            .filter(Boolean)
            .join(' · ');

        return html`
            <li class="run">
                <div class="run-head">
                    <uui-tag color=${OUTCOME_COLORS[run.outcome] ?? 'default'}>${t(OUTCOME_TERMS[run.outcome] ?? 'outcomeRunning')}</uui-tag>
                    <span class="when">${this.#date(run.startedUtc)}</span>
                    ${run.durationMs != null && run.outcome !== 'skipped'
                        ? html`<span class="muted">${formatElapsed(run.durationMs)}</span>`
                        : nothing}
                </div>
                <div class="muted">
                    ${meta}
                    ${run.occurrences > 1 ? html` · ${t('repeated', run.occurrences, this.#date(run.lastOccurrenceUtc))}` : nothing}
                </div>
                ${this.#renderProgress(run)}
                ${run.message ? html`<p class="message outcome-${run.outcome}">${run.message}</p>` : nothing}
                ${run.stopRequestedBy && run.outcome === 'running' ? html`<p class="message">${t('stopRequestedBy', run.stopRequestedBy)}</p>` : nothing}
                ${run.retryAtUtc
                    ? html`<p class="message retry">${t('retryAt', formatRelative(run.retryAtUtc, Date.now(), this.localize.lang()))}</p>`
                    : nothing}
                ${run.details
                    ? html`<details><summary>${t('errorDetails')}</summary><pre>${run.details}</pre></details>`
                    : nothing}
                ${this.#renderLog(run)}
            </li>
        `;
    }

    #renderChange(change) {
        const t = this.#t;
        // An action a newer version recorded shows as it was stored.
        const action = AUDIT_ACTIONS.has(change.action) ? t(`audit_${change.action}`) : change.action;
        return html`
            <li class="run">
                <div class="run-head">
                    <strong>${action}</strong>
                    <span class="when">${this.#date(change.utc)}</span>
                </div>
                <div class="muted">${t('auditBy', change.performedBy || t('unknownUser'))}${change.server ? ` · ${change.server}` : ''}</div>
                ${change.details ? html`<p class="message">${change.details}</p>` : nothing}
            </li>
        `;
    }

    #renderRuns() {
        const t = this.#t;
        if (this._runs.length === 0) return html`<p class="empty">${t('noRuns')}</p>`;
        return html`<ul class="runs">${this._runs.map((run) => this.#renderRun(run))}</ul>`;
    }

    #renderChanges() {
        const changes = this._changes ?? [];
        if (changes.length === 0) return html`<p class="empty">${this.#t('noChanges')}</p>`;
        return html`<ul class="runs">${changes.map((change) => this.#renderChange(change))}</ul>`;
    }

    render() {
        const t = this.#t;
        const task = this.#task;
        const persisted = this.data?.historyPersisted;
        return html`
            <umb-body-layout headline=${t('historyHeadline', task?.name ?? '')}>
                <uui-box>
                    <p class="summary">${t('runSummary', formatInterval(task?.periodSeconds, t), task?.runCount ?? 0, task?.failureCount ?? 0)}</p>
                    <p class="muted">${persisted ? t('historyPersistedNote', this.data?.runHistoryLimit ?? 50) : t('historyMemoryNote')}</p>
                    <p class="muted"><code>${task?.typeName}</code></p>
                </uui-box>

                <uui-box>
                    <uui-tab-group slot="headline" class="tabs">
                        <uui-tab label=${t('runsTab')} ?active=${this._tab === 'runs'} @click=${() => this.#switchTab('runs')}>${t('runsTab')}</uui-tab>
                        <uui-tab label=${t('changesTab')} ?active=${this._tab === 'changes'} @click=${() => this.#switchTab('changes')}>${t('changesTab')}</uui-tab>
                    </uui-tab-group>
                    <uui-button slot="header-actions" compact look="secondary" label=${t('refresh')} @click=${() => this.#load()}>
                        <uui-icon name="icon-refresh"></uui-icon> ${t('refresh')}
                    </uui-button>
                    ${this._error ? html`<p role="alert" class="error">${this._error}</p>` : nothing}
                    ${this._loading
                        ? html`<uui-loader></uui-loader>`
                        : this._tab === 'runs' ? this.#renderRuns() : this.#renderChanges()}
                </uui-box>

                <div slot="actions">
                    ${this._tab === 'runs'
                        ? html`<uui-button
                            look="secondary"
                            color="danger"
                            label=${t('clearHistory')}
                            ?disabled=${this._runs.length === 0}
                            @click=${() => this.#clear()}></uui-button>`
                        : nothing}
                    <uui-button look="primary" label=${t('close')} @click=${() => this._rejectModal()}></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    static styles = css`
        .progress { display: flex; align-items: center; gap: var(--uui-size-space-3); margin-block-start: var(--uui-size-space-2); max-width: 24rem; }
        .progress uui-progress-bar { flex: 1; }
        uui-box + uui-box { margin-block-start: var(--uui-size-space-5); }
        .tabs { --uui-tab-divider: transparent; }
        uui-button uui-icon { vertical-align: text-top; }
        .summary { margin-block-start: 0; }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-font-size-small, 0.875em); margin: 0; }
        p.muted + p.muted { margin-block-start: var(--uui-size-space-2); }
        .runs { list-style: none; margin: 0; padding: 0; }
        .run { padding-block: var(--uui-size-space-4); border-block-end: 1px solid var(--uui-color-divider); }
        .run:last-child { border-block-end: none; }
        .run-head { display: flex; align-items: center; gap: var(--uui-size-space-3); margin-block-end: var(--uui-size-space-1); }
        .message { margin: var(--uui-size-space-2) 0 0; word-break: break-word; }
        .message.outcome-failed { color: var(--uui-color-danger); }
        .message.retry { color: var(--uui-color-warning-standalone, #8a6d00); }
        details { margin-block-start: var(--uui-size-space-2); }
        summary { cursor: pointer; color: var(--uui-color-interactive); }
        pre, .log {
            max-height: 20rem;
            overflow: auto;
            margin: var(--uui-size-space-2) 0 0;
            padding: var(--uui-size-space-3);
            background: var(--uui-color-surface-alt);
            border-radius: var(--uui-border-radius);
            font-size: 12px;
            white-space: pre-wrap;
            word-break: break-word;
        }
        .log { list-style: none; font-family: var(--uui-font-monospace, monospace); white-space: normal; }
        .log .text { white-space: pre-wrap; }
        .log li { display: flex; gap: var(--uui-size-space-2); padding-block: 1px; }
        .log time, .log .level { color: var(--uui-color-text-alt); flex: none; }
        .log .level-wrn .level { color: var(--uui-color-warning-standalone, #8a6d00); }
        .log .level-err .level, .log .level-ftl .level, .log .level-err .text, .log .level-ftl .text { color: var(--uui-color-danger); }
        .error { color: var(--uui-color-danger); }
        .empty { color: var(--uui-color-text-alt); font-style: italic; }
    `;
}

customElements.define('esatto-scheduled-task-runs-modal', EsattoScheduledTaskRunsModal);
export default EsattoScheduledTaskRunsModal;
