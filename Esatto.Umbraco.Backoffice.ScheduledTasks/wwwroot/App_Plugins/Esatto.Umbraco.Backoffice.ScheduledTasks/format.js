// Pure formatting helpers for the Scheduled Tasks dashboard. No @umbraco-cms imports, so they
// unit-test in plain Node (see Esatto.Umbraco.Backoffice.ScheduledTasks.Tests/client).

const DAY = 86400;

// Units an editor can pick for an interval, largest first. Monthly and yearly runs are calendar
// schedules ("on the 1st", "on 15 March") rather than fixed intervals.
export const INTERVAL_UNITS = [
    { unit: 'weeks', seconds: 7 * DAY, singular: 'week', label: 'weeks' },
    { unit: 'days', seconds: DAY, singular: 'day', label: 'days' },
    { unit: 'hours', seconds: 3600, singular: 'hour', label: 'hours' },
    { unit: 'minutes', seconds: 60, singular: 'minute', label: 'minutes' },
    { unit: 'seconds', seconds: 1, singular: 'second', label: 'seconds' },
];

// Compact units for elapsed times (how long a run took).
const DURATION_UNITS = [
    { seconds: DAY, short: 'd' },
    { seconds: 3600, short: 'h' },
    { seconds: 60, short: 'min' },
    { seconds: 1, short: 's' },
];

/** 300 → "5 min", 5400 → "1 h 30 min", 90061 → "1 d 1 h" (two largest parts). */
export function formatDuration(seconds) {
    if (seconds == null || !Number.isFinite(seconds)) return '';
    let remaining = Math.round(seconds);
    if (remaining <= 0) return '0 s';

    const parts = [];
    for (const { seconds: size, short } of DURATION_UNITS) {
        const count = Math.floor(remaining / size);
        if (count > 0) {
            parts.push(`${count} ${short}`);
            remaining -= count * size;
        }
        if (parts.length === 2) break;
    }
    return parts.join(' ');
}

/**
 * Interval label for a task, in the largest unit that expresses it exactly:
 * 3600 → "Every hour", 1209600 → "Every 2 weeks", 5400 → "Every 90 minutes".
 * null means the task never runs on its own.
 */
export function formatInterval(seconds) {
    if (seconds == null) return 'On demand only';
    const { value, unit } = splitInterval(seconds);
    if (value === 1) return `Every ${INTERVAL_UNITS.find((u) => u.unit === unit).singular}`;
    return `Every ${value} ${unit}`;
}

/** Run time: 320 → "320 ms", 4200 → "4.2 s", 95000 → "1 min 35 s". */
export function formatElapsed(ms) {
    if (ms == null || !Number.isFinite(ms)) return '';
    if (ms < 1000) return `${Math.round(ms)} ms`;
    if (ms < 60000) return `${(ms / 1000).toFixed(1).replace(/\.0$/, '')} s`;
    return formatDuration(ms / 1000);
}

/** "in 3 minutes", "2 hours ago", "in 1 day", "now" — relative to `now` (ms since epoch). */
export function formatRelative(iso, now = Date.now(), locale = 'en') {
    if (!iso) return '';
    const diffSeconds = (new Date(iso).getTime() - now) / 1000;
    const abs = Math.abs(diffSeconds);
    // Always numeric: the values are rounded durations, so "tomorrow" or "next year" could name
    // the wrong calendar day or year (31 hours from 20:00 is two days later).
    const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'always' });

    if (abs < 5) return new Intl.RelativeTimeFormat(locale, { numeric: 'auto' }).format(0, 'second');
    if (abs < 60) return rtf.format(Math.round(diffSeconds), 'second');
    if (abs < 3600) return rtf.format(Math.round(diffSeconds / 60), 'minute');
    if (abs < DAY) return rtf.format(Math.round(diffSeconds / 3600), 'hour');
    if (abs < 14 * DAY) return rtf.format(Math.round(diffSeconds / DAY), 'day');
    if (abs < 60 * DAY) return rtf.format(Math.round(diffSeconds / (7 * DAY)), 'week');
    if (abs < 365 * DAY) return rtf.format(Math.round(diffSeconds / (30 * DAY)), 'month');
    return rtf.format(Math.round(diffSeconds / (365 * DAY)), 'year');
}

/** Largest unit that expresses the interval exactly: 300 → { value: 5, unit: 'minutes' }. */
export function splitInterval(seconds) {
    if (seconds == null || seconds <= 0) return { value: 1, unit: 'hours' };
    for (const { unit, seconds: size } of INTERVAL_UNITS) {
        if (seconds >= size && Number.isInteger(seconds / size)) return { value: seconds / size, unit };
    }
    return { value: seconds, unit: 'seconds' };
}

/** { value: 5, unit: 'minutes' } → 300; NaN for anything that isn't a positive number. */
export function toSeconds(value, unit) {
    const size = INTERVAL_UNITS.find((u) => u.unit === unit)?.seconds;
    const number = typeof value === 'number' ? value : Number(String(value).trim().replace(',', '.'));
    if (!size || !Number.isFinite(number) || number <= 0) return Number.NaN;
    // Whole milliseconds: 0.7 days is 60480 s, not the float 60479.99999999999.
    return Math.round(number * size * 1000) / 1000;
}

// Calendar schedules, as the API sends them: { kind, time, weekdays, dayOfMonth, month, cron, timeZone }.
export const WEEKDAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
export const MONTHS = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December',
];

/** 1 → "1st", 22 → "22nd", 13 → "13th". */
export function ordinal(n) {
    const tens = n % 100;
    if (tens >= 11 && tens <= 13) return `${n}th`;
    return `${n}${{ 1: 'st', 2: 'nd', 3: 'rd' }[n % 10] ?? 'th'}`;
}

/** ['Monday'] → "Monday"; ['Monday', 'Thursday', 'Friday'] → "Monday, Thursday and Friday". */
function joinList(items) {
    return items.length <= 1 ? items.join('') : `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`;
}

/** "Every Monday at 07:00", "Every month on the 1st at 06:00", "Every year on 15 March at 09:00". */
export function describeSchedule(schedule) {
    if (!schedule) return '';
    const at = schedule.time ? ` at ${schedule.time}` : '';

    switch (schedule.kind) {
        case 'daily':
            return `Every day${at}`;
        case 'weekly': {
            const days = WEEKDAYS.filter((d) => schedule.weekdays?.includes(d));
            if (days.length === 7) return `Every day${at}`;
            if (days.join() === WEEKDAYS.slice(0, 5).join()) return `Every weekday${at}`;
            return `Every ${joinList(days)}${at}`;
        }
        case 'monthly':
            return `Every month on the ${ordinal(schedule.dayOfMonth)}${at}`;
        case 'yearly':
            return `Every year on ${schedule.dayOfMonth} ${MONTHS[schedule.month - 1] ?? '?'}${at}`;
        case 'cron':
            return `Cron: ${schedule.cron}`;
        default:
            return '';
    }
}

/** Last segment of a type name: "Umbraco.Cms.Jobs.TouchServerJob" → "TouchServerJob". */
export function shortTypeName(typeName) {
    if (!typeName) return '';
    const parts = typeName.split(/[.+]/);
    return parts[parts.length - 1];
}

/** Case-insensitive match on name, type name and kind. */
export function matchesFilter(task, term) {
    const needle = (term ?? '').trim().toLowerCase();
    if (!needle) return true;
    return [task.name, task.typeName, task.kind].some((value) => (value ?? '').toLowerCase().includes(needle));
}

/** 1 → "1 second", 300 → "5 minutes", 7200 → "2 hours" (largest exact unit). */
export function formatLength(seconds) {
    const { value, unit } = splitInterval(seconds);
    return value === 1 ? `1 ${INTERVAL_UNITS.find((u) => u.unit === unit).singular}` : `${value} ${unit}`;
}

/**
 * True when the task only runs when started or triggered: no period and no calendar schedule.
 * A task on a calendar can report no period (its own is infinite) and still runs on schedule.
 */
export function isOnDemand(task) {
    return task.periodSeconds == null && !task.schedule;
}

/**
 * The interval the schedule editor starts from. On a calendar schedule the reported period is
 * derived from it (for distributed tasks the time until the next occurrence), so the task's own
 * interval is used instead; null (never runs on its own) leaves the editor's default.
 */
export function initialIntervalSeconds(task) {
    const seconds = task.schedule ? task.defaultPeriodSeconds : task.periodSeconds;
    return seconds != null && Number.isFinite(seconds) && seconds > 0 ? seconds : null;
}

/**
 * Time zones to choose from: the browser's list (which leaves out "UTC" in Chromium and Node)
 * with "UTC" always added, and the current value first when the list doesn't have it.
 */
export function timeZoneOptions(current, supported) {
    const zones = supported?.length ? [...supported] : [];
    if (!zones.includes('UTC')) zones.unshift('UTC');
    return current && !zones.includes(current) ? [current, ...zones] : zones;
}

/** Message when an interval is shorter than the task allows ('' when it's fine or there's no limit). */
export function minimumIntervalError(seconds, minimumPeriodSeconds) {
    if (minimumPeriodSeconds == null || !Number.isFinite(minimumPeriodSeconds) || minimumPeriodSeconds <= 0) return '';
    return seconds < minimumPeriodSeconds
        ? `This task can’t run more often than every ${formatLength(minimumPeriodSeconds)}.`
        : '';
}
