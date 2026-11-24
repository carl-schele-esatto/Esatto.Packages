import { describe, expect, it } from 'vitest';
import { ApiError } from './api-error';
import { fetchStatus } from './testing/fixtures';
import { deferred, expectNoKeyIn, ready, watchConsole } from './testing/settings-harness';
import { FAKE_KEY, NO_KEY, failed, settings, settingsResponse, testResult } from './testing/settings-fixtures';
import type { ConnectionTestResult, SettingsResponse, StartFetchResponse } from './types';

// The Settings tab's actions without a browser: how Save, Remove, Test connection and Fetch now go, and what a late answer does. Where the key
// goes is in settings-model.test.ts.

watchConsole();

describe('Save', () => {
  it('sends the form and the key, rebuilds the form from what the server kept, empties the key box and tells the shell', async () => {
    const { api, model, events } = await ready(settingsResponse({ key: NO_KEY }));
    model.edit({ ga4PropertyId: ' 987654321 ', searchConsoleKind: 'prefix', searchConsoleText: 'https://www.example.com' });
    model.setDraftKey(FAKE_KEY);
    api.putSettings.mockResolvedValueOnce(
      settingsResponse({ settings: settings({ ga4PropertyId: 'properties/987654321', searchConsoleSite: 'https://www.example.com/' }) }),
    );
    const revision = model.state.draftRevision;

    await model.save();

    expect(api.putSettings).toHaveBeenCalledWith({
      settings: expect.objectContaining({ ga4PropertyId: '987654321', searchConsoleSite: 'https://www.example.com/' }),
      serviceAccountJson: FAKE_KEY,
      removeKey: false,
    });
    expect(model.state.form).toMatchObject({ ga4PropertyId: 'properties/987654321', searchConsoleKind: 'prefix', searchConsoleText: 'https://www.example.com/' });
    expect(model.keyMode).toBe('saved');
    expect(model.state.save).toEqual({ busy: false, done: true, problem: null });
    expect(model.state.hasDraftKey).toBe(false);
    expect(model.holdsDraftKey).toBe(false);
    expect(model.state.draftRevision).toBe(revision + 1);
    expect(model.dirty).toBe(false);
    expect(events.saved).toHaveBeenCalledTimes(1);
  });

  it('sends no key when the box is empty: the key stays as it is', async () => {
    const { api, model } = await ready();

    await model.save();

    expect(api.putSettings.mock.calls[0][0]).toMatchObject({ serviceAccountJson: null, removeKey: false });
  });

  it('is saving while the answer is on its way, and ignores a second Save and every edit meanwhile', async () => {
    const { api, model } = await ready(settingsResponse({ key: NO_KEY }));
    const answer = deferred<SettingsResponse>();
    api.putSettings.mockReturnValueOnce(answer.promise);

    const saving = model.save();
    expect(model.state.save.busy).toBe(true);
    await model.save();
    model.edit({ displayName: 'typed meanwhile' });
    model.setDraftKey(FAKE_KEY);

    answer.resolve(settingsResponse());
    await saving;

    expect(api.putSettings).toHaveBeenCalledTimes(1);
    expect(model.state.form?.displayName).toBe('Example');
    expect(model.holdsDraftKey).toBe(false);
  });

  it('keeps everything that was typed, the key included, when the save fails, and Try again sends it again', async () => {
    const { api, model, events } = await ready(settingsResponse({ key: NO_KEY }));
    model.edit({ displayName: 'Changed' });
    model.setDraftKey(FAKE_KEY);
    api.putSettings.mockRejectedValueOnce(new ApiError(400, 'keyInvalid'));

    await model.save();

    expect(model.state.save).toMatchObject({ busy: false, done: false });
    expect(model.state.save.problem?.code).toBe('keyInvalid');
    expect(model.state.form?.displayName).toBe('Changed');
    expect(model.holdsDraftKey).toBe(true);
    expect(events.saved).not.toHaveBeenCalled();

    await model.save();
    expect(api.putSettings).toHaveBeenCalledTimes(2);
    expect(api.putSettings.mock.calls[1][0].serviceAccountJson).toBe(FAKE_KEY);
    expect(model.holdsDraftKey).toBe(false);
  });

  it('says Saved no more once the form is edited again', async () => {
    const { model } = await ready();
    await model.save();

    model.edit({ autoFetch: false });

    expect(model.state.save.done).toBe(false);
  });

  it('drops an answer that arrives after the element left, forgets the key at once when it leaves, and still tells the shell it was saved', async () => {
    const { api, model, events, changed } = await ready(settingsResponse({ key: NO_KEY }));
    model.setDraftKey(FAKE_KEY);
    const answer = deferred<SettingsResponse>();
    api.putSettings.mockReturnValueOnce(answer.promise);

    const saving = model.save();
    model.disconnect();
    expect(model.holdsDraftKey).toBe(false);
    const before = changed.mock.calls.length;
    answer.resolve(settingsResponse());
    await saving;

    // The server has the new settings: the shell must read the status again, or it keeps saying "not connected". Nothing is drawn.
    expect(events.saved).toHaveBeenCalledTimes(1);
    expect(events.saved).toHaveBeenCalledWith();
    expect(model.state.form).toBeNull();
    expect(changed.mock.calls.length).toBe(before);
  });

  it('says nothing to the shell about a save that failed after the element left', async () => {
    const { api, model, events } = await ready();
    const answer = deferred<SettingsResponse>();
    api.putSettings.mockReturnValueOnce(answer.promise);

    const saving = model.save();
    model.disconnect();
    answer.reject(new ApiError(500, 'unknown'));
    await saving;

    expect(events.saved).not.toHaveBeenCalled();
  });
});

describe('Remove', () => {
  it('asks first, then removes the saved key only: the settings saved stay, and so does what is being typed', async () => {
    const { api, model, events } = await ready();
    model.edit({ displayName: 'Typed, not saved' });

    model.askRemove();
    expect(model.state.confirmingRemove).toBe(true);
    await model.removeKey();

    expect(api.putSettings).toHaveBeenCalledWith({ settings: settings(), serviceAccountJson: null, removeKey: true });
    expect(model.keyMode).toBe('none');
    expect(model.state.confirmingRemove).toBe(false);
    expect(model.state.remove).toEqual({ busy: false, done: true, problem: null });
    expect(model.state.form?.displayName).toBe('Typed, not saved');
    expect(events.saved).toHaveBeenCalledTimes(1);
  });

  it('does nothing without the confirmation, and Cancel takes the question back', async () => {
    const { api, model } = await ready();

    await model.removeKey();
    model.askRemove();
    model.cancelRemove();
    await model.removeKey();

    expect(api.putSettings).not.toHaveBeenCalled();
    expect(model.state.confirmingRemove).toBe(false);
  });

  it('still tells the shell when a removal went through after the element left', async () => {
    const { api, model, events } = await ready();
    const answer = deferred<SettingsResponse>();
    api.putSettings.mockReturnValueOnce(answer.promise);

    model.askRemove();
    const removing = model.removeKey();
    model.disconnect();
    answer.resolve(settingsResponse({ key: NO_KEY }));
    await removing;

    expect(events.saved).toHaveBeenCalledTimes(1);
    expect(model.state.saved).toBeNull();
  });

  it('shows a removal that failed and keeps the key shown as saved', async () => {
    const { api, model } = await ready();
    api.putSettings.mockRejectedValueOnce(new ApiError(500, 'unknown'));

    model.askRemove();
    await model.removeKey();

    expect(model.state.remove.problem?.code).toBe('unknown');
    expect(model.keyMode).toBe('saved');
  });
});

describe('Test connection', () => {
  it('tests the values in the form and the saved key when the box is empty: the key is left out of the request', async () => {
    const { api, model } = await ready();
    model.edit({ ga4PropertyId: '987654321' });

    await model.test();

    const request = api.testConnection.mock.calls[0][0];
    expect(request).toEqual({ ga4PropertyId: '987654321', searchConsoleSite: 'sc-domain:example.com' });
    expect('serviceAccountJson' in request).toBe(false);
    expect(model.state.test.result).toEqual(testResult());
  });

  it('tests a key that was entered, and keeps it in the box: only a save sends it for good', async () => {
    const { api, model } = await ready(settingsResponse({ key: NO_KEY }));
    model.setDraftKey(FAKE_KEY);

    await model.test();

    expect(api.testConnection.mock.calls[0][0].serviceAccountJson).toBe(FAKE_KEY);
    expect(model.holdsDraftKey).toBe(true);
    expect(api.putSettings).not.toHaveBeenCalled();
  });

  it('is testing while the answer is on its way, forgets the result before, and ignores a second click meanwhile', async () => {
    const { api, model } = await ready();
    await model.test();
    const answer = deferred<ConnectionTestResult>();
    api.testConnection.mockReturnValueOnce(answer.promise);

    const testing = model.test();
    expect(model.state.test).toMatchObject({ busy: true, result: null });
    await model.test();
    answer.resolve(testResult({ ga4: failed('noAccess', { source: 'ga4' }) }));
    await testing;

    expect(api.testConnection).toHaveBeenCalledTimes(2);
    expect(model.state.test.result?.ga4.errorCode).toBe('noAccess');
  });

  it('shows a test that could not be run as a problem, never the reason the request gave', async () => {
    const { api, model } = await ready(settingsResponse({ key: NO_KEY }));
    model.setDraftKey(FAKE_KEY);
    api.testConnection.mockRejectedValueOnce(new Error(`network down near ${FAKE_KEY}`));

    await model.test();

    expect(model.state.test.problem?.code).toBe('unknown');
    expectNoKeyIn(JSON.stringify(model.state.test.problem));
    expectNoKeyIn(model.state.test.problem?.message ?? '');
  });

  it('drops a result that arrives after the element left', async () => {
    const { api, model } = await ready();
    const answer = deferred<ConnectionTestResult>();
    api.testConnection.mockReturnValueOnce(answer.promise);

    const testing = model.test();
    model.disconnect();
    answer.resolve(testResult());
    await testing;

    expect(model.state.test.result).toBeNull();
  });
});

// A result is about the values it was run on: once the GA4 ID, the Search Console property or the key changes, it is about nothing on the screen.
describe('a test result that no longer fits the form', () => {
  const IDLE_TEST = { busy: false, result: null, problem: null };

  it('is cleared by an edit of the GA4 ID or of the Search Console property, the kind or the text', async () => {
    for (const patch of [{ ga4PropertyId: '987654321' }, { searchConsoleKind: 'prefix' as const }, { searchConsoleText: 'other.example' }]) {
      const { model } = await ready();
      await model.test();
      expect(model.state.test.result).not.toBeNull();

      model.edit(patch);

      expect(model.state.test, JSON.stringify(patch)).toEqual(IDLE_TEST);
    }
  });

  it('is kept by an edit that changes nothing, and by the fields a connection test does not read', async () => {
    const { model } = await ready();
    await model.test();

    model.edit({ ga4PropertyId: 'properties/123456789' });
    model.edit({ brandTerms: 'acme\nbeta', displayName: 'Another', findingsUserGroups: ['admin'], autoFetch: false });

    expect(model.state.test.result).toEqual(testResult());
  });

  it('is cleared when a key is pasted, read from a file or taken out of the box', async () => {
    const { model } = await ready(settingsResponse({ key: NO_KEY }));
    await model.test();

    model.setDraftKey(FAKE_KEY);
    expect(model.state.test.result).toBeNull();

    await model.test();
    model.setDraftKey('');
    expect(model.state.test.result).toBeNull();
  });

  it('is cleared by Replace, Cancel and Discard changes', async () => {
    const { model } = await ready();

    await model.test();
    model.replaceKey();
    expect(model.state.test.result).toBeNull();

    await model.test();
    model.cancelReplace();
    expect(model.state.test.result).toBeNull();

    await model.test();
    model.edit({ displayName: 'Changed' });
    model.revert();
    expect(model.state.test.result).toBeNull();
  });

  it('is cleared by a save and a removal that succeeded, but not by one that failed', async () => {
    const { api, model } = await ready();

    await model.test();
    api.putSettings.mockRejectedValueOnce(new ApiError(500, 'unknown'));
    await model.save();
    expect(model.state.test.result).not.toBeNull();

    await model.save();
    expect(model.state.test.result).toBeNull();

    await model.test();
    model.askRemove();
    await model.removeKey();
    expect(model.state.test.result).toBeNull();
  });

  it('after a removal, shows no service account at all: neither the removed key\'s nor the last test\'s', async () => {
    const { model } = await ready();
    await model.test();

    model.askRemove();
    await model.removeKey();

    expect(model.state.test.result?.serviceAccountEmail ?? model.state.saved?.key.serviceAccountEmail ?? null).toBeNull();
  });

  it('drops the answer of a test whose values were edited while it ran, and ends the test at once', async () => {
    const { api, model } = await ready();
    const answer = deferred<ConnectionTestResult>();
    api.testConnection.mockReturnValueOnce(answer.promise);

    const testing = model.test();
    model.edit({ searchConsoleText: 'other.example' });
    expect(model.state.test).toEqual(IDLE_TEST);

    answer.resolve(testResult({ ga4: failed('noAccess', { source: 'ga4' }) }));
    await testing;

    expect(model.state.test).toEqual(IDLE_TEST);
  });

  it('lets the edited values be tested at once, and keeps only that answer', async () => {
    const { api, model } = await ready();
    const first = deferred<ConnectionTestResult>();
    api.testConnection.mockReturnValueOnce(first.promise);

    const old = model.test();
    model.edit({ ga4PropertyId: '987654321' });
    await model.test();
    first.resolve(testResult({ ga4: failed('noAccess', { source: 'ga4' }) }));
    await old;

    expect(api.testConnection).toHaveBeenCalledTimes(2);
    expect(api.testConnection.mock.calls[1][0].ga4PropertyId).toBe('987654321');
    expect(model.state.test.result).toEqual(testResult());
  });
});

describe('Fetch now', () => {
  it('starts the backfill and hands the run to the shell, which follows it: there is no second poller here', async () => {
    const { api, model, events } = await ready();
    const progress = fetchStatus({ running: true });
    api.startFetch.mockResolvedValueOnce({ result: 'started', progress });

    await model.fetchNow();

    expect(events.fetchStarted).toHaveBeenCalledWith(progress);
    expect(model.state.fetch).toEqual({ busy: false, alreadyRunning: false, problem: null });
  });

  it('calls a run that is already going information, not a failure, and has the shell follow it', async () => {
    const { api, model, events } = await ready();
    api.startFetch.mockRejectedValueOnce(new ApiError(409, 'alreadyRunning'));

    await model.fetchNow();

    expect(model.state.fetch).toEqual({ busy: false, alreadyRunning: true, problem: null });
    expect(events.fetchStarted).toHaveBeenCalledWith(null);
  });

  it('shows a refusal because nothing is set up, or the key cannot be read, as a problem', async () => {
    for (const code of ['notConfigured', 'keyUnreadable'] as const) {
      const { api, model, events } = await ready();
      api.startFetch.mockRejectedValueOnce(new ApiError(409, code));

      await model.fetchNow();

      expect(model.state.fetch.problem?.code).toBe(code);
      expect(events.fetchStarted).not.toHaveBeenCalled();
    }
  });

  it('ignores a second click while the start is on its way, and an answer after the element left', async () => {
    const { api, model, events } = await ready();
    const answer = deferred<StartFetchResponse>();
    api.startFetch.mockReturnValueOnce(answer.promise);

    const starting = model.fetchNow();
    await model.fetchNow();
    model.disconnect();
    answer.resolve({ result: 'started', progress: fetchStatus({ running: true }) });
    await starting;

    expect(api.startFetch).toHaveBeenCalledTimes(1);
    expect(events.fetchStarted).not.toHaveBeenCalled();
  });
});
