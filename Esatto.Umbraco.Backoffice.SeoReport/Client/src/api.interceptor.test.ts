// A problem has to reach the dashboard WITH ITS CODE, through the backoffice's own HTTP client, not through a stand-in for it.
//
// This runs the REAL shared client (umbHttpClient, a hey-api fetch client configured as Umbraco configures it) with Umbraco's REAL
// default interceptors bound to it (UmbApiInterceptorController, from node_modules, unchanged), over a fetch that answers with the bodies
// the server writes, and calls the real api.ts. What it pins, found by reading api-interceptor.controller.js (Umbraco 18.3.2):
//
//   - addErrorInterceptor reads the body of an error answer (any status but 401, 403, 404 and the gateway statuses) and keeps it ONLY when
//     isProblemDetailsLike holds: the members type, title and status are present, and detail, instance and operationStatus are absent or
//     text. Then the whole parsed body, extension members included, goes on to the client: code, args and traceId survive.
//   - Otherwise the body is replaced by a generic problem ({ status, title, type: 'ServerError' }) and the code is lost: every refusal would
//     read "unknown". That is why the server sets `type` itself (ApiProblems.Create) and why the bodies below are the ones it writes.
//   - 401, 403, 404 and the gateway statuses are replaced whatever the body says. We never answer with those; a gateway's own 502 page and
//     a policy's 403 are therefore `unknown`, correctly.
//
// problem-bodies.json is the shared truth: ProblemShapeTests (C#) holds every row to what ApiProblems.Create writes, byte for member.

import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import bodies from './testing/problem-bodies.json';

interface Body {
  type: string;
  title: string;
  status: number;
  code: string;
  args?: Record<string, string | number | boolean | null>;
  traceId: string;
}

type Api = typeof import('./api');
type Client = { setConfig(config: Record<string, unknown>): void };

let api: Api;
let client: Client;
let answer: { status: number; body: string; type: string } = { status: 200, body: '{}', type: 'application/json' };

beforeAll(async () => {
  // Importing the interceptor reaches code that reads `document` when it loads.
  (globalThis as { document?: unknown }).document ??= { documentElement: { lang: 'en', dir: 'ltr' } };

  const http = await import('@umbraco-cms/backoffice/http-client');
  client = http.umbHttpClient as unknown as Client;

  // Umbraco binds its default interceptors to this client at start-up; so do we, to the same client, with the same class.
  const { UmbApiInterceptorController } = await import('../node_modules/@umbraco-cms/backoffice/dist-cms/packages/core/resources/api-interceptor.controller.js');
  const host = { getHostElement: () => ({}), addUmbController: () => undefined, removeUmbController: () => undefined, hasUmbController: () => false, getUmbController: () => undefined };
  new UmbApiInterceptorController(host as never).bindDefaultInterceptors(http.umbHttpClient as never);

  api = await import('./api');
});

afterEach(() => {
  vi.restoreAllMocks();
});

beforeEach(() => {
  // No network: the client's fetch answers with what the test says the server sent.
  client.setConfig({
    baseUrl: 'http://backoffice.test',
    fetch: async () =>
      new Response(answer.body, { status: answer.status, headers: { 'Content-Type': answer.type } }),
  });
});

const serverSays = (status: number, body: unknown) => (answer = { status, body: JSON.stringify(body), type: 'application/problem+json' });

const refused = async (run: () => Promise<unknown>) => {
  try {
    await run();
  } catch (error) {
    return error as InstanceType<Api['ApiError']>;
  }
  throw new Error('expected the call to reject');
};

describe('the real client with the real interceptors', () => {
  it('still answers a success with its body', async () => {
    answer = { status: 200, body: JSON.stringify({ setup: 'ready' }), type: 'application/json' };

    expect(await api.getStatus()).toEqual({ setup: 'ready' });
  });

  for (const row of bodies as Body[]) {
    it(`${row.status} ${row.code} reaches the dashboard with its code, arguments and trace id`, async () => {
      serverSays(row.status, row);

      const error = await refused(() => api.refreshReport('2026-07'));

      expect(error.status).toBe(row.status);
      expect(error.code).toBe(row.code);
      expect(error.args).toEqual(row.args);
      expect(error.traceId).toBe(row.traceId);
    });
  }

  it('covers every code the client has words for, so a code the server cannot send is not what is tested', async () => {
    const sent = new Set((bodies as Body[]).map((row) => row.code));

    for (const code of ['periodInvalid', 'periodOutOfRange', 'dateInvalid', 'dateOutOfRange', 'alreadyRunning', 'requestInvalid', 'notConfigured', 'keyUnreadable', 'keyInvalid', 'apiDisabled', 'noAccess', 'propertyNotFound', 'quota', 'unknown', 'ga4PropertyInvalid', 'searchConsoleInvalid']) {
      expect(sent.has(code), code).toBe(true);
    }
  });

  it('LOSES the code of a body without a type: the interceptor replaces it (this was the server before the type was set)', async () => {
    // The body the server wrote before ApiProblems.Create set `type`: everything but the type.
    const { type: _type, ...withoutType } = (bodies as Body[]).find((row) => row.code === 'noAccess')!;
    serverSays(502, withoutType);

    const error = await refused(() => api.refreshReport('2026-07'));

    expect(error.status).toBe(502);
    expect(error.code).toBe('unknown'); // the generic problem the interceptor wrote has no code
    expect(error.traceId).toBeUndefined();
  });

  it('LOSES the code of a body whose detail is null: Umbraco accepts text or nothing there', async () => {
    serverSays(502, { ...(bodies as Body[]).find((row) => row.code === 'quota')!, detail: null });

    expect((await refused(() => api.refreshReport('2026-07'))).code).toBe('unknown');
  });

  it('reads a 403 from the section policy (an empty body) as unknown with its status, and a 404 the same way', async () => {
    answer = { status: 403, body: '', type: 'text/plain' };
    const forbidden = await refused(() => api.getSettings());

    expect(forbidden.status).toBe(403);
    expect(forbidden.code).toBe('unknown');

    answer = { status: 404, body: '', type: 'text/plain' };
    expect((await refused(() => api.getStatus())).status).toBe(404);
  });

  it('reads a gateway answer (an HTML 502 page) as unknown with its status', async () => {
    // The interceptor logs that it could not read the body as JSON; that is expected here and not output to keep.
    const logged = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    answer = { status: 502, body: '<html>Bad gateway</html>', type: 'text/html' };

    const error = await refused(() => api.getStatus());

    expect(error.status).toBe(502);
    expect(error.code).toBe('unknown');
    expect(logged).toHaveBeenCalledTimes(1);
  });

  it('reads a failed request with no answer as status 0 and unknown', async () => {
    client.setConfig({ fetch: async () => Promise.reject(new TypeError('Failed to fetch')) });

    const error = await refused(() => api.getStatus());

    expect(error.status).toBe(0);
    expect(error.code).toBe('unknown');
  });
});
