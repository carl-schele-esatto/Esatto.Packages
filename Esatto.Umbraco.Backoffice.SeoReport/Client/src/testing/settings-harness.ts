// What the Settings model's tests share: a model over stand-ins for the API and the shell, a watch on the console (nothing in any flow may write
// there), and the check that the fake key is in nothing the model shows.

import { afterEach, beforeEach, expect, vi, type MockInstance } from 'vitest';
import { SettingsModel, type SettingsApi } from '../settings-model';
import type { ConnectionTestRequest, ConnectionTestResult, FetchStatus, PutSettingsRequest, SettingsResponse, StartFetchResponse, UserGroupItem } from '../types';
import { fetchStatus } from './fixtures';
import { FAKE_KEY_MARKER, GROUPS, NO_KEY, keyStatus, settingsResponse, testResult } from './settings-fixtures';

export { deferred } from './deferred';

/** A model over stand-ins. A save answers with what was sent (a key given is saved, a removal leaves none); `first` is what is read. */
export function setup(first: SettingsResponse = settingsResponse()) {
  const api = {
    getSettings: vi.fn<() => Promise<SettingsResponse>>(async () => first),
    putSettings: vi.fn<(body: PutSettingsRequest) => Promise<SettingsResponse>>(async (body) => ({
      settings: body.settings,
      locked: first.locked,
      key: body.removeKey ? NO_KEY : body.serviceAccountJson ? keyStatus() : first.key,
    })),
    testConnection: vi.fn<(body: ConnectionTestRequest) => Promise<ConnectionTestResult>>(async () => testResult()),
    startFetch: vi.fn<() => Promise<StartFetchResponse>>(async () => ({ result: 'started', progress: fetchStatus({ running: true }) })),
    getUserGroups: vi.fn<() => Promise<UserGroupItem[]>>(async () => GROUPS),
  } satisfies SettingsApi;
  const changed = vi.fn();
  const events = { saved: vi.fn<() => void>(), fetchStarted: vi.fn<(progress: FetchStatus | null) => void>() };
  const model = new SettingsModel(api, changed, events);
  return { api, model, changed, events };
}

/** A model that is connected and has read the settings and the user groups. */
export async function ready(first?: SettingsResponse) {
  const parts = setup(first);
  await parts.model.connect();
  return parts;
}

/** Watches every console method for the tests of the file that calls it, and fails a test that wrote anything there. */
export function watchConsole(): void {
  let spies: MockInstance[] = [];

  beforeEach(() => {
    spies = (['log', 'info', 'warn', 'error', 'debug', 'trace', 'dir', 'table'] as const).map((name) =>
      vi.spyOn(console, name).mockImplementation(() => {}),
    );
  });

  afterEach(() => {
    for (const spy of spies) {
      expect(spy, 'nothing is written to the console').not.toHaveBeenCalled();
      spy.mockRestore();
    }
  });
}

/**
 * Everything the model shows and every value reachable on it, as text. The private field that holds an entered key is not reachable (it is a
 * #private field); `holdsDraftKey` says whether it holds one, and the tests check that too.
 */
export function everythingVisible(model: SettingsModel): string {
  const reachable: Record<string, unknown> = { state: model.state, dirty: model.dirty, keyMode: model.keyMode };
  for (const name of Object.getOwnPropertyNames(model)) reachable[`own:${name}`] = (model as unknown as Record<string, unknown>)[name];
  return JSON.stringify(reachable);
}

export function expectNoKeyIn(said: string): void {
  expect(said).not.toContain(FAKE_KEY_MARKER);
  expect(said).not.toContain('private_key');
  expect(said).not.toContain('PRIVATE KEY');
}
