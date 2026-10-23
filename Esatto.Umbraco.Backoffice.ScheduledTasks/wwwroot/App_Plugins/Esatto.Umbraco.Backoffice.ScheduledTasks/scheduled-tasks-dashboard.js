import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UmbModalToken, umbConfirmModal, umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { errorMessage, exportSettings, getOverview, resetRun, runNow, setPaused, stopTask } from '@esatto/scheduled-tasks/api.js';
import {
    describeSchedule,
    formatElapsed,
    formatInterval,
    formatRelative,
    isOnDemand,
    matchesFilter,
    shortTypeName,
    summarizeRunPolicy,
    termsOf,
} from '@esatto/scheduled-tasks/format.js';

// Modals are opened by alias (their element modules are only ever loaded through the manifest).
const sidebar = (alias) => new UmbModalToken(`Esatto.Umbraco.Backoffice.ScheduledTasks.${alias}`, {
    modal: { type: 'sidebar', size: 'medium' },
});
const RUNS_MODAL = sidebar('RunsModal');
const SCHEDULE_MODAL = sidebar('ScheduleModal');
const PROPERTIES_MODAL = sidebar('PropertiesModal');
const RUN_POLICY_MODAL = sidebar('RunPolicyModal');
const IMPORT_MODAL = sidebar('ImportModal');

// Relative times re-render every TICK_MS; data reloads every REFRESH_TICKS ticks while visible
// (every tick while a task is running or waiting to retry).
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
        _exporting: { state: true },
    };

    /** Remembered per user in the browser; Umbraco's own tasks are hidden by default. */
    static #showUmbracoKey = 'esatto-scheduled-tasks:show-umbraco';

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
        this._showUmbraco = this.#readShowUmbraco();
        this._autoRefresh = true;
        this._now = Date.now();
        this._exporting = false;

        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notifications = ctx; });
    }

    #readShowUmbraco() {
        try {
            return localStorage.getItem(EsattoScheduledTasksDashboard.#showUmbracoKey) === '1';
        } catch {
            return false;
        }
    }

    #saveShowUmbraco(on) {
        this._showUmbraco = on;
        try {
            localStorage.setItem(EsattoScheduledTasksDashboard.#showUmbracoKey, on ? '1' : '0');
        } catch {
            // A per-user convenience only; a blocked localStorage just loses the memory.
        }
    }

    /** The dashboard's texts in the user's language (lang/*.js). */
    get #t() {
        return termsOf(this.localize);
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
        const busy = this._overview?.tasks.some((t) => t.isRunning || t.retryAtUtc);
        if ((this.#ticks === 0 || busy) && this._autoRefresh && document.visibilityState === 'visible') {
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
            if (!quiet) this._loadError = errorMessage(err, this.#t('loadError'));
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

    /** Asks before a command; false when cancelled. */
    async #confirm(headline, content, confirmLabel) {
        try {
            await umbConfirmModal(this, { headline, content, confirmLabel, color: 'danger' });
            return true;
        } catch {
            return false;
        }
    }

    async #resetRun(task) {
        const t = this.#t;
        if (!(await this.#confirm(t('resetHeadline', task.name), t('resetContent'), t('reset')))) return;

        await this.#withBusy(task, async () => {
            const { data, error } = await resetRun(this, task.id, task.runningSinceUtc ?? null);
            if (error) {
                this.#peek('warning', errorMessage(error, t('couldNotReset')));
                return;
            }
            // The row shows the result: no message on success.
            this.#replaceTask(data?.task);
        });
    }

    async #runNow(task) {
        const t = this.#t;
        await this.#withBusy(task, async () => {
            const { data, error } = await runNow(this, task.id);
            if (error) {
                this.#peek('warning', errorMessage(error, t('couldNotStart')));
                return;
            }
            // The row shows it running (and its progress): no message on success.
            this.#replaceTask(data?.task);
            // The run starts on the job's own thread; refresh shortly to show it running/finished.
            setTimeout(() => this.#load({ quiet: true }), 1500);
        });
    }

    async #stop(task) {
        const t = this.#t;
        const retryOnly = task.retryAtUtc && !task.isRunning;
        if (!retryOnly && !(await this.#confirm(t('stopHeadline', task.name), t('stopContent'), t('stop')))) return;

        await this.#withBusy(task, async () => {
            const { data, error } = await stopTask(this, task.id);
            if (error) {
                this.#peek('warning', errorMessage(error, t('couldNotStop')));
                return;
            }
            // The row shows "Stopping…" (or the retry gone): no message on success.
            this.#replaceTask(data?.task);
            setTimeout(() => this.#load({ quiet: true }), 1500);
        });
    }

    async #togglePaused(task) {
        const t = this.#t;
        const pause = !task.isPaused;
        if (pause && task.isUmbraco && !(await this.#confirm(t('pauseHeadline', task.name), t('pauseUmbracoContent'), t('pause')))) return;

        await this.#withBusy(task, async () => {
            const { data, error } = await setPaused(this, task.id, pause);
            if (error) {
                this.#peek('danger', errorMessage(error, t(pause ? 'pauseFailed' : 'resumeFailed')));
                return;
            }
            this.#replaceTask(data?.task);
        });
    }

    async #export() {
        const t = this.#t;
        this._exporting = true;
        try {
            const { data, error } = await exportSettings(this);
            if (error) {
                this.#peek('danger', errorMessage(error, t('exportFailed')));
                return;
            }
            const day = new Date().toISOString().slice(0, 10);
            const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
            const link = Object.assign(document.createElement('a'), {
                href: URL.createObjectURL(blob),
                download: `scheduled-tasks-${(this._overview?.serverName ?? 'site').toLowerCase()}-${day}.json`,
            });
            link.click();
            setTimeout(() => URL.revokeObjectURL(link.href), 10000);
            this.#peek('positive', t('exported'));
        } finally {
            this._exporting = false;
        }
    }

    async #import() {
        try {
            const result = await umbOpenModal(this, IMPORT_MODAL, { data: {} });
            if (result?.message) this.#peek('positive', result.message);
        } catch {
            return; // closed without importing
        }
        this.#commandCompletedAt = performance.now();
        await this.#load();
    }

    #openRuns(task) {
        umbOpenModal(this, RUNS_MODAL, { data: { task, historyPersisted: this._overview?.historyPersisted, runHistoryLimit: this._overview?.runHistoryLimit } })
            .catch(() => {});
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
        if (result?.message) this.#peek('positive', this.#t('toast', task.name, result.message));
    }

    #peek(color, message) {
        this.#notifications?.peek(color, { data: { message } });
    }

    #visibleTasks() {
        const tasks = this._overview?.tasks ?? [];
        return tasks.filter((t) => (this._showUmbraco || !t.isUmbraco) && matchesFilter(t, this._filter));
    }

    #date(iso) {
        return this.localize.date(iso, { dateStyle: 'short', timeStyle: 'medium' });
    }

    #relative(iso) {
        return formatRelative(iso, this._now, this.localize.lang());
    }

    // ---- rendering ----

    #renderStatus(task) {
        const t = this.#t;
        const since = task.runningSinceUtc ? this.#date(task.runningSinceUtc) : null;
        if (task.isPossiblyStuck) {
            return html`<uui-tag color="warning" title=${t('stuckHelp', since)}>${t('statusStuck')}</uui-tag>`;
        }
        if (task.retryAtUtc) {
            const when = this.#relative(task.retryAtUtc);
            return html`<uui-tag color="warning" title=${t('retryHelp', task.nextAttempt, task.maxAttempts, when)}>
                    <uui-icon name="icon-refresh"></uui-icon> ${t('statusRetry')}
                </uui-tag>
                <div class="muted">${t('retryHelp', task.nextAttempt, task.maxAttempts, when)}</div>`;
        }
        if (task.isRunning) {
            return html`
                <uui-tag color="positive" title=${since ? t('runningSince', since) : ''}><uui-loader-circle></uui-loader-circle> ${t('statusRunning')}</uui-tag>
                ${task.stopRequestedBy
                    ? html`<uui-tag color="warning" class="stopping" title=${t('stoppingHelp', task.stopRequestedBy)}>${t('statusStopping')}</uui-tag>`
                    : nothing}
                ${this.#renderProgress(task)}`;
        }
        if (task.isPaused) {
            return html`<uui-tag color="warning" title=${this.#changedBy(task)}>${t('statusPaused')}</uui-tag>`;
        }
        if (!task.runsOnThisServer) {
            return html`<uui-tag look="outline" title=${t('otherServersHelp', task.serverRoles.join(' / '), this._overview.serverRole)}>${t('statusOtherServers')}</uui-tag>`;
        }
        if (task.lastRun?.outcome === 'failed') {
            return html`<uui-tag color="danger">${t('statusFailing')}</uui-tag>`;
        }
        if (isOnDemand(task)) {
            return html`<uui-tag look="secondary">${t('statusOnDemand')}</uui-tag>`;
        }
        return html`<uui-tag look="secondary">${t('statusScheduled')}</uui-tag>`;
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
        const t = this.#t;
        const busy = this._busyIds.has(task.id);
        const text = task.schedule ? describeSchedule(task.schedule, t) : formatInterval(task.periodSeconds, t);
        const title = !task.isManaged
            ? t('notHosted')
            : task.schedule?.timeZone ? t('changeScheduleIn', task.schedule.timeZone) : t('changeSchedule');

        // The schedule itself is the edit button, so it reads as something you can change. Its
        // accessible name starts with the visible text (voice control) and names the task.
        return html`
            <button
                type="button"
                class="schedule-button"
                aria-label=${t('changeScheduleLabel', text, task.name)}
                title=${title}
                ?disabled=${busy || !task.isManaged}
                @click=${() => this.#openEditor(SCHEDULE_MODAL, task)}>
                <span>${text}</span>
                <uui-icon name="icon-edit"></uui-icon>
            </button>
            ${task.hasPeriodOverride || task.schedule
                ? html`<div class="muted" title=${this.#changedBy(task)}>${t('defaultSchedule', formatInterval(task.defaultPeriodSeconds, t).toLowerCase())}</div>`
                : nothing}
        `;
    }

    #renderLastRun(task) {
        const t = this.#t;
        const run = task.lastRun;
        if (run) {
            const when = run.outcome === 'skipped' ? run.lastOccurrenceUtc : run.startedUtc;
            const elsewhere = run.server && this._overview && run.server !== this._overview.serverName;
            return html`
                <div class="last-run outcome-${run.outcome}" title=${this.#date(when)}>
                    <uui-icon name=${OUTCOME_ICONS[run.outcome] ?? 'icon-info'}></uui-icon>
                    <span>${this.#relative(when)}</span>
                    ${run.durationMs != null && run.outcome !== 'skipped'
                        ? html`<span class="muted">· ${formatElapsed(run.durationMs)}</span>`
                        : nothing}
                </div>
                ${elsewhere ? html`<div class="muted">${t('onServer', run.server)}</div>` : nothing}
                ${run.message ? html`<div class="run-message" title=${run.message}>${run.message}</div>` : nothing}
            `;
        }
        if (task.lastRunUtc) {
            return html`<span class="muted" title=${t('lastFinishHelp')}>
                ${this.#relative(task.lastRunUtc)} · ${t('anyServer')}
            </span>`;
        }
        return html`<span class="muted">${this._overview?.historyPersisted ? t('noRunsYet') : t('notSinceStartup')}</span>`;
    }

    #renderNextRun(task) {
        const t = this.#t;
        if (task.isPaused) return html`<span class="muted">${t('statusPaused')}</span>`;
        if (task.retryAtUtc) return html`<span title=${this.#date(task.retryAtUtc)}>${this.#relative(task.retryAtUtc)}</span>`;
        if (!task.runsOnThisServer || task.isRunning) return html`<span class="muted">—</span>`;
        // A calendar task's next run comes from its schedule, even when its own period is infinite.
        if (isOnDemand(task)) return html`<span class="muted">${t('statusOnDemand')}</span>`;
        if (!task.nextRunUtc) return html`<span class="muted">—</span>`;

        const due = Date.parse(task.nextRunUtc) <= this._now;
        return html`<span title=${this.#date(task.nextRunUtc)}>${due ? t('dueNow') : this.#relative(task.nextRunUtc)}</span>`;
    }

    #renderActions(task) {
        const t = this.#t;
        const busy = this._busyIds.has(task.id);
        const canRun = task.isManaged && task.runsOnThisServer && !task.isRunning && !task.retryAtUtc;
        const stoppable = task.isManaged && (task.isRunning || task.retryAtUtc);
        const retryOnly = task.retryAtUtc && !task.isRunning;

        let primary;
        if (task.isPossiblyStuck) {
            // A run that looks stuck can't be started again until it's reset.
            primary = html`
                <uui-button
                    look="secondary"
                    color="warning"
                    compact
                    label=${t('actionLabel', t('reset'), task.name)}
                    title=${t('resetTitle')}
                    ?disabled=${busy}
                    @click=${() => this.#resetRun(task)}>
                    <uui-icon name="icon-refresh"></uui-icon> <span class="action-text">${t('reset')}</span>
                </uui-button>`;
        } else if (stoppable) {
            const label = retryOnly ? t('cancelRetry') : t('stop');
            primary = html`
                <uui-button
                    look="secondary"
                    color="danger"
                    compact
                    label=${t('actionLabel', label, task.name)}
                    title=${task.stopRequestedBy ? t('stoppingHelp', task.stopRequestedBy) : retryOnly ? t('cancelRetryTitle') : t('stopTitle')}
                    ?disabled=${busy || Boolean(task.stopRequestedBy)}
                    @click=${() => this.#stop(task)}>
                    <uui-icon name="icon-stop"></uui-icon> <span class="action-text">${label}</span>
                </uui-button>`;
        } else {
            primary = html`
                <uui-button
                    look="secondary"
                    compact
                    label=${t('actionLabel', t('runNow'), task.name)}
                    title=${canRun ? t('runNowTitle', task.name) : task.isRunning ? t('alreadyRunning') : t('cantStartHere')}
                    ?disabled=${busy || !canRun}
                    @click=${() => this.#runNow(task)}>
                    <uui-icon name="icon-play"></uui-icon> <span class="action-text">${t('runNow')}</span>
                </uui-button>`;
        }

        const pauseText = task.isPaused ? t('resume') : t('pause');
        return html`
            <div class="row-actions">
                ${primary}
                <uui-button
                    look="secondary"
                    compact
                    label=${t('actionLabel', pauseText, task.name)}
                    ?disabled=${busy || !task.isManaged}
                    @click=${() => this.#togglePaused(task)}>
                    <uui-icon name=${task.isPaused ? 'icon-play' : 'icon-pause'}></uui-icon> <span class="action-text">${pauseText}</span>
                </uui-button>
                <uui-button look="secondary" compact label=${t('actionLabel', t('history'), task.name)} @click=${() => this.#openRuns(task)}>
                    <uui-icon name="icon-history"></uui-icon> <span class="action-text">${t('history')}</span>
                </uui-button>
            </div>
        `;
    }

    #renderTaskExtras(task) {
        const t = this.#t;
        const policy = task.runPolicy;
        return html`
            <div class="task-extras">
                ${task.propertyCount > 0
                    ? html`
                        <span class="extra">
                            <uui-button
                                look="outline"
                                compact
                                label=${t('propertiesOf', task.name)}
                                title=${t('propertiesHelp')}
                                @click=${() => this.#openEditor(PROPERTIES_MODAL, task)}>
                                <uui-icon name="icon-settings"></uui-icon> ${t('properties')}
                            </uui-button>
                            <span class="muted">${task.hasCustomProperties ? t('customised') : t('defaults')}</span>
                        </span>`
                    : nothing}
                ${task.isManaged && policy
                    ? html`
                        <span class="extra">
                            <uui-button
                                look="outline"
                                compact
                                label=${t('runPolicyOf', task.name)}
                                title=${t('runPolicyHelp')}
                                @click=${() => this.#openEditor(RUN_POLICY_MODAL, task)}>
                                <uui-icon name="icon-refresh"></uui-icon> ${t('runPolicy')}
                            </uui-button>
                            ${policy.retries > 0 || policy.timeoutSeconds
                                ? html`<span class="muted ${policy.isCustomised ? 'customised' : ''}">${summarizeRunPolicy(policy, t)}</span>`
                                : nothing}
                        </span>`
                    : nothing}
            </div>
        `;
    }

    #renderRow(task) {
        const t = this.#t;
        return html`
            <uui-table-row class=${task.isPaused ? 'paused' : ''}>
                <uui-table-cell>
                    <div class="task-name">${task.name}</div>
                    <div class="task-meta">
                        <uui-tag look="outline" class="kind" title=${t(task.kind === 'distributed' ? 'kindDistributedHelp' : 'kindRecurringHelp')}>
                            ${t(task.kind === 'distributed' ? 'kindDistributed' : 'kindRecurring')}
                        </uui-tag>
                        ${task.isUmbraco ? html`<uui-tag look="outline" class="kind">${t('kindUmbraco')}</uui-tag>` : nothing}
                        <code title=${task.typeName}>${shortTypeName(task.typeName)}</code>
                    </div>
                    ${task.isManaged ? nothing : html`<div class="muted">${t('notManaged')}</div>`}
                    ${this.#renderTaskExtras(task)}
                </uui-table-cell>
                <uui-table-cell class="schedule">${this.#renderSchedule(task)}</uui-table-cell>
                <uui-table-cell class="nowrap">${this.#renderStatus(task)}</uui-table-cell>
                <uui-table-cell>${this.#renderLastRun(task)}</uui-table-cell>
                <uui-table-cell class="nowrap">${this.#renderNextRun(task)}</uui-table-cell>
                <uui-table-cell class="actions">${this.#renderActions(task)}</uui-table-cell>
            </uui-table-row>
        `;
    }

    #changedBy(task) {
        if (!task.settingsUpdatedUtc) return '';
        const when = this.#date(task.settingsUpdatedUtc);
        return task.settingsUpdatedBy ? this.#t('changedBy', task.settingsUpdatedBy, when) : this.#t('changedAt', when);
    }

    render() {
        const t = this.#t;
        if (!this._loaded) {
            return html`<uui-box><strong slot="headline">${t('headline')}</strong><uui-loader></uui-loader></uui-box>`;
        }

        const overview = this._overview;
        const tasks = this.#visibleTasks();
        const total = overview?.tasks.length ?? 0;

        return html`
            <uui-box>
                <!-- Slotted, not the attribute: the box's own h3 isn't bold. -->
                <strong slot="headline">${t('headline')}</strong>
                <div slot="header-actions" class="header-actions">
                    <uui-toggle
                        label=${t('autoRefresh')}
                        .checked=${this._autoRefresh}
                        @change=${(e) => { this._autoRefresh = e.target.checked; }}></uui-toggle>
                    <uui-button look="secondary" compact label=${t('export')} title=${t('exportTitle')} ?disabled=${this._exporting} @click=${() => this.#export()}>
                        <uui-icon name="icon-download-alt"></uui-icon> ${t('export')}
                    </uui-button>
                    <uui-button look="secondary" compact label=${t('import')} title=${t('importTitle')} @click=${() => this.#import()}>
                        <uui-icon name="icon-page-up"></uui-icon> ${t('import')}
                    </uui-button>
                    <uui-button look="secondary" compact label=${t('refresh')} @click=${() => this.#load()}>
                        <uui-icon name="icon-refresh"></uui-icon> ${t('refresh')}
                    </uui-button>
                </div>

                <p class="intro">${t('intro')} ${t('introSaved', Boolean(overview?.historyPersisted))}</p>

                ${overview ? html`<p class="server">${t('thisServer', overview.serverName, overview.serverRole)}</p>` : nothing}

                ${this._loadError ? html`<p role="alert" class="error">${this._loadError}</p>` : nothing}

                <div class="toolbar">
                    <input
                        class="text-input search"
                        type="search"
                        aria-label=${t('filterLabel')}
                        placeholder=${t('filterPlaceholder')}
                        .value=${this._filter}
                        @input=${(e) => { this._filter = e.target.value; }}>
                    <uui-toggle
                        label=${t('showUmbraco')}
                        .checked=${this._showUmbraco}
                        @change=${(e) => this.#saveShowUmbraco(e.target.checked)}></uui-toggle>
                </div>

                ${tasks.length === 0
                    ? html`<p class="empty">
                          ${total === 0
                              ? t('noTasks')
                              : this._filter
                                  ? t('noMatches')
                                  : html`${t('allUmbraco', total)}
                                        <uui-button
                                            look="secondary"
                                            label=${t('showUmbraco')}
                                            @click=${() => this.#saveShowUmbraco(true)}>${t('showUmbraco')}</uui-button>`}
                      </p>`
                    : html`
                        <uui-table aria-label=${t('headline')}>
                            <uui-table-head>
                                <uui-table-head-cell>${t('colTask')}</uui-table-head-cell>
                                <uui-table-head-cell>${t('colSchedule')}</uui-table-head-cell>
                                <uui-table-head-cell>${t('colStatus')}</uui-table-head-cell>
                                <uui-table-head-cell>${t('colLastRun')}</uui-table-head-cell>
                                <uui-table-head-cell>${t('colNextRun')}</uui-table-head-cell>
                                <uui-table-head-cell><span class="visually-hidden">${t('colActions')}</span></uui-table-head-cell>
                            </uui-table-head>
                            ${tasks.map((task) => this.#renderRow(task))}
                        </uui-table>
                    `}
            </uui-box>
        `;
    }

    static styles = css`
        :host { display: block; padding: var(--uui-size-space-5); }
        .header-actions { display: flex; align-items: center; gap: var(--uui-size-space-3); }
        .header-actions uui-toggle { margin-inline-end: var(--uui-size-space-3); }
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
        .task-extras { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2) var(--uui-size-space-5); margin-block-start: var(--uui-size-space-3); }
        .task-extras:empty { display: none; }
        .extra { display: flex; flex-wrap: wrap; align-items: center; gap: var(--uui-size-space-1) var(--uui-size-space-2); }
        .extra .muted { white-space: nowrap; }
        .extra uui-button { white-space: nowrap; }
        .extra .customised { color: var(--uui-color-text); }
        uui-tag.kind { font-size: 11px; }
        /* Icons default to sitting on the text baseline, which rides high next to a label. */
        /* Sit icons on the label's baseline (text-top floated them above the text). */
        uui-button uui-icon { vertical-align: -0.125em; }
        /* The icon+label pair renders slightly high in the pill; nudge both down together.
           The icon skips the global -0.125em baseline pull or it lands too low. */
        .row-actions uui-button uui-icon,
        .row-actions uui-button .action-text { display: inline-block; transform: translateY(1.5px); }
        .row-actions uui-button uui-icon { vertical-align: 0; }
        /* The loader defaults to --uui-color-default (dark navy); in the tag it must follow the tag's text (white). */
        uui-tag uui-loader-circle { font-size: 0.9em; margin-inline-end: 4px; color: inherit; }
        uui-tag.stopping { margin-inline-start: var(--uui-size-space-1); }
        .progress { display: flex; align-items: center; gap: var(--uui-size-space-2); margin-block-start: var(--uui-size-space-2); min-width: 9rem; }
        .progress uui-progress-bar { flex: 1; }
        .progress-message { white-space: normal; max-width: 14rem; margin-block-start: var(--uui-size-space-1); overflow-wrap: anywhere; }
        /* A native button, so a long schedule ("Every Monday and Thursday at 07:00") wraps tidily. */
        uui-table-cell.schedule { min-width: 10rem; }
        .schedule-button {
            display: inline-flex;
            align-items: center;
            gap: var(--uui-size-space-2);
            max-width: 100%;
            padding: var(--uui-size-space-2) var(--uui-size-space-3);
            border: 1px solid var(--uui-color-border-standalone, #c2c2c2);
            border-radius: var(--uui-border-radius, 3px);
            background: none;
            font: inherit;
            line-height: 1.35;
            text-align: start;
            color: var(--uui-color-interactive, inherit);
            cursor: pointer;
        }
        .schedule-button:hover:not(:disabled) { border-color: var(--uui-color-interactive-emphasis, #3544b1); color: var(--uui-color-interactive-emphasis, #3544b1); }
        .schedule-button:focus-visible { outline: 2px solid var(--uui-color-focus, #3879ff); outline-offset: 2px; }
        .schedule-button:disabled { cursor: default; color: var(--uui-color-disabled-contrast, #9d9d9d); }
        .schedule-button uui-icon { flex: none; }
        .schedule-button + .muted { margin-block-start: var(--uui-size-space-2); }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-font-size-small, 0.875em); }
        uui-table-cell.nowrap .muted { white-space: normal; max-width: 14rem; margin-block-start: var(--uui-size-space-1); }
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
        .outcome-failed ~ .run-message { color: var(--uui-color-danger); }
        /* Wraps onto a second line rather than pushing the table past the box. */
        .row-actions { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); justify-content: flex-end; min-width: 13rem; }
        uui-table-cell.actions { width: 1%; }
        .row-actions uui-button { white-space: nowrap; }
        /* Shrink-to-fit columns that must stay on one line. */
        uui-table-cell.nowrap { white-space: nowrap; width: 1%; }
        .error { color: var(--uui-color-danger); }
        .empty { color: var(--uui-color-text-alt); font-style: italic; }
        /* The inline "Show Umbraco's tasks" action when the toggle hides every task. */
        .empty uui-button { margin-inline-start: var(--uui-size-space-2); font-style: normal; }
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

customElements.define('esatto-scheduled-tasks-dashboard', EsattoScheduledTasksDashboard);
export default EsattoScheduledTasksDashboard;
