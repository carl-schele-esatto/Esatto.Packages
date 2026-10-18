import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

// Management API endpoints require bearer auth. umbHttpClient is the pre-configured Bellissima
// OpenAPI client; security must be declared explicitly or it doesn't attach the token (401).
// tryExecute wraps the request and returns { data, error }.
const API_BASE = '/umbraco/management/api/v1/backoffice/scheduled-tasks';
const SECURITY = [{ scheme: 'bearer', type: 'http' }];

// Task ids contain dots and colons, so they go in the query string or body, never the path.
const byId = (path, id) => `${API_BASE}/${path}?id=${encodeURIComponent(id)}`;

// The dashboard reports errors itself (inline or as its own toast), so calls opt out of the
// backoffice's generic error toast — which would otherwise fire on every background refresh.
const QUIET = { disableNotifications: true };

export function getOverview(host) {
    return tryExecute(host, umbHttpClient.get({ url: API_BASE, security: SECURITY }), QUIET);
}

export function getRuns(host, id) {
    return tryExecute(host, umbHttpClient.get({ url: byId('runs', id), security: SECURITY }));
}

export function clearRuns(host, id) {
    return tryExecute(host, umbHttpClient.delete({ url: byId('runs', id), security: SECURITY }));
}

export function runNow(host, id) {
    return tryExecute(host, umbHttpClient.post({ url: `${API_BASE}/run`, body: { id }, security: SECURITY }), QUIET);
}

/**
 * Clears the "running" flag of a distributed task whose run was interrupted. Pass the
 * runningSinceUtc the dashboard showed, so a run picked up since isn't cleared.
 */
export function resetRun(host, id, runningSinceUtc = null) {
    return tryExecute(host, umbHttpClient.post({ url: `${API_BASE}/reset`, body: { id, runningSinceUtc }, security: SECURITY }), QUIET);
}

export function setPaused(host, id, paused) {
    const url = `${API_BASE}/${paused ? 'pause' : 'resume'}`;
    return tryExecute(host, umbHttpClient.post({ url, body: { id }, security: SECURITY }), QUIET);
}

/** periodSeconds null restores the task's own interval. */
export function setPeriod(host, id, periodSeconds) {
    return tryExecute(
        host,
        umbHttpClient.put({ url: `${API_BASE}/period`, body: { id, periodSeconds }, security: SECURITY }),
        QUIET,
    );
}

/**
 * A calendar schedule { kind, time, weekdays, dayOfMonth, month, cron, timeZone } for the task;
 * null goes back to its interval.
 */
export function setSchedule(host, id, schedule) {
    return tryExecute(
        host,
        umbHttpClient.put({ url: `${API_BASE}/schedule`, body: { id, schedule }, security: SECURITY }),
        QUIET,
    );
}

/** { nextRunsUtc: [...], error } for a schedule, without saving it. */
export function previewSchedule(host, schedule, count = 5) {
    return tryExecute(
        host,
        umbHttpClient.post({ url: `${API_BASE}/schedule/preview`, body: { schedule, count }, security: SECURITY }),
        QUIET,
    );
}

/** The task's properties form: { definitions, values: [{ alias, value }], isCustomised, warnings }. */
export function getProperties(host, id) {
    return tryExecute(host, umbHttpClient.get({ url: byId('properties', id), security: SECURITY }), QUIET);
}

/** values: [{ alias, value }] as the property editors emitted them; null restores all defaults. */
export function setProperties(host, id, values) {
    return tryExecute(
        host,
        umbHttpClient.put({ url: `${API_BASE}/properties`, body: { id, values }, security: SECURITY }),
        QUIET,
    );
}

/** The message to show for a failed command: the API's ProblemDetails title when there is one. */
export function errorMessage(error, fallback) {
    return error?.problemDetails?.title ?? error?.message ?? fallback;
}
