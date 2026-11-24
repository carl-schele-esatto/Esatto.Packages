import { beforeEach, describe, expect, it, vi } from 'vitest';

// A stand-in for the backoffice's shared HTTP client. It answers like the real one does when a call says throwOnError: false: a
// success is { data, response }, an HTTP error is { error: <the parsed body, or the text, or {}>, response }, and only a request
// that never got an answer (a network failure) rejects.
const client = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }));
vi.mock('@umbraco-cms/backoffice/http-client', () => ({ umbHttpClient: client }));

import * as api from './api';
import { ApiError, isApiError, toApiError } from './api-error';

const BASE = '/umbraco/management/api/v1/esatto/seo-report';

const ok = (data: unknown, status = 200) => ({ data, response: { status } });
const failed = (error: unknown, status: number) => ({ data: undefined, error, response: { status } });

const lastCall = (verb: 'get' | 'post' | 'put') => client[verb].mock.calls[client[verb].mock.calls.length - 1][0] as {
  url: string;
  body?: unknown;
  headers?: Record<string, string>;
  security?: unknown;
  throwOnError?: boolean;
};

beforeEach(() => {
  for (const verb of Object.values(client)) verb.mockReset();
});

describe('BASE', () => {
  it('is the controller route', () => {
    expect(api.BASE).toBe(BASE);
  });
});

describe('the endpoints', () => {
  const security = [{ scheme: 'bearer', type: 'http' }];

  it('GET status', async () => {
    client.get.mockResolvedValue(ok({ setup: 'ready' }));

    expect(await api.getStatus()).toEqual({ setup: 'ready' });
    expect(lastCall('get')).toMatchObject({ url: `${BASE}/status`, security, throwOnError: false });
  });

  it('GET report, with the month when there is one and without when there is none', async () => {
    client.get.mockResolvedValue(ok({ report: null }));

    await api.getReport('2026-07');
    expect(lastCall('get').url).toBe(`${BASE}/report?period=2026-07`);

    await api.getReport();
    expect(lastCall('get').url).toBe(`${BASE}/report`);
  });

  it('POST refresh, always with the month', async () => {
    client.post.mockResolvedValue(ok({ report: null }));

    await api.refreshReport('2026-07');

    expect(lastCall('post')).toMatchObject({ url: `${BASE}/refresh?period=2026-07`, security, throwOnError: false });
    expect(lastCall('post').body).toBeUndefined();
  });

  it('POST fetch and GET fetch/progress', async () => {
    client.post.mockResolvedValue(ok({ result: 'started' }, 202));
    client.get.mockResolvedValue(ok({ running: true }));

    await api.startFetch();
    await api.getFetchProgress();

    expect(lastCall('post').url).toBe(`${BASE}/fetch`);
    expect(lastCall('get').url).toBe(`${BASE}/fetch/progress`);
  });

  it('PUT note, with the month in the query and the text in the body', async () => {
    client.put.mockResolvedValue(ok({ note: 'a note' }));

    expect(await api.putNote('2026-07', 'a note')).toEqual({ note: 'a note' });

    expect(lastCall('put')).toMatchObject({
      url: `${BASE}/note?period=2026-07`,
      body: { text: 'a note' },
      headers: { 'Content-Type': 'application/json' },
      security,
    });
  });

  it('GET and PUT targets', async () => {
    client.get.mockResolvedValue(ok({ terms: ['a'] }));
    client.put.mockResolvedValue(ok({ terms: ['a', 'b'] }));

    await api.getTargets();
    await api.putTargets(['a', 'b']);

    expect(lastCall('get').url).toBe(`${BASE}/targets`);
    expect(lastCall('put')).toMatchObject({ url: `${BASE}/targets`, body: { terms: ['a', 'b'] } });
  });

  it('GET analysis, with the month when there is one', async () => {
    client.get.mockResolvedValue(ok({ hasAnalysis: false }));

    expect(await api.getAnalysis('2026-07')).toEqual({ hasAnalysis: false });
    expect(lastCall('get').url).toBe(`${BASE}/analysis?period=2026-07`);

    await api.getAnalysis();
    expect(lastCall('get').url).toBe(`${BASE}/analysis`);
  });

  it('GET daily: the rolling window has no month, a calendar month has one', async () => {
    client.get.mockResolvedValue(ok({}));

    await api.getDaily();
    expect(lastCall('get').url).toBe(`${BASE}/daily`);

    await api.getDaily('');
    expect(lastCall('get').url).toBe(`${BASE}/daily`);

    await api.getDaily('2026-07');
    expect(lastCall('get').url).toBe(`${BASE}/daily?month=2026-07`);
  });

  it('GET daily/sources, with the day', async () => {
    client.get.mockResolvedValue(ok({ date: '2026-07-05', sources: [] }));

    await api.getDailySources('2026-07-05');

    expect(lastCall('get').url).toBe(`${BASE}/daily/sources?date=2026-07-05`);
  });

  it('the settings endpoints', async () => {
    client.get.mockResolvedValue(ok({}));
    client.put.mockResolvedValue(ok({}));
    client.post.mockResolvedValue(ok({}));
    const settings = { ga4PropertyId: '1', searchConsoleSite: 'sc-domain:example.com', brandTerms: [], displayName: null, findingsUserGroups: [], autoFetch: true };

    await api.getSettings();
    expect(lastCall('get').url).toBe(`${BASE}/settings`);

    await api.putSettings({ settings, serviceAccountJson: null, removeKey: false });
    expect(lastCall('put')).toMatchObject({ url: `${BASE}/settings`, body: { settings, serviceAccountJson: null, removeKey: false } });

    await api.testConnection({ ga4PropertyId: '1', searchConsoleSite: 'sc-domain:example.com', serviceAccountJson: null });
    expect(lastCall('post')).toMatchObject({ url: `${BASE}/settings/test`, headers: { 'Content-Type': 'application/json' } });

    await api.getUserGroups();
    expect(lastCall('get').url).toBe(`${BASE}/user-groups`);
  });

  it('writes a value that came from the user into the URL encoded', async () => {
    client.get.mockResolvedValue(ok({}));

    await api.getReport('2026-07&x=1#');

    expect(lastCall('get').url).toBe(`${BASE}/report?period=2026-07%26x%3D1%23`);
  });
});

// A problem the server sends: ProblemDetails with the code, the arguments and the trace id as extensions.
const problem = (status: number, extra: Record<string, unknown> = {}) => ({ type: 'x', title: 'The request failed.', status, traceId: 'trace-1', ...extra });

describe('a failed request', () => {
  const rejection = async (run: () => Promise<unknown>): Promise<ApiError> => {
    try {
      await run();
    } catch (error) {
      expect(isApiError(error)).toBe(true);
      return error as ApiError;
    }
    throw new Error('expected the call to reject');
  };

  it('is an ApiError with the status, the code, the arguments and the trace id of the problem', async () => {
    client.post.mockResolvedValue(failed(problem(502, { code: 'apiDisabled', args: { api: 'searchConsole' } }), 502));

    const error = await rejection(() => api.refreshReport('2026-07'));

    expect(error).toBeInstanceOf(Error);
    expect(error.name).toBe('ApiError');
    expect(error.status).toBe(502);
    expect(error.code).toBe('apiDisabled');
    expect(error.args).toEqual({ api: 'searchConsole' });
    expect(error.traceId).toBe('trace-1');
  });

  it('takes the status from the response when the body has none', async () => {
    client.get.mockResolvedValue(failed({ code: 'periodInvalid' }, 400));

    expect((await rejection(() => api.getReport('x'))).status).toBe(400);
  });

  it('is `unknown` for a 502 with no code, but keeps the status and the trace id', async () => {
    client.post.mockResolvedValue(failed(problem(502), 502));

    const error = await rejection(() => api.refreshReport('2026-07'));

    expect(error.status).toBe(502);
    expect(error.code).toBe('unknown');
    expect(error.traceId).toBe('trace-1');
  });

  it('is `unknown` for any other answer without a code: a 500, the framework\'s own 400, a 403 with an empty body', async () => {
    for (const [body, status] of [[problem(500), 500], [problem(400, { errors: { period: ['bad'] } }), 400], [{}, 403], ['', 403]] as const) {
      client.get.mockResolvedValue(failed(body, status));

      const error = await rejection(() => api.getStatus());

      expect(error.code, `${status}`).toBe('unknown');
      expect(error.status).toBe(status);
    }
  });

  it('is `unknown` for a body that is not JSON (a gateway page), keeping the status of the response', async () => {
    client.get.mockResolvedValue(failed('<html>Bad gateway</html>', 502));

    const error = await rejection(() => api.getStatus());

    expect(error.code).toBe('unknown');
    expect(error.status).toBe(502);
    expect(error.traceId).toBeUndefined();
  });

  it('is `unknown` for a code this client has no words for', async () => {
    client.get.mockResolvedValue(failed(problem(400, { code: 'brandNewCode' }), 400));

    expect((await rejection(() => api.getStatus())).code).toBe('unknown');
  });

  it('is `unknown` with status 0 for a network failure (fetch rejects with a TypeError)', async () => {
    client.get.mockRejectedValue(new TypeError('Failed to fetch'));

    const error = await rejection(() => api.getStatus());

    expect(error.status).toBe(0);
    expect(error.code).toBe('unknown');
    expect(error.args).toBeUndefined();
    expect(error.traceId).toBeUndefined();
  });

  it('reads a problem that the client rejected with, as well as one it returned', async () => {
    client.get.mockRejectedValue(problem(409, { code: 'notConfigured' }));

    const error = await rejection(() => api.getStatus());

    expect(error.status).toBe(409);
    expect(error.code).toBe('notConfigured');
  });

  it('reads the problem inside an UmbApiError', async () => {
    client.get.mockRejectedValue(Object.assign(new Error('x'), { name: 'UmbApiError', status: 409, problemDetails: problem(409, { code: 'alreadyRunning' }) }));

    const error = await rejection(() => api.getStatus());

    expect(error.status).toBe(409);
    expect(error.code).toBe('alreadyRunning');
  });

  it('keeps only string, number, boolean and null arguments', async () => {
    client.get.mockResolvedValue(failed(problem(502, { code: 'noAccess', args: { source: 'ga4', n: 2, flag: true, none: null, nested: { a: 1 }, list: [1] } }), 502));

    expect((await rejection(() => api.getStatus())).args).toEqual({ source: 'ga4', n: 2, flag: true, none: null });
  });

  it('knows every code of both C# lists', () => {
    for (const code of ['notConfigured', 'keyUnreadable', 'keyInvalid', 'apiDisabled', 'noAccess', 'propertyNotFound', 'noData', 'quota', 'unknown', 'ga4PropertyInvalid', 'searchConsoleInvalid', 'periodInvalid', 'periodOutOfRange', 'dateInvalid', 'dateOutOfRange', 'alreadyRunning', 'requestInvalid']) {
      expect(toApiError(problem(400, { code }), 400).code, code).toBe(code);
    }
  });
});
