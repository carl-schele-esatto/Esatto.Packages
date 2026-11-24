import { describe, expect, it } from 'vitest';
import { ApiError } from './api-error';
import { deferred, everythingVisible, expectNoKeyIn, ready, setup, watchConsole } from './testing/settings-harness';
import { FAKE_KEY, FAKE_KEY_MARKER, GROUPS, NO_KEY, fakeKey, keyStatus, settingsResponse } from './testing/settings-fixtures';
import type { SettingsResponse } from './types';

// The Settings tab's model without a browser: what it reads, how the form and the key box take what is typed, and above all where the key
// goes: only out, in the request that saves it (or tests it), and nowhere else, ever. Save, Remove, Test connection and Fetch now are in
// settings-model.actions.test.ts.

watchConsole();

describe('reading', () => {
  it('reads the settings and the user groups, and nothing else', async () => {
    const { api, model } = await ready();

    expect(api.getSettings).toHaveBeenCalledTimes(1);
    expect(api.getUserGroups).toHaveBeenCalledTimes(1);
    expect(api.putSettings).not.toHaveBeenCalled();
    expect(api.testConnection).not.toHaveBeenCalled();
    expect(api.startFetch).not.toHaveBeenCalled();
    expect(model.state.loading).toBe(false);
    expect(model.state.form?.searchConsoleText).toBe('example.com');
    expect(model.state.groups).toEqual(GROUPS);
    expect(model.dirty).toBe(false);
  });

  it('shows a read that failed, and Try again reads again', async () => {
    const { api, model } = setup();
    api.getSettings.mockRejectedValueOnce(new ApiError(500, 'unknown', undefined, 'trace-1'));

    await model.connect();
    expect(model.state.loadProblem?.traceId).toBe('trace-1');
    expect(model.state.form).toBeNull();

    await model.retryLoad();
    expect(model.state.loadProblem).toBeNull();
    expect(model.state.form).not.toBeNull();
  });

  it('keeps the form when only the user groups cannot be read, and reads them again on Try again', async () => {
    const { api, model } = setup();
    api.getUserGroups.mockRejectedValueOnce(new ApiError(502, 'unknown'));

    await model.connect();
    expect(model.state.form).not.toBeNull();
    expect(model.state.groupsProblem?.status).toBe(502);

    await model.retryGroups();
    expect(model.state.groups).toEqual(GROUPS);
    expect(model.state.groupsProblem).toBeNull();
  });

  it('drops an answer that arrives after the element left', async () => {
    const { api, model, changed } = setup();
    const slow = deferred<SettingsResponse>();
    api.getSettings.mockReturnValueOnce(slow.promise);

    const connecting = model.connect();
    model.disconnect();
    const before = changed.mock.calls.length;
    slow.resolve(settingsResponse());
    await connecting;

    expect(model.state.form).toBeNull();
    expect(changed.mock.calls.length).toBe(before);
  });
});

describe('editing', () => {
  it('keeps what is typed, and says the form differs from what is saved', async () => {
    const { model } = await ready();

    model.edit({ displayName: 'Another name' });

    expect(model.state.form?.displayName).toBe('Another name');
    expect(model.dirty).toBe(true);
  });

  it('ignores an edit of a field configuration locks: it is read-only, and the save sends the value in force', async () => {
    const { api, model } = await ready(settingsResponse({ locked: ['ga4PropertyId', 'searchConsoleSite', 'autoFetch'] }));

    model.edit({ ga4PropertyId: '987654321', searchConsoleKind: 'prefix', autoFetch: false, displayName: 'Changed' });
    await model.save();

    expect(model.state.form?.ga4PropertyId).toBe('properties/123456789');
    const sent = api.putSettings.mock.calls[0][0].settings;
    expect(sent).toMatchObject({ ga4PropertyId: 'properties/123456789', searchConsoleSite: 'sc-domain:example.com', autoFetch: true, displayName: 'Changed' });
  });

  it('is not reset when nothing new was read: only a save, a removal or Discard changes rebuild the form', async () => {
    const { model } = await ready();
    model.edit({ brandTerms: 'acme\nbeta' });

    model.connect(); // the element was moved: connected again while it was connected
    await Promise.resolve();

    expect(model.state.form?.brandTerms).toBe('acme\nbeta');
  });

  it('Discard changes puts back what is saved and empties the key box', async () => {
    const { model } = await ready(settingsResponse({ key: NO_KEY }));
    model.edit({ displayName: 'Changed' });
    model.setDraftKey(FAKE_KEY);
    const revision = model.state.draftRevision;

    model.revert();

    expect(model.state.form?.displayName).toBe('Example');
    expect(model.state.hasDraftKey).toBe(false);
    expect(model.holdsDraftKey).toBe(false);
    expect(model.state.draftRevision).toBe(revision + 1);
    expect(model.dirty).toBe(false);
  });
});

// The tab tells the shell when the person is in the middle of something, so that the shell keeps the tab open whatever the status does next.
describe('engaged', () => {
  it('is not engaged when the settings are only being looked at', async () => {
    const { model } = await ready();

    expect(model.engaged).toBe(false);
  });

  it('is engaged by unsaved changes, an entered key, Replace and a removal waiting for its confirmation', async () => {
    const edited = await ready();
    edited.model.edit({ displayName: 'Changed' });
    expect(edited.model.engaged).toBe(true);

    const keyed = await ready(settingsResponse({ key: NO_KEY }));
    keyed.model.setDraftKey(FAKE_KEY);
    expect(keyed.model.engaged).toBe(true);

    const replacing = await ready();
    replacing.model.replaceKey();
    expect(replacing.model.engaged).toBe(true);

    const removing = await ready();
    removing.model.askRemove();
    expect(removing.model.engaged).toBe(true);
  });

  it('is engaged while a save, a test or Fetch now is on its way', async () => {
    for (const act of ['save', 'test', 'fetchNow'] as const) {
      const { api, model } = await ready();
      api.putSettings.mockReturnValueOnce(deferred<SettingsResponse>().promise);
      api.testConnection.mockReturnValueOnce(deferred<never>().promise);
      api.startFetch.mockReturnValueOnce(deferred<never>().promise);

      void model[act]();

      expect(model.engaged, act).toBe(true);
    }
  });
});

describe('the key box', () => {
  it('says whose key was pasted, and that a paste that cannot be a key cannot, without holding any of it in what it shows', async () => {
    const { model } = await ready(settingsResponse({ key: NO_KEY }));

    model.setDraftKey(FAKE_KEY);
    expect(model.state).toMatchObject({ hasDraftKey: true, draftEmail: 'svc@proj.iam.gserviceaccount.com', draftProblem: null });
    expect(model.dirty).toBe(true);
    expectNoKeyIn(everythingVisible(model));

    model.setDraftKey(fakeKey({ type: 'authorized_user' }));
    expect(model.state).toMatchObject({ hasDraftKey: true, draftEmail: null, draftProblem: 'notServiceAccount' });

    model.setDraftKey('');
    expect(model.state).toMatchObject({ hasDraftKey: false, draftEmail: null, draftProblem: null });
    expect(model.holdsDraftKey).toBe(false);
  });

  it('takes no key while a saved one is shown, until Replace is chosen; Cancel forgets what was pasted', async () => {
    const { model } = await ready();

    model.setDraftKey(FAKE_KEY);
    expect(model.holdsDraftKey).toBe(false);

    model.replaceKey();
    expect(model.keyMode).toBe('replacing');
    model.setDraftKey(FAKE_KEY);
    expect(model.holdsDraftKey).toBe(true);

    const revision = model.state.draftRevision;
    model.cancelReplace();
    expect(model.keyMode).toBe('saved');
    expect(model.holdsDraftKey).toBe(false);
    expect(model.state.draftRevision).toBe(revision + 1);
  });

  it('takes no key when configuration supplies it', async () => {
    const { model } = await ready(settingsResponse({ key: keyStatus({ source: 'configuration' }) }));

    model.replaceKey();
    model.setDraftKey(FAKE_KEY);

    expect(model.keyMode).toBe('configured');
    expect(model.holdsDraftKey).toBe(false);
  });

  it('takes the key again when the saved one cannot be read', async () => {
    const { model } = await ready(settingsResponse({ key: keyStatus({ hasKey: false, serviceAccountEmail: null, unreadable: true }) }));

    model.setDraftKey(FAKE_KEY);

    expect(model.keyMode).toBe('unreadable');
    expect(model.holdsDraftKey).toBe(true);
  });
});

// The key is write-only: it is sent when it is saved (or tested) and is then gone from everything the tab holds.
describe('where the key goes', () => {
  /** Every argument every call and every event got, as text. */
  function argumentsOf(parts: Awaited<ReturnType<typeof ready>>) {
    const { api, events, changed } = parts;
    const calls = (mock: { mock: { calls: unknown[][] } }) => JSON.stringify(mock.mock.calls);
    return {
      putSettings: calls(api.putSettings),
      testConnection: calls(api.testConnection),
      others: [api.getSettings, api.getUserGroups, api.startFetch, events.saved, events.fetchStarted, changed].map(calls).join(),
    };
  }

  it('reaches putSettings and nothing else when it is saved, and is in nothing the tab holds afterwards', async () => {
    const parts = await ready(settingsResponse({ key: NO_KEY }));
    const { model } = parts;

    model.setDraftKey(FAKE_KEY);
    await model.save();
    await model.fetchNow();

    const sent = argumentsOf(parts);
    expect(sent.putSettings).toContain(FAKE_KEY_MARKER);
    expect(sent.testConnection).toBe('[]');
    expectNoKeyIn(sent.others);
    expectNoKeyIn(everythingVisible(model));
    expect(model.holdsDraftKey).toBe(false);
  });

  it('is not taken back from a response that (wrongly) carried it', async () => {
    const parts = await ready(settingsResponse({ key: NO_KEY }));
    const { api, model } = parts;
    api.putSettings.mockResolvedValueOnce({ ...settingsResponse(), serviceAccountJson: FAKE_KEY, key: { ...keyStatus(), json: FAKE_KEY } } as unknown as SettingsResponse);

    model.setDraftKey(FAKE_KEY);
    await model.save();

    expectNoKeyIn(everythingVisible(model));
  });

  it('is in nothing the tab holds after a removal, after Discard changes, after a failed save and after the element left', async () => {
    const parts = await ready(settingsResponse({ key: keyStatus({ hasKey: false, serviceAccountEmail: null, unreadable: true }) }));
    const { api, model } = parts;

    model.setDraftKey(FAKE_KEY);
    api.putSettings.mockRejectedValueOnce(new Error(`refused ${FAKE_KEY}`));
    await model.save();
    expectNoKeyIn(everythingVisible(model)); // the key stays in the box (and in the model's private field), never in what is shown
    expect(model.holdsDraftKey).toBe(true);

    model.askRemove();
    await model.removeKey();
    expect(model.holdsDraftKey).toBe(false);
    expectNoKeyIn(everythingVisible(model));

    model.setDraftKey(FAKE_KEY);
    model.revert();
    expect(model.holdsDraftKey).toBe(false);

    model.setDraftKey(FAKE_KEY);
    model.disconnect();
    expect(model.holdsDraftKey).toBe(false);
    expectNoKeyIn(everythingVisible(model));
    expectNoKeyIn(argumentsOf(parts).others);
  });

  it('reaches testConnection when a key that was entered is tested, and no other call', async () => {
    const parts = await ready(settingsResponse({ key: NO_KEY }));

    parts.model.setDraftKey(FAKE_KEY);
    await parts.model.test();

    const sent = argumentsOf(parts);
    expect(sent.testConnection).toContain(FAKE_KEY_MARKER);
    expect(sent.putSettings).toBe('[]');
    expectNoKeyIn(sent.others);
    expectNoKeyIn(everythingVisible(parts.model));
  });
});
