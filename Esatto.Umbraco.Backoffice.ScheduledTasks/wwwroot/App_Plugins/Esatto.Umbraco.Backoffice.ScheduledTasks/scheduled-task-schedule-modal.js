import { css, html, live, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { errorMessage, previewSchedule, setPeriod, setSchedule } from '@esatto/scheduled-tasks/api.js';
import {
    INTERVAL_UNITS,
    MONTHS,
    WEEKDAYS,
    formatInterval,
    formatLength,
    formatRelative,
    initialIntervalSeconds,
    minimumIntervalError,
    splitInterval,
    timeZoneOptions,
    toSeconds,
} from '@esatto/scheduled-tasks/format.js';

const MODES = [
    { mode: 'interval', label: 'Interval' },
    { mode: 'daily', label: 'Daily' },
    { mode: 'weekly', label: 'Weekly' },
    { mode: 'monthly', label: 'Monthly' },
    { mode: 'yearly', label: 'Yearly' },
    { mode: 'cron', label: 'Cron' },
];
const UNITS_ASCENDING = [...INTERVAL_UNITS].reverse();
const DAY_SECONDS = 86400;
const BROWSER_TIME_ZONE = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
// The browser's zones plus 'UTC', which Chromium's list leaves out.
const TIME_ZONES = timeZoneOptions(null, (() => {
    try {
        return Intl.supportedValuesOf('timeZone');
    } catch {
        return [BROWSER_TIME_ZONE];
    }
})());

/**
 * Sidebar for when a task runs: an interval ("every 30 minutes") or a calendar schedule ("every
 * Monday at 07:00", "on the 1st of every month", "every year on 15 March", or cron), with a
 * preview of the next runs. Opened by the dashboard with `data: { task, onSavedAfterClose }`;
 * submits `{ task, message }` after saving. When the sidebar was closed (Esc, backdrop) while a
 * save was in flight, the result goes to `onSavedAfterClose` instead.
 */
class EsattoScheduledTaskScheduleModal extends UmbModalBaseElement {
    static properties = {
        _mode: { state: true },
        _interval: { state: true },
        _time: { state: true },
        _weekdays: { state: true },
        _dayOfMonth: { state: true },
        _month: { state: true },
        _cron: { state: true },
        _timeZone: { state: true },
        _preview: { state: true },
        _error: { state: true },
        _saving: { state: true },
    };

    #seeded = false;
    #previewTimer;
    #previewSeq = 0;

    constructor() {
        super();
        this._mode = 'interval';
        this._interval = { value: '1', unit: 'hours' };
        this._time = '07:00';
        this._weekdays = ['Monday'];
        this._dayOfMonth = 1;
        this._month = 1;
        this._cron = '0 7 * * 1-5';
        this._timeZone = BROWSER_TIME_ZONE;
        this._preview = null;
        this._error = '';
        this._saving = false;
    }

    connectedCallback() {
        super.connectedCallback();
        // Seed once; re-attaching must not throw away unsaved edits.
        if (this.#seeded) return;
        this.#seeded = true;

        const task = this.data.task;
        // On a calendar the reported period is derived from it; start from the task's own interval.
        const { value, unit } = splitInterval(initialIntervalSeconds(task));
        this._interval = { value: String(value), unit };

        const schedule = task.schedule;
        if (schedule) {
            this._mode = schedule.kind;
            this._time = schedule.time ?? this._time;
            this._weekdays = schedule.weekdays?.length ? [...schedule.weekdays] : this._weekdays;
            this._dayOfMonth = schedule.dayOfMonth ?? this._dayOfMonth;
            this._month = schedule.month ?? this._month;
            this._cron = schedule.cron ?? this._cron;
            this._timeZone = schedule.timeZone ?? this._timeZone;
            this.#schedulePreview();
        }
    }

    disconnectedCallback() {
        clearTimeout(this.#previewTimer);
        super.disconnectedCallback();
    }

    /** The calendar schedule being edited, in the API's shape (null in interval mode). */
    #schedule() {
        if (this._mode === 'interval') return null;
        return {
            kind: this._mode,
            time: this._mode === 'cron' ? null : this._time,
            weekdays: this._mode === 'weekly' ? WEEKDAYS.filter((d) => this._weekdays.includes(d)) : null,
            dayOfMonth: this._mode === 'monthly' || this._mode === 'yearly' ? Number(this._dayOfMonth) : null,
            month: this._mode === 'yearly' ? Number(this._month) : null,
            cron: this._mode === 'cron' ? this._cron : null,
            timeZone: this._timeZone,
        };
    }

    /** Change a field and refresh the preview of the next runs. */
    #set(changes) {
        Object.assign(this, changes);
        this._error = '';
        this.#schedulePreview();
    }

    #schedulePreview() {
        clearTimeout(this.#previewTimer);
        const schedule = this.#schedule();
        if (!schedule) {
            this._preview = null;
            return;
        }

        this.#previewTimer = setTimeout(async () => {
            const seq = ++this.#previewSeq;
            const { data, error } = await previewSchedule(this, schedule);
            if (seq !== this.#previewSeq) return; // a newer edit is on its way
            this._preview = error
                ? { nextRunsUtc: [], error: errorMessage(error, 'Could not preview the schedule.') }
                : data;
        }, 250);
    }

    #toggleWeekday(day) {
        const days = this._weekdays.includes(day) ? this._weekdays.filter((d) => d !== day) : [...this._weekdays, day];
        this.#set({ _weekdays: days });
    }

    /** Why this task can't have a calendar schedule (e.g. it aligns to the clock), or ''. */
    #calendarUnavailableReason() {
        return this.data?.task?.calendarUnavailableReason || '';
    }

    async #save() {
        const task = this.data.task;
        let result;

        if (this._mode === 'interval') {
            const seconds = toSeconds(this._interval.value, this._interval.unit);
            if (Number.isNaN(seconds)) {
                this._error = 'Enter a positive number.';
                return;
            }
            const tooShort = minimumIntervalError(seconds, task.minimumPeriodSeconds);
            if (tooShort) {
                this._error = tooShort;
                return;
            }
            this._saving = true;
            result = await setPeriod(this, task.id, seconds);
        } else {
            const unavailable = this.#calendarUnavailableReason();
            if (unavailable) {
                this._error = unavailable;
                return;
            }
            this._saving = true;
            result = await setSchedule(this, task.id, this.#schedule());
        }

        this._saving = false;
        if (result.error) {
            this._error = errorMessage(result.error, 'Could not save the schedule.');
            return;
        }

        this.#finish({ task: result.data?.task, message: result.data?.message });
    }

    /** Back to the task's own interval: clears an interval override and a calendar alike. */
    async #useOwnSchedule() {
        this._saving = true;
        const { data, error } = await setPeriod(this, this.data.task.id, null);
        this._saving = false;
        if (error) {
            this._error = errorMessage(error, 'Could not reset the schedule.');
            return;
        }

        this.#finish({ task: data?.task, message: data?.message });
    }

    /**
     * Submits a saved result. Esc and the backdrop still close the sidebar while a save is in
     * flight; the modal context is then resolved (and soon destroyed, so setting the value would
     * throw), so the result is handed to the opener's callback instead of being dropped.
     */
    #finish(result) {
        if (!this.isConnected || !this.modalContext || this.modalContext.isResolved?.()) {
            this.data?.onSavedAfterClose?.(result);
            return;
        }
        this.value = result;
        this._submitModal();
    }

    #formatRun(utc) {
        const date = new Date(utc);
        let text;
        try {
            text = new Intl.DateTimeFormat(undefined, {
                timeZone: this._timeZone,
                weekday: 'short',
                day: 'numeric',
                month: 'short',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
            }).format(date);
        } catch {
            text = date.toLocaleString();
        }
        return html`<li>${text} <span class="muted">(${formatRelative(utc)})</span></li>`;
    }

    /**
     * A toggle chip. A native button, because uui-button only forwards aria-label/labelledby to its
     * inner <button>: aria-pressed on its host never reaches assistive tech.
     */
    #renderToggle({ label, name, pressed, disabled = false, describedBy, onClick }) {
        return html`
            <button
                type="button"
                class="toggle"
                aria-pressed=${pressed ? 'true' : 'false'}
                aria-label=${name ?? nothing}
                aria-describedby=${describedBy ?? nothing}
                ?disabled=${disabled || this._saving}
                @click=${onClick}>${label}</button>`;
    }

    #renderInterval() {
        const task = this.data.task;
        const seconds = toSeconds(this._interval.value, this._interval.unit);
        return html`
            <div class="field">
                <label for="interval-value">Run every</label>
                <div class="row">
                    <input
                        id="interval-value"
                        class="text-input number-input"
                        type="number"
                        step="any"
                        .value=${live(this._interval.value)}
                        @input=${(e) => { this._interval = { ...this._interval, value: e.target.value }; this._error = ''; }}>
                    <select
                        class="text-input"
                        aria-label="Unit"
                        @change=${(e) => { this._interval = { ...this._interval, unit: e.target.value }; this._error = ''; }}>
                        ${UNITS_ASCENDING.map((u) => html`<option value=${u.unit} ?selected=${u.unit === this._interval.unit}>${u.label}</option>`)}
                    </select>
                </div>
                ${task.defaultPeriodSeconds != null
                    ? html`<p class="muted">The task’s own interval: ${formatInterval(task.defaultPeriodSeconds).toLowerCase()}.</p>`
                    : nothing}
                ${task.minimumPeriodSeconds > 0
                    ? html`<p class="muted">Shortest allowed: ${formatLength(task.minimumPeriodSeconds)}.</p>`
                    : nothing}
                ${task.kind === 'recurring' && seconds >= DAY_SECONDS
                    ? html`<p class="muted">On an interval, recurring tasks also run once shortly after every restart of the site; a calendar schedule doesn’t.</p>`
                    : nothing}
            </div>
        `;
    }

    #renderTime() {
        return html`
            <div class="field">
                <label for="time">At</label>
                <input
                    id="time"
                    class="text-input"
                    type="time"
                    required
                    .value=${this._time}
                    @change=${(e) => this.#set({ _time: e.target.value })}>
            </div>
        `;
    }

    #renderDayOfMonth() {
        return html`
            <select
                id="day"
                class="text-input"
                aria-label="Day of the month"
                @change=${(e) => this.#set({ _dayOfMonth: Number(e.target.value) })}>
                ${Array.from({ length: 31 }, (_, i) => i + 1).map((d) => html`<option value=${d} ?selected=${d === Number(this._dayOfMonth)}>${d}</option>`)}
            </select>
        `;
    }

    #renderCalendar() {
        switch (this._mode) {
            case 'daily':
                return this.#renderTime();
            case 'weekly':
                return html`
                    <div class="field">
                        <span class="label" id="weekdays-label">On</span>
                        <div class="weekdays" role="group" aria-labelledby="weekdays-label">
                            ${WEEKDAYS.map((day) => this.#renderToggle({
                                label: day.slice(0, 3),
                                name: day,
                                pressed: this._weekdays.includes(day),
                                onClick: () => this.#toggleWeekday(day),
                            }))}
                        </div>
                    </div>
                    ${this.#renderTime()}
                `;
            case 'monthly':
                return html`
                    <div class="field">
                        <label for="day">On day</label>
                        ${this.#renderDayOfMonth()}
                        ${Number(this._dayOfMonth) > 28 ? html`<p class="muted">Months without this day are skipped.</p>` : nothing}
                    </div>
                    ${this.#renderTime()}
                `;
            case 'yearly':
                return html`
                    <div class="field">
                        <label for="day">On</label>
                        <div class="row">
                            ${this.#renderDayOfMonth()}
                            <select
                                class="text-input"
                                aria-label="Month"
                                @change=${(e) => this.#set({ _month: Number(e.target.value) })}>
                                ${MONTHS.map((m, i) => html`<option value=${i + 1} ?selected=${i + 1 === Number(this._month)}>${m}</option>`)}
                            </select>
                        </div>
                    </div>
                    ${this.#renderTime()}
                `;
            case 'cron':
                return html`
                    <div class="field">
                        <label for="cron">Cron expression</label>
                        <input
                            id="cron"
                            class="text-input"
                            type="text"
                            spellcheck="false"
                            placeholder="0 7 * * 1-5"
                            .value=${this._cron}
                            @input=${(e) => this.#set({ _cron: e.target.value })}>
                        <p class="muted">Five fields: minute, hour, day of month, month, day of week. <code>0 7 * * 1-5</code> is 07:00 on weekdays.</p>
                    </div>
                `;
            default:
                return nothing;
        }
    }

    #renderTimeZoneAndPreview() {
        const preview = this._preview;
        return html`
            <div class="field">
                <label for="time-zone">Time zone</label>
                <select id="time-zone" class="text-input" @change=${(e) => this.#set({ _timeZone: e.target.value })}>
                    ${timeZoneOptions(this._timeZone, TIME_ZONES).map(
                        (zone) => html`<option value=${zone} ?selected=${zone === this._timeZone}>${zone}</option>`,
                    )}
                </select>
            </div>
            <div class="preview" aria-live="polite">
                <strong>Next runs</strong>
                ${!preview
                    ? html`<p class="muted">…</p>`
                    : preview.error
                        ? html`<p role="alert" class="error">${preview.error}</p>`
                        : html`<ul>${preview.nextRunsUtc.map((utc) => this.#formatRun(utc))}</ul>`}
            </div>
        `;
    }

    render() {
        const task = this.data?.task;
        const customised = task?.hasPeriodOverride || task?.schedule;
        const unavailable = this.#calendarUnavailableReason();

        return html`
            <umb-body-layout headline=${`Schedule: ${task?.name ?? ''}`}>
                <uui-box>
                    <div class="modes" role="group" aria-label="How the task is scheduled">
                        ${MODES.map(({ mode, label }) => this.#renderToggle({
                            label,
                            pressed: this._mode === mode,
                            // A task that can't have a calendar keeps Interval (and the mode it's in).
                            disabled: Boolean(unavailable) && mode !== 'interval' && mode !== this._mode,
                            describedBy: unavailable && mode !== 'interval' ? 'calendar-unavailable' : undefined,
                            onClick: () => this.#set({ _mode: mode }),
                        }))}
                    </div>
                    ${unavailable
                        ? html`<p id="calendar-unavailable" class="muted unavailable">${unavailable}</p>`
                        : nothing}

                    ${this._mode === 'interval' ? this.#renderInterval() : html`${this.#renderCalendar()}${this.#renderTimeZoneAndPreview()}`}

                    ${this._error ? html`<p role="alert" class="error">${this._error}</p>` : nothing}
                </uui-box>

                <div slot="actions">
                    ${customised
                        ? html`<uui-button
                            look="secondary"
                            label="Use the task’s own schedule"
                            ?disabled=${this._saving}
                            @click=${() => this.#useOwnSchedule()}></uui-button>`
                        : nothing}
                    <uui-button label="Cancel" ?disabled=${this._saving} @click=${() => this._rejectModal()}></uui-button>
                    <uui-button
                        look="primary"
                        color="positive"
                        label="Save"
                        .state=${this._saving ? 'waiting' : undefined}
                        ?disabled=${this._saving}
                        @click=${() => this.#save()}></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    static styles = css`
        .modes { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); margin-block-end: var(--uui-size-space-5); }
        .modes + .unavailable { margin-block: calc(var(--uui-size-space-3) * -1) var(--uui-size-space-5); }
        /* Looks like a compact uui-button: secondary when off, primary when pressed. */
        .toggle {
            font: inherit;
            font-weight: var(--uui-font-weight-bold, 700);
            min-height: var(--uui-size-10, 30px);
            padding: 0 var(--uui-size-space-4, 12px);
            border: 1px solid transparent;
            border-radius: var(--uui-border-radius, 3px);
            background: var(--uui-color-surface-alt, #f3f3f5);
            color: var(--uui-color-text, #060606);
            cursor: pointer;
        }
        .toggle:hover:not(:disabled) { background: var(--uui-color-surface-emphasis, #e4e4e4); }
        .toggle[aria-pressed='true'] { background: var(--uui-color-default, #1b264f); color: var(--uui-color-default-contrast, #fff); }
        .toggle[aria-pressed='true']:hover:not(:disabled) { background: var(--uui-color-default-emphasis, #2152a3); }
        .toggle:focus-visible { outline: 2px solid var(--uui-color-focus, #3879ff); outline-offset: 2px; }
        .toggle:disabled { cursor: default; opacity: 0.55; }
        .field { display: flex; flex-direction: column; gap: var(--uui-size-space-2); margin-block-end: var(--uui-size-space-5); }
        .field label, .field .label { font-weight: var(--uui-font-weight-bold, 700); }
        .row { display: flex; gap: var(--uui-size-space-3); align-items: center; }
        .weekdays { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }
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
        .number-input { width: 7rem; }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-font-size-small, 0.875em); margin: 0; }
        .preview ul { margin: var(--uui-size-space-2) 0 0; padding-inline-start: var(--uui-size-space-5); }
        .preview li { margin-block: var(--uui-size-space-1); }
        .error { color: var(--uui-color-danger); }
        code { font-size: 0.95em; }
    `;
}

customElements.define('esatto-scheduled-task-schedule-modal', EsattoScheduledTaskScheduleModal);
export default EsattoScheduledTaskScheduleModal;
