// Run with: node --test Esatto.Umbraco.Backoffice.ScheduledTasks.Tests/client/format.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    WEEKDAYS,
    describeSchedule,
    ordinal,
    formatDuration,
    formatElapsed,
    formatInterval,
    formatLength,
    initialIntervalSeconds,
    isOnDemand,
    minimumIntervalError,
    timeZoneOptions,
    formatRelative,
    matchesFilter,
    shortTypeName,
    splitInterval,
    toSeconds,
} from '../../Esatto.Umbraco.Backoffice.ScheduledTasks/wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.ScheduledTasks/format.js';

test('formatDuration keeps the two largest units', () => {
    assert.equal(formatDuration(45), '45 s');
    assert.equal(formatDuration(300), '5 min');
    assert.equal(formatDuration(5400), '1 h 30 min');
    assert.equal(formatDuration(90061), '1 d 1 h');
    assert.equal(formatDuration(0), '0 s');
    assert.equal(formatDuration(null), '');
});

test('formatInterval names the interval in its largest exact unit', () => {
    assert.equal(formatInterval(1), 'Every second');
    assert.equal(formatInterval(30), 'Every 30 seconds');
    assert.equal(formatInterval(3600), 'Every hour');
    assert.equal(formatInterval(5400), 'Every 90 minutes');
    assert.equal(formatInterval(86400), 'Every day');
    assert.equal(formatInterval(7 * 86400), 'Every week');
    assert.equal(formatInterval(14 * 86400), 'Every 2 weeks');
    assert.equal(formatInterval(30 * 86400), 'Every 30 days');
    assert.equal(formatInterval(365 * 86400), 'Every 365 days');
    assert.equal(formatInterval(null), 'On demand only');
});

test('describeSchedule reads calendar schedules', () => {
    assert.equal(describeSchedule({ kind: 'daily', time: '07:00' }), 'Every day at 07:00');
    assert.equal(describeSchedule({ kind: 'weekly', time: '07:00', weekdays: ['Monday'] }), 'Every Monday at 07:00');
    assert.equal(describeSchedule({ kind: 'weekly', time: '07:00', weekdays: ['Thursday', 'Monday'] }), 'Every Monday and Thursday at 07:00');
    assert.equal(describeSchedule({ kind: 'weekly', time: '08:30', weekdays: ['Monday', 'Wednesday', 'Friday'] }), 'Every Monday, Wednesday and Friday at 08:30');
    assert.equal(describeSchedule({ kind: 'weekly', time: '07:00', weekdays: WEEKDAYS.slice(0, 5) }), 'Every weekday at 07:00');
    assert.equal(describeSchedule({ kind: 'monthly', time: '06:00', dayOfMonth: 1 }), 'Every month on the 1st at 06:00');
    assert.equal(describeSchedule({ kind: 'monthly', time: '06:00', dayOfMonth: 22 }), 'Every month on the 22nd at 06:00');
    assert.equal(describeSchedule({ kind: 'yearly', time: '09:00', dayOfMonth: 15, month: 3 }), 'Every year on 15 March at 09:00');
    assert.equal(describeSchedule({ kind: 'cron', cron: '0 7 * * 1-5' }), 'Cron: 0 7 * * 1-5');
    assert.equal(describeSchedule(null), '');
});

test('ordinal', () => {
    assert.deepEqual([1, 2, 3, 4, 11, 12, 13, 21, 22, 23, 31].map(ordinal),
        ['1st', '2nd', '3rd', '4th', '11th', '12th', '13th', '21st', '22nd', '23rd', '31st']);
});

test('formatElapsed scales from milliseconds to minutes', () => {
    assert.equal(formatElapsed(320.4), '320 ms');
    assert.equal(formatElapsed(4200), '4.2 s');
    assert.equal(formatElapsed(5000), '5 s');
    assert.equal(formatElapsed(95000), '1 min 35 s');
    assert.equal(formatElapsed(undefined), '');
});

test('formatRelative describes past and future times', () => {
    const now = Date.parse('2026-10-18T08:00:00Z');
    assert.equal(formatRelative('2026-10-18T08:03:00Z', now), 'in 3 minutes');
    assert.equal(formatRelative('2026-10-18T06:00:00Z', now), '2 hours ago');
    assert.equal(formatRelative('2026-10-18T08:00:02Z', now), 'now');
    assert.equal(formatRelative('2026-10-20T08:00:00Z', now), 'in 2 days');
    assert.equal(formatRelative('2026-11-08T08:00:00Z', now), 'in 3 weeks');
    assert.equal(formatRelative('2027-01-16T08:00:00Z', now), 'in 3 months');
    assert.equal(formatRelative('2027-10-18T08:00:00Z', now), 'in 1 year');
    assert.equal(formatRelative('2028-10-17T08:00:00Z', now), 'in 2 years');
    assert.equal(formatRelative(null, now), '');
});

test('formatRelative never names a calendar day or year it can get wrong', () => {
    // Monday 20:00 → Wednesday 03:00 is 31 hours: not "tomorrow".
    const mondayEvening = Date.parse('2026-10-19T20:00:00Z');
    assert.equal(formatRelative('2026-10-21T03:00:00Z', mondayEvening), 'in 1 day');
    // Saturday 20:00 → Monday 02:00 is 30 hours ago: not "yesterday".
    assert.equal(formatRelative('2026-10-17T20:00:00Z', Date.parse('2026-10-19T02:00:00Z')), '1 day ago');
    // 500 days ahead lands two calendar years on: not "next year".
    assert.equal(formatRelative('2028-03-01T08:00:00Z', Date.parse('2026-10-18T08:00:00Z')), 'in 1 year');
});

test('splitInterval picks the largest exact unit', () => {
    assert.deepEqual(splitInterval(300), { value: 5, unit: 'minutes' });
    assert.deepEqual(splitInterval(5400), { value: 90, unit: 'minutes' });
    assert.deepEqual(splitInterval(86400), { value: 1, unit: 'days' });
    assert.deepEqual(splitInterval(14 * 86400), { value: 2, unit: 'weeks' });
    assert.deepEqual(splitInterval(30 * 86400), { value: 30, unit: 'days' });
    assert.deepEqual(splitInterval(90), { value: 90, unit: 'seconds' });
    assert.deepEqual(splitInterval(null), { value: 1, unit: 'hours' });
});

test('toSeconds converts editor input and rejects nonsense', () => {
    assert.equal(toSeconds(5, 'minutes'), 300);
    assert.equal(toSeconds('1,5', 'hours'), 5400);
    assert.equal(toSeconds(2, 'weeks'), 14 * 86400);
    // Decimal input lands on whole milliseconds, not float noise below them.
    assert.equal(toSeconds('0,7', 'days'), 60480);
    assert.equal(toSeconds('4.1', 'minutes'), 246);
    assert.equal(toSeconds('8.2', 'hours'), 29520);
    assert.ok(Number.isNaN(toSeconds(1, 'months')));
    assert.ok(Number.isNaN(toSeconds('', 'minutes')));
    assert.ok(Number.isNaN(toSeconds(-1, 'minutes')));
    assert.ok(Number.isNaN(toSeconds(5, 'fortnights')));
});

test('shortTypeName and matchesFilter', () => {
    assert.equal(shortTypeName('Umbraco.Cms.Jobs.TouchServerJob'), 'TouchServerJob');
    assert.equal(shortTypeName('My.Jobs+Nested'), 'Nested');

    const task = { name: 'Temp file cleanup', typeName: 'Umbraco.Jobs.TempFileCleanupJob', kind: 'recurring' };
    assert.ok(matchesFilter(task, 'TEMP'));
    assert.ok(matchesFilter(task, 'recurring'));
    assert.ok(matchesFilter(task, '  '));
    assert.ok(!matchesFilter(task, 'webhook'));
});

test('isOnDemand: a calendar task with no period of its own still runs on schedule', () => {
    assert.ok(isOnDemand({ periodSeconds: null, schedule: null }));
    assert.ok(!isOnDemand({ periodSeconds: 3600, schedule: null }));
    assert.ok(!isOnDemand({ periodSeconds: null, schedule: { kind: 'weekly', time: '07:00', weekdays: ['Monday'] } }));
});

test('initialIntervalSeconds uses the task’s own interval while on a calendar', () => {
    const schedule = { kind: 'weekly', time: '07:00', weekdays: ['Monday'] };
    // A distributed task on a calendar reports the time until the next occurrence.
    assert.equal(initialIntervalSeconds({ periodSeconds: 604799.997, defaultPeriodSeconds: 3600, schedule }), 3600);
    assert.equal(initialIntervalSeconds({ periodSeconds: 604799.997, defaultPeriodSeconds: null, schedule }), null);
    assert.equal(initialIntervalSeconds({ periodSeconds: 900, defaultPeriodSeconds: 3600, schedule: null }), 900);
    assert.equal(initialIntervalSeconds({ periodSeconds: null, defaultPeriodSeconds: null, schedule: null }), null);
    assert.deepEqual(splitInterval(initialIntervalSeconds({ periodSeconds: 277199.997, defaultPeriodSeconds: null, schedule })), { value: 1, unit: 'hours' });
});

test('timeZoneOptions always offers UTC and keeps an unknown current zone', () => {
    // Chromium/Node leave UTC out of Intl.supportedValuesOf('timeZone').
    assert.deepEqual(timeZoneOptions('Europe/Stockholm', ['Europe/Oslo', 'Europe/Stockholm']), ['UTC', 'Europe/Oslo', 'Europe/Stockholm']);
    assert.deepEqual(timeZoneOptions('UTC', ['Europe/Stockholm']), ['UTC', 'Europe/Stockholm']);
    assert.deepEqual(timeZoneOptions('Etc/GMT-2', ['UTC', 'Europe/Stockholm']), ['Etc/GMT-2', 'UTC', 'Europe/Stockholm']);
    assert.deepEqual(timeZoneOptions(null, undefined), ['UTC']);
    assert.ok(timeZoneOptions(null, Intl.supportedValuesOf('timeZone')).includes('UTC'));
});

test('minimumIntervalError and formatLength', () => {
    assert.equal(formatLength(1), '1 second');
    assert.equal(formatLength(300), '5 minutes');
    assert.equal(minimumIntervalError(2, 5), 'This task can’t run more often than every 5 seconds.');
    assert.equal(minimumIntervalError(5, 5), '');
    assert.equal(minimumIntervalError(1, null), '');
    assert.equal(minimumIntervalError(1, undefined), '');
});
