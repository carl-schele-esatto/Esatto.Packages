// Run with: node --test Esatto.Umbraco.Backoffice.ScheduledTasks.Tests/client/schedule-modal.test.mjs
// Loads the schedule sidebar with the Umbraco and Lit imports stubbed, so its logic (seeding,
// saving, closing mid-save, toggle markup) runs in plain Node. Templates are captured as
// { strings, values } instead of rendered.
import { test, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const pluginDir = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    '../../Esatto.Umbraco.Backoffice.ScheduledTasks/wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.ScheduledTasks',
);

const moduleUrl = (source) => `data:text/javascript,${encodeURIComponent(source)}`;
const stubs = {
    '@umbraco-cms/backoffice/external/lit': moduleUrl(`
        export const nothing = Symbol('nothing');
        export const html = (strings, ...values) => ({ strings: [...strings], values });
        export const css = (strings, ...values) => ({ strings: [...strings], values });
        export const live = (value) => value;
    `),
    // Like UmbModalBaseElement over a destroyed modal context, setting the value once the modal
    // has closed throws.
    '@umbraco-cms/backoffice/modal': moduleUrl(`
        export class UmbModalBaseElement {
            isConnected = true;
            data;
            modalContext;
            submitted = 0;
            #value;
            set value(value) {
                if (this.modalContext?.isResolved?.()) throw new Error('_subject is undefined');
                this.#value = value;
            }
            get value() { return this.#value; }
            connectedCallback() {}
            disconnectedCallback() {}
            _submitModal() { this.submitted++; }
            _rejectModal() {}
        }
    `),
    '@esatto/scheduled-tasks/api.js': moduleUrl(`
        export const errorMessage = (error, fallback) => error?.message ?? fallback;
        export const previewSchedule = (...args) => globalThis.__api.previewSchedule(...args);
        export const setPeriod = (...args) => globalThis.__api.setPeriod(...args);
        export const setSchedule = (...args) => globalThis.__api.setSchedule(...args);
    `),
};

registerHooks({
    resolve(specifier, context, nextResolve) {
        if (stubs[specifier]) return { url: stubs[specifier], shortCircuit: true };
        if (specifier.startsWith('@esatto/scheduled-tasks/')) {
            const file = path.join(pluginDir, specifier.slice('@esatto/scheduled-tasks/'.length));
            return { url: pathToFileURL(file).href, shortCircuit: true };
        }
        return nextResolve(specifier, context);
    },
});

const defined = new Map();
globalThis.customElements = { define: (name, ctor) => defined.set(name, ctor) };

const calls = [];
globalThis.__api = {
    previewSchedule: async () => ({ data: { nextRunsUtc: [] } }),
    setPeriod: async (_host, id, seconds) => {
        calls.push(['setPeriod', id, seconds]);
        return { data: { task: { id, periodSeconds: seconds }, message: 'Interval saved.' } };
    },
    setSchedule: async (_host, id, schedule) => {
        calls.push(['setSchedule', id, schedule]);
        return { data: { task: { id, schedule }, message: 'Schedule saved.' } };
    },
};

const { default: ScheduleModal } = await import(pathToFileURL(path.join(pluginDir, 'scheduled-task-schedule-modal.js')).href);

const mondayTask = {
    id: 'job-1',
    name: 'Report',
    kind: 'distributed',
    // On a calendar a distributed task reports the time until the next occurrence.
    periodSeconds: 604799.997,
    defaultPeriodSeconds: 2 * 3600,
    hasPeriodOverride: false,
    schedule: { kind: 'weekly', time: '07:00', weekdays: ['Monday'], timeZone: 'Europe/Stockholm' },
};

function open(task, extraData = {}) {
    const modal = new ScheduleModal();
    let resolved = false;
    modal.modalContext = { isResolved: () => resolved, close: () => { resolved = true; } };
    modal.data = { task, ...extraData };
    modal.connectedCallback();
    return modal;
}

/** Every captured template in a render tree, depth first. */
function templates(node, found = []) {
    if (Array.isArray(node)) node.forEach((n) => templates(n, found));
    else if (node && Array.isArray(node.strings)) {
        found.push(node);
        node.values.forEach((v) => templates(v, found));
    }
    return found;
}

/** The toggle buttons in a render, as { label, pressed, disabled }. */
function toggles(modal) {
    return templates(modal.render())
        .filter((t) => t.strings.join('').includes('class="toggle"'))
        .map((t) => {
            const attr = (name) => t.values[t.strings.findIndex((s) => s.trimEnd().endsWith(`${name}=`))];
            return { label: t.values[t.values.length - 1], pressed: attr('aria-pressed'), disabled: attr('?disabled'), markup: t.strings.join('') };
        });
}

afterEach(() => {
    calls.length = 0;
});

test('switching a calendar task to Interval starts from its own interval, not the time to the next run', () => {
    const modal = open(mondayTask);
    assert.equal(modal._mode, 'weekly');
    assert.deepEqual(modal._interval, { value: '2', unit: 'hours' });
    modal.disconnectedCallback();
});

test('an interval task still starts from its current (overridden) interval', () => {
    const modal = open({ ...mondayTask, schedule: null, periodSeconds: 900, hasPeriodOverride: true });
    assert.equal(modal._mode, 'interval');
    assert.deepEqual(modal._interval, { value: '15', unit: 'minutes' });
});

test('saving while open submits the result', async () => {
    const modal = open({ ...mondayTask, schedule: null, periodSeconds: 3600 });
    modal._interval = { value: '30', unit: 'minutes' };
    await clickSave(modal);
    assert.deepEqual(calls, [['setPeriod', 'job-1', 1800]]);
    assert.equal(modal.submitted, 1);
    assert.equal(modal.value.message, 'Interval saved.');
});

test('closing the sidebar while a save is in flight hands the result to the opener instead of throwing', async () => {
    const late = [];
    const modal = open({ ...mondayTask, schedule: null, periodSeconds: 3600 }, { onSavedAfterClose: (r) => late.push(r) });
    let release;
    globalThis.__api.setPeriod = async (_host, id, seconds) => {
        await new Promise((resolve) => { release = resolve; });
        return { data: { task: { id, periodSeconds: seconds }, message: 'Interval saved.' } };
    };
    try {
        const saving = clickSave(modal);
        modal.modalContext.close(); // Esc / backdrop: the context resolves while the PUT is pending
        modal.isConnected = false;
        release();
        await saving; // must not reject with '_subject is undefined'
    } finally {
        globalThis.__api.setPeriod = async (_host, id, seconds) => {
            calls.push(['setPeriod', id, seconds]);
            return { data: { task: { id, periodSeconds: seconds }, message: 'Interval saved.' } };
        };
    }
    assert.equal(modal.submitted, 0);
    assert.equal(late.length, 1);
    assert.equal(late[0].message, 'Interval saved.');
});

test('an interval below the task’s minimum is refused with a message, without calling the server', async () => {
    const modal = open({ ...mondayTask, schedule: null, periodSeconds: 60, minimumPeriodSeconds: 5 });
    modal._interval = { value: '2', unit: 'seconds' };
    await clickSave(modal);
    assert.deepEqual(calls, []);
    assert.match(modal._error, /more often than every 5 seconds/);

    modal._interval = { value: '5', unit: 'seconds' };
    await clickSave(modal);
    assert.deepEqual(calls, [['setPeriod', 'job-1', 5]]);
});

test('a task that can’t have a calendar has the calendar modes disabled and explained', async () => {
    const reason = 'This job aligns its runs to the clock, so it needs an interval.';
    const modal = open({ ...mondayTask, schedule: null, periodSeconds: 60, calendarUnavailableReason: reason });
    const modes = toggles(modal);
    assert.deepEqual(modes.map((m) => [m.label, m.disabled]), [
        ['Interval', false], ['Daily', true], ['Weekly', true], ['Monthly', true], ['Yearly', true], ['Cron', true],
    ]);
    assert.ok(templates(modal.render()).some((t) => t.values.includes(reason)));

    modal._mode = 'weekly'; // e.g. a schedule saved before the restriction applied
    await clickSave(modal);
    assert.deepEqual(calls, []);
    assert.equal(modal._error, reason);
});

test('missing calendarUnavailableReason and minimumPeriodSeconds leave everything enabled', async () => {
    const modal = open({ ...mondayTask, schedule: null, periodSeconds: 60 });
    assert.ok(toggles(modal).every((m) => m.disabled === false));
    modal._interval = { value: '1', unit: 'seconds' };
    await clickSave(modal);
    assert.deepEqual(calls, [['setPeriod', 'job-1', 1]]);
});

test('mode and weekday toggles are native buttons that expose aria-pressed', () => {
    const modal = open(mondayTask);
    const all = toggles(modal);
    assert.ok(all.every((t) => t.markup.includes('<button') && !t.markup.includes('uui-button')));
    const pressed = all.filter((t) => t.pressed === 'true').map((t) => t.label);
    assert.deepEqual(pressed, ['Weekly', 'Mon']);
    assert.equal(all.filter((t) => t.pressed === 'false').length, all.length - 2);
    modal.disconnectedCallback();
});

test('the time zone list offers UTC and the stored zone', () => {
    const modal = open({ ...mondayTask, schedule: { ...mondayTask.schedule, timeZone: 'Mars/Olympus_Mons' } });
    const select = templates(modal.render()).find((t) => t.strings.join('').includes('id="time-zone"'));
    const zones = templates(select).filter((t) => t.strings.join('').includes('<option')).map((t) => t.values[0]);
    assert.ok(zones.includes('UTC'));
    assert.equal(zones[0], 'Mars/Olympus_Mons');
    modal.disconnectedCallback();
});

/** Clicks Save (the primary action) and waits for it to finish. */
function clickSave(modal) {
    const save = templates(modal.render()).find((t) => t.strings.join('').includes('label="Save"'));
    return save.values[save.values.length - 1]();
}
