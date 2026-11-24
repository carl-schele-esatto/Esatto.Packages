import { describe, expect, it } from 'vitest';
import {
  acceptsDraft,
  controlOf,
  formOf,
  groupOptions,
  keyModeOf,
  responseOf,
  sameForm,
  settingsOf,
  testRequestOf,
  testRows,
} from './settings-view';
import { FAKE_KEY, FAKE_KEY_MARKER, GROUPS, NO_KEY, failed, keyStatus, notTested, passed, settings, settingsResponse, testResult } from './testing/settings-fixtures';
import { enHost } from './testing/host';
import type { SettingsResponse } from './types';

const host = enHost();

describe('the form and the settings', () => {
  it('shows the saved settings: the Search Console property as its choice and text, the brand terms one per line', () => {
    expect(formOf(settings())).toEqual({
      ga4PropertyId: 'properties/123456789',
      searchConsoleKind: 'domain',
      searchConsoleText: 'example.com',
      brandTerms: 'acme\nacme widgets',
      displayName: 'Example',
      findingsUserGroups: ['editor'],
      autoFetch: true,
    });
  });

  it('starts empty fields as empty text, never as "null"', () => {
    const form = formOf(settings({ ga4PropertyId: null, searchConsoleSite: null, displayName: null, brandTerms: [], findingsUserGroups: [] }));

    expect(form).toMatchObject({ ga4PropertyId: '', searchConsoleKind: 'domain', searchConsoleText: '', brandTerms: '', displayName: '' });
  });

  it('sends what the form holds: the IDs trimmed (the server normalizes them), the property composed, the terms one per line', () => {
    const form = { ...formOf(settings()), ga4PropertyId: ' 987654321 ', searchConsoleKind: 'prefix' as const, searchConsoleText: 'https://www.example.com', brandTerms: ' acme \n\n widgets ', displayName: '  ' };

    expect(settingsOf(form, settings(), [])).toEqual({
      ga4PropertyId: '987654321',
      searchConsoleSite: 'https://www.example.com/',
      brandTerms: ['acme', 'widgets'],
      displayName: null,
      findingsUserGroups: ['editor'],
      autoFetch: true,
    });
  });

  it('sends null for an ID that was emptied: the field is cleared', () => {
    const form = { ...formOf(settings()), ga4PropertyId: '', searchConsoleText: '' };

    expect(settingsOf(form, settings(), [])).toMatchObject({ ga4PropertyId: null, searchConsoleSite: null });
  });

  it('sends the saved value of a field configuration locks, whatever the form holds', () => {
    const saved = settings({ ga4PropertyId: 'properties/111', autoFetch: false, findingsUserGroups: [] });
    const form = { ...formOf(saved), ga4PropertyId: '222', autoFetch: true, findingsUserGroups: ['admin'], searchConsoleText: 'other.example' };

    const sent = settingsOf(form, saved, ['ga4PropertyId', 'autoFetch', 'findingsUserGroups', 'searchConsoleSite']);

    expect(sent).toMatchObject({ ga4PropertyId: 'properties/111', autoFetch: false, findingsUserGroups: [], searchConsoleSite: 'sc-domain:example.com' });
  });

  it('compares two forms by what they say', () => {
    const form = formOf(settings());

    expect(sameForm(form, formOf(settings()))).toBe(true);
    expect(sameForm(form, { ...form, autoFetch: false })).toBe(false);
    expect(sameForm(form, { ...form, findingsUserGroups: ['editor', 'admin'] })).toBe(false);
  });
});

describe('controlOf', () => {
  it('makes a field configuration locks read-only and says why, matching the name in any case', () => {
    expect(controlOf(['ga4PropertyId'], 'ga4PropertyId', false)).toEqual({ locked: true, readonly: true });
    expect(controlOf(['GA4PropertyID'], 'ga4PropertyId', false)).toEqual({ locked: true, readonly: true });
    expect(controlOf(['ga4PropertyId'], 'displayName', false)).toEqual({ locked: false, readonly: false });
  });

  it('makes every field read-only while a save is on its way, so what is typed meanwhile is not replaced by what the server kept', () => {
    expect(controlOf([], 'displayName', true)).toEqual({ locked: false, readonly: true });
  });
});

describe('testRequestOf', () => {
  it('omits the key when the box is empty, so the saved or configured key is tested', () => {
    const request = testRequestOf(formOf(settings()), settings(), [], null);

    expect(request).toEqual({ ga4PropertyId: 'properties/123456789', searchConsoleSite: 'sc-domain:example.com' });
    expect('serviceAccountJson' in request).toBe(false);
  });

  it('omits a key that is only white space', () => {
    expect('serviceAccountJson' in testRequestOf(formOf(settings()), settings(), [], '  \n')).toBe(false);
  });

  it('carries a key that was entered, and the values of the form as they would be saved', () => {
    const form = { ...formOf(settings()), ga4PropertyId: '987654321', searchConsoleKind: 'prefix' as const, searchConsoleText: 'https://www.example.com' };

    expect(testRequestOf(form, settings(), [], FAKE_KEY)).toEqual({
      ga4PropertyId: '987654321',
      searchConsoleSite: 'https://www.example.com/',
      serviceAccountJson: FAKE_KEY,
    });
  });
});

describe('the key', () => {
  it('is read-only "set in configuration" when configuration supplies it, and a problem to hand to the developer when that one is broken', () => {
    expect(keyModeOf(keyStatus({ source: 'configuration' }), false)).toBe('configured');
    expect(keyModeOf(keyStatus({ source: 'configuration', hasKey: false, serviceAccountEmail: null, unreadable: true }), false)).toBe('configuredBroken');
  });

  it('is "saved" with Replace and Remove when it was saved here, and takes a new key once Replace is chosen', () => {
    expect(keyModeOf(keyStatus(), false)).toBe('saved');
    expect(keyModeOf(keyStatus(), true)).toBe('replacing');
  });

  it('asks for the key again when the saved one cannot be read, and for a key when there is none', () => {
    expect(keyModeOf(keyStatus({ hasKey: false, serviceAccountEmail: null, unreadable: true }), false)).toBe('unreadable');
    expect(keyModeOf(NO_KEY, false)).toBe('none');
  });

  it('takes a pasted key only where there is a box to paste it in', () => {
    expect((['none', 'replacing', 'unreadable'] as const).every(acceptsDraft)).toBe(true);
    expect((['saved', 'configured', 'configuredBroken'] as const).some(acceptsDraft)).toBe(false);
  });
});

describe('responseOf', () => {
  it('keeps only the fields the tab knows, so nothing else a response carried is held', () => {
    const echoed = { ...settingsResponse(), serviceAccountJson: FAKE_KEY, key: { ...keyStatus(), privateKey: FAKE_KEY } } as unknown as SettingsResponse;

    const kept = responseOf(echoed);

    expect(JSON.stringify(kept)).not.toContain(FAKE_KEY_MARKER);
    expect(kept).toEqual(settingsResponse());
  });

  it('copes with lists the server left out', () => {
    const sparse = { settings: { ...settings(), brandTerms: null, findingsUserGroups: undefined }, locked: null, key: NO_KEY } as unknown as SettingsResponse;

    expect(responseOf(sparse)).toMatchObject({ locked: [], settings: { brandTerms: [], findingsUserGroups: [] } });
  });
});

describe('groupOptions', () => {
  it('lists the user groups by name, with the saved ones checked', () => {
    expect(groupOptions(GROUPS, ['editor'])).toEqual([
      { alias: 'admin', name: 'Administrators', selected: false, known: true },
      { alias: 'editor', name: 'Editors', selected: true, known: true },
      { alias: 'writer', name: 'Writers', selected: false, known: true },
    ]);
  });

  it('matches a saved alias in any case, and keeps one that no group has any more, so a save does not drop it unseen', () => {
    const options = groupOptions(GROUPS, ['EDITOR', 'retired']);

    expect(options.find((option) => option.alias === 'editor')?.selected).toBe(true);
    expect(options[options.length - 1]).toEqual({ alias: 'retired', name: 'retired', selected: true, known: false });
  });
});

describe('testRows', () => {
  it('has a row for the key, Google Analytics and Search Console, each with an icon and a word for how it went', () => {
    const rows = testRows(host, testResult(), null);

    expect(rows.map((row) => row.id)).toEqual(['key', 'ga4', 'searchConsole']);
    expect(rows.map((row) => row.label)).toEqual(['Service account key', 'Google Analytics', 'Search Console']);
    expect(rows.every((row) => row.kind === 'passed' && row.icon === 'icon-check' && row.status !== '')).toBe(true);
  });

  it('calls a source without data connected, as information and not as a failure', () => {
    const [, ga4] = testRows(host, testResult({ ga4: { ok: true, errorCode: 'noData', args: { source: 'ga4' } } }), null);

    expect(ga4).toMatchObject({ kind: 'noData', tone: 'notice', icon: 'icon-info', title: 'GA4 is connected, but has no data yet' });
  });

  it('says a source was not tested when the key could not be used, and why on the key row', () => {
    const rows = testRows(host, testResult({ serviceAccountEmail: null, key: failed('keyInvalid'), ga4: notTested, searchConsole: notTested }), null);

    expect(rows[0]).toMatchObject({ kind: 'failed', tone: 'critical', title: 'The service account key is not valid' });
    expect(rows[1]).toMatchObject({ kind: 'notTested', tone: 'none', status: 'Not tested' });
    expect(rows[2].kind).toBe('notTested');
  });

  it('writes a failure with its code\'s words and the service account address to add, from the test or else from the saved key', () => {
    const noAccess = failed('noAccess', { source: 'searchConsole' });

    const fromTest = testRows(host, testResult({ searchConsole: noAccess }), 'saved@proj.iam.gserviceaccount.com')[2];
    const fromSaved = testRows(host, testResult({ serviceAccountEmail: null, searchConsole: noAccess }), 'saved@proj.iam.gserviceaccount.com')[2];

    expect(fromTest).toMatchObject({ kind: 'failed', title: 'No access to Search Console' });
    expect(fromTest.text).toContain('add svc@proj.iam.gserviceaccount.com as a user');
    expect(fromSaved.text).toContain('add saved@proj.iam.gserviceaccount.com as a user');
  });

  it('shows a code the client has no words for as "unknown", never as the code itself', () => {
    const [, ga4] = testRows(host, testResult({ ga4: failed('somethingNew') }), null);

    expect(ga4.title).toBe('Something went wrong');
  });

  it('keeps passed apart from the others by more than colour: each kind has its own icon and status word', () => {
    const rows = testRows(host, testResult({ ga4: { ok: true, errorCode: 'noData', args: null }, searchConsole: failed('quota'), key: passed }), null);
    const notTestedRow = testRows(host, testResult({ key: failed('keyInvalid'), ga4: notTested }), null)[1];

    const all = [...rows, notTestedRow];
    expect(new Set(all.map((row) => row.icon)).size).toBe(4);
    expect(new Set(all.map((row) => row.status)).size).toBe(4);
  });
});
