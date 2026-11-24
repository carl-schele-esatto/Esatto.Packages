// Synthetic settings, key statuses and connection tests for the Settings tab's tests. Nothing here is real: the key is an obviously fake one
// (a PEM header around a marker, no key material), the property IDs and addresses are the documentation examples.

import type { ConnectionTestResult, KeyStatus, SeoReportSettings, SettingsResponse, SourceTest, UserGroupItem } from '../types';

/** A string that only the fake key contains: a test that finds it anywhere outside the key box has found a leak. */
export const FAKE_KEY_MARKER = 'FAKE-KEY-MARKER-7c1e';

const FAKE_PEM = `-----BEGIN PRIVATE KEY-----\n${FAKE_KEY_MARKER}-not-a-real-key\n-----END PRIVATE KEY-----\n`;

type KeyFields = Partial<Record<'type' | 'project_id' | 'client_email' | 'private_key', string | undefined>>;

/** The JSON of a fake service-account key, shaped as Google Cloud writes it; a field set to undefined is left out. */
export function fakeKey(overrides: KeyFields = {}): string {
  const fields: KeyFields = {
    type: 'service_account',
    project_id: 'proj',
    private_key: FAKE_PEM,
    client_email: 'svc@proj.iam.gserviceaccount.com',
    ...overrides,
  };
  return JSON.stringify(fields, null, 2);
}

export const FAKE_KEY = fakeKey();

export function settings(overrides: Partial<SeoReportSettings> = {}): SeoReportSettings {
  return {
    ga4PropertyId: 'properties/123456789',
    searchConsoleSite: 'sc-domain:example.com',
    brandTerms: ['acme', 'acme widgets'],
    displayName: 'Example',
    findingsUserGroups: ['editor'],
    autoFetch: true,
    ...overrides,
  };
}

export function keyStatus(overrides: Partial<KeyStatus> = {}): KeyStatus {
  return { hasKey: true, source: 'database', serviceAccountEmail: 'svc@proj.iam.gserviceaccount.com', unreadable: false, ...overrides };
}

export const NO_KEY: KeyStatus = { hasKey: false, source: 'none', serviceAccountEmail: null, unreadable: false };

export function settingsResponse(overrides: Partial<SettingsResponse> = {}): SettingsResponse {
  return { settings: settings(), locked: [], key: keyStatus(), ...overrides };
}

export const passed: SourceTest = { ok: true, errorCode: null, args: null };
export const notTested: SourceTest = { ok: false, errorCode: null, args: null };

export function failed(errorCode: string, args: SourceTest['args'] = null): SourceTest {
  return { ok: false, errorCode, args };
}

export function testResult(overrides: Partial<ConnectionTestResult> = {}): ConnectionTestResult {
  return { serviceAccountEmail: 'svc@proj.iam.gserviceaccount.com', key: passed, ga4: passed, searchConsole: passed, ...overrides };
}

export const GROUPS: UserGroupItem[] = [
  { alias: 'admin', name: 'Administrators' },
  { alias: 'editor', name: 'Editors' },
  { alias: 'writer', name: 'Writers' },
];
