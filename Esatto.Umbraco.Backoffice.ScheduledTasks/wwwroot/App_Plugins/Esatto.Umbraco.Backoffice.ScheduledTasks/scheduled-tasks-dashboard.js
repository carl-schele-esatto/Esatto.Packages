import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UmbModalToken, umbConfirmModal, umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { errorMessage, getOverview, resetRun, runNow, setPaused } from '@esatto/scheduled-tasks/api.js';
import {
    describeSchedule,
    formatElapsed,
    formatInterval,
    formatRelative,
    isOnDemand,
    matchesFilter,
    shortTypeName,
} from '@esatto/scheduled-tasks/format.js';

// Modals are opened by alias (their element modules are only ever loaded through the manifest).
const RUNS_MODAL = new UmbModalToken('Esatto.Umbraco.Backoffice.ScheduledTasks.RunsModal', {
    modal: { type: 'sidebar', size: 'medium' },
});
const SCHEDULE_MODAL = new UmbModalToken('Esatto.Umbraco.Backoffice.ScheduledTasks.ScheduleModal', {
    modal: { type: 'sidebar', size: 'medium' },
});
const PROPERTIES_MODAL = new UmbModalToken('Esatto.Umbraco.Backoffice.ScheduledTasks.PropertiesModal', {
    modal: { type: 'sidebar', size: 'medium' },
});


// Relative times re-render every TICK_MS; data reloads every REFRESH_TICKS ticks while visible
// (every tick while a task is running).
const TICK_MS = 2500;
const REFRESH_TICKS = 4;

class EsattoScheduledTasksDashboard extends UmbElementMixin(LitElement) {
    static properties = {
        _overview: { state: true },
        _loaded: { state: true },
        _loadError: { state: true },
        _busyIds: { state: true },
        _filter: { state: true },
        _showUmbraco: { state: true },
        _autoRefresh: { state: true },
        _now: { state: true },
    };

    #notifications;
    #timer;
    #ticks = 0;
    #loading = false;
    // When the last command (run, pause, interval…) finished; overview responses requested before
    // that are older than the command's own result, so they're dropped.
    #commandCompletedAt = 0;

    constructor() {
        super();
        this._overview = null;
        this._loaded = false;
        this._loadError = '';
        this._busyIds = new Set();
        this._filter = '';
        this._showUmbraco = true;
        this._autoRefresh = true;
        this._now = Date.now();

        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notifications = ctx; });
    }

    connectedCallback() {
        super.connectedCallback();
        this.#load();
        this.#timer = setInterval(() => this.#tick(), TICK_MS);
    }

    disconnectedCallback() {
        clearInterval(this.#timer);
        super.disconnectedCallback();
    }

    #tick() {
        this._now = Date.now();
        this.#ticks = (this.#ticks + 1) % REFRESH_TICKS;
        // While something runs, refresh on every tick so its progress bar moves.
        const running = this._overview?.tasks.some((t) => t.isRunning);
        if ((this.#ticks === 0 || running) && this._autoRefresh && document.visibilityState === 'visible') {
            this.#load({ quiet: true });
        }
    }

    async #load({ quiet = false } = {}) {
        if (this.#loading) return;
        this.#loading = true;
        const requestedAt = performance.now();
        try {
            const { data, error } = await getOverview(this);
            if (error) throw error;
            if (requestedAt < this.#commandCompletedAt) return; // stale: a command's newer result is showing
            this._overview = data;
            this._loadError = '';
        } catch (err) {
            // A background refresh failing (e.g. an expired session) shouldn't spam toasts.
            if (!quiet) this._loadError = errorMessage(err, 'Could not load the scheduled tasks.');
        } finally {
            this._loaded = true;
            this._now = Date.now();
            this.#loading = false;
        }
    }

    #replaceTask(task) {
        if (!task || !this._overview) return;
        this._overview = {
            ...this._overview,
            tasks: this._overview.tasks.map((t) => (t.id === task.id ? task : t)),
        };
    }

    async #withBusy(task, action) {
        this._busyIds = new Set(this._busyIds).add(task.id);
        try {
            await action();
        } finally {
            this.#commandCompletedAt = performance.now();
            const next = new Set(this._busyIds);
            next.delete(task.id);
            this._busyIds = next;
        }
    }

    async #resetRun(task) {
        try {
            await umbConfirmModal(this, {
                headline: `Reset “${task.name}”?`,
                content: 'It has been marked as running for longer than Umbraco allows for a run, usually because the site stopped in the middle of it. Only reset it if no server is running it any more; it then runs again when it’s due.',
                confirmLabel: 'Reset',
                color: 'danger',
            });
        } catch {
            return; // cancelled
        }

        await this.#withBusy(task, async () => {
            const { data, error } = await resetRun(this, task.id, task.runningSinceUtc ?? null);
            if (error) {
                this.#peek('warning', errorMessage(error, 'Could not reset the task.'));
                return;
            }
            this.#replaceTask(data?.task);
            this.#peek('positive', `${task.name}: ${data?.message ?? 'reset.'}`);
        });
    }

    async #runNow(task) {
        await this.#withBusy(task, async () => {
            const { data, error } = await runNow(this, task.id);
            if (error) {
                this.#peek('warning', errorMessage(error, 'Could not start the task.'));
                return;
            }
            this.#replaceTask(data?.task);
            this.#peek('positive', `${task.name}: ${data?.message ?? 'run started.'}`);
            // The run starts on the job's own thread; refresh shortly to show it running/finished.
            setTimeout(() => this.#load({ quiet: true }), 1500);
        });
    }

    async #togglePaused(task) {
        const pause = !task.isPaused;
        if (pause && task.isUmbraco) {
            try {
                await umbConfirmModal(this, {
                    headline: `Pause “${task.name}”?`,
                    content: 'This is one of Umbraco’s own tasks. Pausing it can stop housekeeping Umbraco relies on, such as cache synchronisation, scheduled publishing or clean-up, until you resume it.',
                    confirmLabel: 'Pause',
                    color: 'danger',
                });
            } catch {
                return; // cancelled
            }
        }

        await this.#withBusy(task, async () => {
            const { data, error } = await setPaused(this, task.id, pause);
            if (error) {
                this.#peek('danger', errorMessage(error, pause ? 'Pause failed.' : 'Resume failed.'));
                return;
            }
            this.#replaceTask(data?.task);
        });
    }

    #openRuns(task) {
        umbOpenModal(this, RUNS_MODAL, { data: { task } }).catch(() => {});
    }

    /** Opens a modal that edits the task and submits { task, message }; applies the result. */
    async #openEditor(token, task) {
        // A save still in flight when the sidebar is closed (Esc, backdrop) reports here instead.
        const onSavedAfterClose = (late) => this.#applyEditorResult(task, late);
        let result;
        try {
            result = await umbOpenModal(this, token, { data: { task, onSavedAfterClose } });
        } catch {
            return; // closed without saving
        }
        this.#applyEditorResult(task, result);
    }

    #applyEditorResult(task, result) {
        this.#commandCompletedAt = performance.now();
        this.#replaceTask(result?.task);
        if (result?.message) this.#peek('positive', `${task.name}: ${result.message}`);
    }

    #peek(color, message) {
        this.#notifications?.peek(color, { data: { message } });
    }

    #visibleTasks() {
        const tasks = this._overview?.tasks ?? [];
        return tasks.filter((t) => (this._showUmbraco || !t.isUmbraco) && matchesFilter(t, this._filter));
    }

    // ---- rendering ----

    #renderStatus(task) {
        const since = task.runningSinceUtc ? new Date(task.runningSinceUtc).toLocaleString() : null;
        if (task.isPossiblyStuck) {
            return html`<uui-tag color="warning" title=${`Marked as running since ${since}, longer than Umbraco allows for a run. If no server is running it any more, reset it.`}>Possibly stuck</uui-tag>`;
        }
        if (task.isRunning) {
            return html`
                <uui-tag color="positive" title=${since ? `Running since ${since}` : ''}><uui-loader-circle></uui-loader-circle> Running</uui-tag>
                ${this.#renderProgress(task)}`;
        }
        if (task.isPaused) {
            return html`<uui-tag color="warning" title=${this.#changedBy(task)}>Paused</uui-tag>`;
        }
        if (!task.runsOnThisServer) {
            return html`<uui-tag look="outline" title=${`Runs on servers with role ${task.serverRoles.join(' or ')}; this server is ${this._overview.serverRole}.`}>Other servers</uui-tag>`;
        }
        if (task.lastRun?.outcome === 'failed') {
            return html`<uui-tag color="danger">Failing</uui-tag>`;
        }
        if (isOnDemand(task)) {
            return html`<uui-tag look="secondary">On demand</uui-tag>`;
        }
        return html`<uui-tag look="secondary">Scheduled</uui-tag>`;
    }

    /** The progress the running job reported (IScheduledTaskProgress), if any. */
    #renderProgress(task) {
        if (task.progress == null && !task.progressMessage) return nothing;
        const percent = task.progress == null ? null : Math.round(task.progress);
        return html`
            <div class="progress">
                ${percent == null
                    ? nothing
                    : html`<uui-progress-bar progress=${percent} aria-label=${`${task.name}: ${percent}%`}></uui-progress-bar>
                        <span class="muted">${percent}%</span>`}
            </div>
            ${task.progressMessage ? html`<div class="muted progress-message" title=${task.progressMessage}>${task.progressMessage}</div>` : nothing}
        `;
    }

    #renderSchedule(task) {
        const busy = this._busyIds.has(task.id);
        const text = task.schedule ? describeSchedule(task.schedule) : formatInterval(task.periodSeconds);
        const zone = task.schedule?.timeZone ? ` (times in ${task.schedule.timeZone})` : '';

        // The schedule itself is the edit button, so it reads as something you can change. Its
        // accessible name starts with the visible text (voice control) and names the task.
        return html`
            <uui-button
                class="schedule-button"
                look="outline"
                compact
                label=${`${text}. Change the schedule of ${task.name}`}
                title=${task.isManaged ? `Change schedule${zone}` : 'Not hosted by Scheduled Tasks'}
                ?disabled=${busy || !task.isManaged}
                @click=${() => this.#openEditor(SCHEDULE_MODAL, task)}>
                ${text}
                <uui-icon name="icon-edit"></uui-icon>
            </uui-button>
            ${task.hasPeriodOverride || task.schedule
                ? html`<div class="muted" title=${this.#changedBy(task)}>Default: ${formatInterval(task.defaultPeriodSeconds).toLowerCase()}</div>`
                : nothing}
        `;
    }

    #renderLastRun(task) {
        const run = task.lastRun;
        if (run) {
            const when = run.outcome === 'skipped' ? run.lastOccurrenceUtc : run.startedUtc;
            return html`
                <div class="last-run outcome-${run.outcome}" title=${new Date(when).toLocaleString()}>
                    <uui-icon name=${OUTCOME_ICONS[run.outcome] ?? 'icon-info'}></uui-icon>
                    <span>${formatRelative(when, this._now)}</span>
                    ${run.durationMs != null && run.outcome !== 'skipped'
                        ? html`<span class="muted">· ${formatElapsed(run.durationMs)}</span>`
                        : nothing}
                </div>
                ${run.message ? html`<div class="run-message" title=${run.message}>${run.message}</div>` : nothing}
            `;
        }
        if (task.lastRunUtc) {
            return html`<span class="muted" title="Last finish recorded in Umbraco’s distributed job table: any server, including before the last restart">
                ${formatRelative(task.lastRunUtc, this._now)} · any server
            </span>`;
        }
        return html`<span class="muted">Not since startup</span>`;
    }

    #renderNextRun(task) {
        if (task.isPaused) return html`<span class="muted">Paused</span>`;
        if (!task.runsOnThisServer || task.isRunning) return html`<span class="muted">—</span>`;
        // A calendar task's next run comes from its schedule, even when its own period is infinite.
        if (isOnDemand(task)) return html`<span class="muted">On demand</span>`;
        if (!task.nextRunUtc) return html`<span class="muted">—</span>`;

        const due = Date.parse(task.nextRunUtc) <= this._now;
        return html`<span title=${new Date(task.nextRunUtc).toLocaleString()}>${due ? 'Due now' : formatRelative(task.nextRunUtc, this._now)}</span>`;
    }

    #renderActions(task) {
        const busy = this._busyIds.has(task.id);
        const canRun = task.isManaged && task.runsOnThisServer && !task.isRunning;
        // A run that looks stuck can't be started again until it's reset.
        const runButton = task.isPossiblyStuck
            ? html`
                <uui-button
                    look="secondary"
                    color="warning"
                    compact
                    label=${`Reset: ${task.name}`}
                    title="Clear the running flag left by an interrupted run"
                    ?disabled=${busy}
                    @click=${() => this.#resetRun(task)}>
                    <uui-icon name="icon-refresh"></uui-icon> Reset
                </uui-button>`
            : html`
                <uui-button
                    look="secondary"
                    compact
                    label=${`Run now: ${task.name}`}
                    title=${canRun ? `Run ${task.name} now` : task.isRunning ? 'Already running' : 'Can’t be started from this server'}
                    ?disabled=${busy || !canRun}
                    @click=${() => this.#runNow(task)}>
                    <uui-icon name="icon-play"></uui-icon> Run now
                </uui-button>`;

        return html`
            <div class="row-actions">
                ${runButton}
                <uui-button
                    look="secondary"
                    compact
                    label=${`${task.isPaused ? 'Resume' : 'Pause'}: ${task.name}`}
                    ?disabled=${busy || !task.isManaged}
                    @click=${() => this.#togglePaused(task)}>
                    <uui-icon name=${task.isPaused ? 'icon-play' : 'icon-pause'}></uui-icon> ${task.isPaused ? 'Resume' : 'Pause'}
                </uui-button>
                <uui-button look="secondary" compact label=${`History: ${task.name}`} @click=${() => this.#openRuns(task)}>
                    <uui-icon name="icon-history"></uui-icon> History
                </uui-button>
            </div>
        `;
    }

    #renderRow(task) {
        return html`
            <uui-table-row class=${task.isPaused ? 'paused' : ''}>
                <uui-table-cell>
                    <div class="task-name">${task.name}</div>
                    <div class="task-meta">
                        <uui-tag look="outline" class="kind" title=${KIND_HELP[task.kind]}>${task.kind === 'distributed' ? 'Distributed' : 'Recurring'}</uui-tag>
                        ${task.isUmbraco ? html`<uui-tag look="outline" class="kind">Umbraco</uui-tag>` : nothing}
                        <code title=${task.typeName}>${shortTypeName(task.typeName)}</code>
                    </div>
                    ${task.isManaged ? nothing : html`<div class="muted">Not hosted by Scheduled Tasks — controls unavailable.</div>`}
                    ${task.propertyCount > 0
                        ? html`
                            <div class="task-properties">
                                <uui-button
                                    look="outline"
                                    compact
                                    label=${`Properties of ${task.name}`}
                                    title="Values the task reads when it runs"
                                    @click=${() => this.#openEditor(PROPERTIES_MODAL, task)}>
                                    <uui-icon name="icon-settings"></uui-icon> Properties
                                </uui-button>
                                <span class="muted">${task.hasCustomProperties ? 'Customised' : 'Defaults'}</span>
                            </div>`
                        : nothing}
                </uui-table-cell>
                <uui-table-cell>${this.#renderSchedule(task)}</uui-table-cell>
                <uui-table-cell class="nowrap">${this.#renderStatus(task)}</uui-table-cell>
                <uui-table-cell>${this.#renderLastRun(task)}</uui-table-cell>
                <uui-table-cell class="nowrap">${this.#renderNextRun(task)}</uui-table-cell>
                <uui-table-cell class="nowrap">${this.#renderActions(task)}</uui-table-cell>
            </uui-table-row>
        `;
    }

    #changedBy(task) {
        if (!task.settingsUpdatedUtc) return '';
        const who = task.settingsUpdatedBy ? ` by ${task.settingsUpdatedBy}` : '';
        return `Changed${who} ${new Date(task.settingsUpdatedUtc).toLocaleString()}`;
    }

    render() {
        if (!this._loaded) {
            return html`<uui-box headline="Scheduled tasks"><uui-loader></uui-loader></uui-box>`;
        }

        const overview = this._overview;
        const tasks = this.#visibleTasks();
        const total = overview?.tasks.length ?? 0;

        return html`
            <uui-box headline="Scheduled tasks">
                <div slot="header-actions" class="header-actions">
                    <uui-toggle
                        label="Auto-refresh"
                        .checked=${this._autoRefresh}
                        @change=${(e) => { this._autoRefresh = e.target.checked; }}></uui-toggle>
                    <uui-button look="secondary" compact label="Refresh" @click=${() => this.#load()}>
                        <uui-icon name="icon-refresh"></uui-icon> Refresh
                    </uui-button>
                </div>

                <p class="intro">
                    Background jobs registered in this Umbraco instance — Umbraco’s own and your site’s.
                    <strong>Recurring</strong> tasks run in-process on every server whose role matches;
                    <strong>distributed</strong> tasks run on one server at a time, scheduled through the database.
                    Pausing, schedules and properties are saved and survive restarts. Run history is kept in memory on this server.
                </p>

                ${overview
                    ? html`<p class="server">This server: <strong>${overview.serverName}</strong> · role <strong>${overview.serverRole}</strong></p>`
                    : nothing}

                ${this._loadError ? html`<p role="alert" class="error">${this._loadError}</p>` : nothing}

                <div class="toolbar">
                    <input
                        class="text-input search"
                        type="search"
                        aria-label="Filter tasks"
                        placeholder="Filter by name or type"
                        .value=${this._filter}
                        @input=${(e) => { this._filter = e.target.value; }}>
                    <uui-toggle
                        label="Show Umbraco’s tasks"
                        .checked=${this._showUmbraco}
                        @change=${(e) => { this._showUmbraco = e.target.checked; }}></uui-toggle>
                </div>

                ${tasks.length === 0
                    ? html`<p class="empty">${total === 0 ? 'No scheduled tasks are registered.' : 'No tasks match the filter.'}</p>`
                    : html`
                        <uui-table aria-label="Scheduled tasks">
                            <uui-table-head>
                                <uui-table-head-cell>Task</uui-table-head-cell>
                                <uui-table-head-cell>Schedule</uui-table-head-cell>
                                <uui-table-head-cell>Status</uui-table-head-cell>
                                <uui-table-head-cell>Last run</uui-table-head-cell>
                                <uui-table-head-cell>Next run</uui-table-head-cell>
                                <uui-table-head-cell><span class="visually-hidden">Actions</span></uui-table-head-cell>
                            </uui-table-head>
                            ${tasks.map((task) => this.#renderRow(task))}
                        </uui-table>
                    `}
            </uui-box>
        `;
    }

    static styles = css`
        :host { display: block; padding: var(--uui-size-space-5); }
        .header-actions { display: flex; align-items: center; gap: var(--uui-size-space-4); }
        .intro { margin-block: 0 var(--uui-size-space-3); max-width: 80ch; }
        .server { margin-block: 0 var(--uui-size-space-5); color: var(--uui-color-text-alt); }
        .toolbar { display: flex; align-items: center; gap: var(--uui-size-space-5); margin-block-end: var(--uui-size-space-4); }
        .search { flex: 1; max-width: 28rem; }
        .text-input {
            box-sizing: border-box;
            padding: var(--uui-size-space-2, 6px) var(--uui-size-space-3, 9px);
            border: 1px solid var(--uui-color-border, #d8d7d9);
            border-radius: var(--uui-border-radius, 3px);
            font: inherit;
            color: inherit;
            background: var(--uui-color-surface, #fff);
        }
        .text-input:focus {
            outline: none;
            border-color: var(--uui-color-focus, #3879ff);
            box-shadow: 0 0 0 1px var(--uui-color-focus, #3879ff);
        }
        uui-table { width: 100%; }
        uui-table-cell { vertical-align: top; padding-block: var(--uui-size-space-3); }
        uui-table-row.paused uui-table-cell:first-child .task-name { color: var(--uui-color-text-alt); }
        .task-name { font-weight: var(--uui-font-weight-bold, 700); }
        .task-meta { display: flex; flex-wrap: wrap; align-items: center; gap: var(--uui-size-space-2); margin-block-start: var(--uui-size-space-1); }
        .task-meta code { font-size: var(--uui-font-size-small, 0.875em); color: var(--uui-color-text-alt); }
        .task-properties { display: flex; align-items: center; gap: var(--uui-size-space-3); margin-block-start: var(--uui-size-space-3); }
        uui-tag.kind { font-size: 11px; }
        uui-tag uui-loader-circle { font-size: 0.9em; margin-inline-end: 4px; }
        .progress { display: flex; align-items: center; gap: var(--uui-size-space-2); margin-block-start: var(--uui-size-space-2); min-width: 9rem; }
        .progress uui-progress-bar { flex: 1; }
        .progress-message { white-space: normal; max-width: 14rem; margin-block-start: var(--uui-size-space-1); overflow-wrap: anywhere; }
        .schedule-button { white-space: nowrap; }
        .schedule-button uui-icon { margin-inline-start: var(--uui-size-space-2); }
        .schedule-button + .muted { margin-block-start: var(--uui-size-space-2); }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-font-size-small, 0.875em); }
        .last-run { display: flex; align-items: center; gap: var(--uui-size-space-2); white-space: nowrap; }
        .outcome-succeeded uui-icon { color: var(--uui-color-positive); }
        .outcome-failed uui-icon { color: var(--uui-color-danger); }
        .outcome-skipped uui-icon,
        .outcome-canceled uui-icon { color: var(--uui-color-text-alt); }
        /* Wraps (two lines max) instead of widening the table past the box. */
        .run-message {
            display: -webkit-box;
            -webkit-box-orient: vertical;
            -webkit-line-clamp: 2;
            overflow: hidden;
            overflow-wrap: anywhere;
            margin-block-start: var(--uui-size-space-1);
            color: var(--uui-color-text-alt);
            font-size: var(--uui-font-size-small, 0.875em);
        }
        .outcome-failed + .run-message { color: var(--uui-color-danger); }
        .row-actions { display: flex; gap: var(--uui-size-space-2); justify-content: flex-end; }
        /* Shrink-to-fit columns that must stay on one line. */
        uui-table-cell.nowrap { white-space: nowrap; width: 1%; }
        .error { color: var(--uui-color-danger); }
        .empty { color: var(--uui-color-text-alt); font-style: italic; }
        .visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
    `;
}

const OUTCOME_ICONS = {
    succeeded: 'icon-check',
    failed: 'icon-alert',
    skipped: 'icon-navigation-last',
    canceled: 'icon-block',
    running: 'icon-time',
};

const KIND_HELP = {
    recurring: 'IRecurringBackgroundJob — runs in-process on every server whose role matches.',
    distributed: 'IDistributedBackgroundJob — runs on one server at a time, scheduled through the database.',
};

customElements.define('esatto-scheduled-tasks-dashboard', EsattoScheduledTasksDashboard);
export default EsattoScheduledTasksDashboard;
