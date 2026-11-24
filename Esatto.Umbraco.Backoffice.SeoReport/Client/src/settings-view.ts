// What the Settings tab shows and sends, decided from what the server said. Pure functions, without a DOM; the model (settings-model.ts) holds the
// state and the element draws it.
//
// THE FORM HOLDS TEXT, THE SERVER HOLDS SETTINGS. The form keeps what the person types (the Search Console property as a kind and a text, the brand
// terms one per line); settingsOf turns it into what is sent, and the server normalizes and answers with what it kept, which the form is rebuilt
// from. A field that configuration sets (`locked`) is shown read-only and sent with the value it has, which the server ignores.
//
// THE KEY IS NOT HERE. Nothing in this file takes or returns the key text, except testRequestOf, which puts it in the request it builds and
// nowhere else.

import { errorTexts, text, type Localizer } from './localize';
import { composeSearchConsole, parseSearchConsole, type SearchConsoleKind } from './search-console';
import { enteredTerms, textFromTerms } from './target-terms';
import type {
  ConnectionTestRequest,
  ConnectionTestResult,
  KeySourceKind,
  KeyStatus,
  SeoReportSettings,
  SettingsResponse,
  SourceTest,
  TextArgs,
  UserGroupItem,
} from './types';

/** The settings by the camelCase names the server locks them by (SettingsResponse.locked). */
export type SettingsField = 'ga4PropertyId' | 'searchConsoleSite' | 'brandTerms' | 'displayName' | 'findingsUserGroups' | 'autoFetch';

export interface SettingsForm {
  /** As typed: 123456789 or properties/123456789. */
  ga4PropertyId: string;
  searchConsoleKind: SearchConsoleKind;
  searchConsoleText: string;
  /** One term per line. */
  brandTerms: string;
  displayName: string;
  /** Aliases. */
  findingsUserGroups: string[];
  autoFetch: boolean;
}

/** The field of the settings a value of the form belongs to: the Search Console property is two values of the form. */
export const FIELD_OF: Record<keyof SettingsForm, SettingsField> = {
  ga4PropertyId: 'ga4PropertyId',
  searchConsoleKind: 'searchConsoleSite',
  searchConsoleText: 'searchConsoleSite',
  brandTerms: 'brandTerms',
  displayName: 'displayName',
  findingsUserGroups: 'findingsUserGroups',
  autoFetch: 'autoFetch',
};

/** The form for saved settings. */
export function formOf(settings: SeoReportSettings): SettingsForm {
  const site = parseSearchConsole(settings.searchConsoleSite);
  return {
    ga4PropertyId: settings.ga4PropertyId ?? '',
    searchConsoleKind: site.kind,
    searchConsoleText: site.text,
    brandTerms: textFromTerms(settings.brandTerms),
    displayName: settings.displayName ?? '',
    findingsUserGroups: [...(settings.findingsUserGroups ?? [])],
    autoFetch: settings.autoFetch,
  };
}

/** Whether configuration sets a field. The names are compared in any case, as SEO Health does. */
export function isLocked(locked: readonly string[], field: SettingsField): boolean {
  return locked.some((name) => name.toLowerCase() === field.toLowerCase());
}

const orNull = (value: string): string | null => (value.trim() === '' ? null : value.trim());

/**
 * The settings to send for a form. A field configuration locks is sent as it is saved (`saved`); every other field from the form: the IDs trimmed
 * (the server normalizes them; an empty one is null, which clears it), the property composed (search-console.ts), the terms one per line.
 */
export function settingsOf(form: SettingsForm, saved: SeoReportSettings, locked: readonly string[]): SeoReportSettings {
  const pick = <K extends SettingsField>(field: K, fromForm: SeoReportSettings[K]): SeoReportSettings[K] =>
    isLocked(locked, field) ? saved[field] : fromForm;

  return {
    ga4PropertyId: pick('ga4PropertyId', orNull(form.ga4PropertyId)),
    searchConsoleSite: pick('searchConsoleSite', orNull(composeSearchConsole(form.searchConsoleKind, form.searchConsoleText))),
    brandTerms: pick('brandTerms', enteredTerms(form.brandTerms)),
    displayName: pick('displayName', orNull(form.displayName)),
    findingsUserGroups: pick('findingsUserGroups', [...form.findingsUserGroups]),
    autoFetch: pick('autoFetch', form.autoFetch),
  };
}

/** Whether two forms say the same. */
export function sameForm(a: SettingsForm, b: SettingsForm): boolean {
  return (
    a.ga4PropertyId === b.ga4PropertyId &&
    a.searchConsoleKind === b.searchConsoleKind &&
    a.searchConsoleText === b.searchConsoleText &&
    a.brandTerms === b.brandTerms &&
    a.displayName === b.displayName &&
    a.autoFetch === b.autoFetch &&
    a.findingsUserGroups.length === b.findingsUserGroups.length &&
    a.findingsUserGroups.every((alias, index) => alias === b.findingsUserGroups[index])
  );
}

/**
 * How a field's control is drawn: `locked` (configuration sets it: read-only, with "Set in configuration" beside it), `readonly` (it cannot be
 * changed now: locked, or a save is on its way, so that what is typed meanwhile is not replaced by what the server kept).
 */
export function controlOf(locked: readonly string[], field: SettingsField, busy: boolean): { locked: boolean; readonly: boolean } {
  const isSet = isLocked(locked, field);
  return { locked: isSet, readonly: isSet || busy };
}

/**
 * The body of Test connection: the IDs as they would be saved, and the key entered in the form. A blank key is LEFT OUT, so the server tests the
 * key in force (saved or configured).
 */
export function testRequestOf(form: SettingsForm, saved: SeoReportSettings, locked: readonly string[], draftKey: string | null): ConnectionTestRequest {
  const sent = settingsOf(form, saved, locked);
  const request: ConnectionTestRequest = { ga4PropertyId: sent.ga4PropertyId, searchConsoleSite: sent.searchConsoleSite };
  if (draftKey && draftKey.trim().length > 0) request.serviceAccountJson = draftKey;
  return request;
}

// ---- The key ---------------------------------------------------------------------------------------------------------------------

/**
 * What the key field shows:
 * - configured        configuration supplies a usable key: read-only "Set in configuration", no box
 * - configuredBroken  configuration supplies a key that cannot be used: pasting cannot mend it (configuration wins), the developer has to
 * - saved             a key saved here: "Key saved" with the service account, Replace and Remove
 * - replacing         Replace was chosen: the box and the file chooser; the saved key stays until a new one is saved
 * - unreadable        a key is saved but cannot be used here: why, and the box to enter it again
 * - none              no key: the box and the file chooser
 */
export type KeyMode = 'configured' | 'configuredBroken' | 'saved' | 'replacing' | 'unreadable' | 'none';

export function keyModeOf(key: KeyStatus, replacing: boolean): KeyMode {
  if (key.source === 'configuration') return key.unreadable ? 'configuredBroken' : 'configured';
  if (key.unreadable) return 'unreadable';
  if (key.hasKey) return replacing ? 'replacing' : 'saved';
  return 'none';
}

/** Whether the field has a box to paste a key in. */
export function acceptsDraft(mode: KeyMode): boolean {
  return mode === 'none' || mode === 'replacing' || mode === 'unreadable';
}

const SOURCES: readonly KeySourceKind[] = ['none', 'configuration', 'database'];

const strings = (value: unknown): string[] => (Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : []);
const stringOrNull = (value: unknown): string | null => (typeof value === 'string' ? value : null);

/**
 * A copy of a settings response with the fields this tab knows and nothing else, so that whatever else an answer carried is never held (the
 * server sends nothing secret; this makes sure of it). Lists the server left out are empty.
 */
export function responseOf(response: SettingsResponse): SettingsResponse {
  const settings = response.settings ?? ({} as SeoReportSettings);
  const key = response.key ?? ({} as KeyStatus);
  return {
    settings: {
      ga4PropertyId: stringOrNull(settings.ga4PropertyId),
      searchConsoleSite: stringOrNull(settings.searchConsoleSite),
      brandTerms: strings(settings.brandTerms),
      displayName: stringOrNull(settings.displayName),
      findingsUserGroups: strings(settings.findingsUserGroups),
      autoFetch: settings.autoFetch !== false,
    },
    locked: strings(response.locked),
    key: {
      hasKey: key.hasKey === true,
      source: SOURCES.includes(key.source) ? key.source : 'none',
      serviceAccountEmail: stringOrNull(key.serviceAccountEmail),
      unreadable: key.unreadable === true,
    },
  };
}

// ---- The user groups --------------------------------------------------------------------------------------------------------------

export interface GroupOption {
  alias: string;
  name: string;
  selected: boolean;
  /** False for a saved alias that no user group has any more: listed so that a save does not drop it unseen. */
  known: boolean;
}

/** The checkboxes of who sees "To fix": the user groups in the server's order, then any saved alias no group has. Aliases match in any case. */
export function groupOptions(groups: readonly UserGroupItem[], selected: readonly string[]): GroupOption[] {
  const chosen = new Set(selected.map((alias) => alias.toLowerCase()));
  const options = groups.map((group) => ({ alias: group.alias, name: group.name, selected: chosen.has(group.alias.toLowerCase()), known: true }));

  const listed = new Set(groups.map((group) => group.alias.toLowerCase()));
  const orphans = selected.filter((alias) => !listed.has(alias.toLowerCase()));
  return [...options, ...orphans.map((alias) => ({ alias, name: alias, selected: true, known: false }))];
}

// ---- Test connection --------------------------------------------------------------------------------------------------------------

/**
 * How one check went (ConnectionTester's SourceTest):
 * - passed     ok, no code
 * - noData     ok with `noData`: connected, nothing there yet. Information, not a failure
 * - notTested  not ok, no code: not checked, because the key could not be used (the key's row says why)
 * - failed     not ok with a code
 */
export type TestRowKind = 'passed' | 'noData' | 'notTested' | 'failed';

export interface TestRow {
  id: 'key' | 'ga4' | 'searchConsole';
  label: string;
  kind: TestRowKind;
  tone: 'good' | 'notice' | 'none' | 'critical';
  icon: string;
  /** The word beside the icon: how it went, never told by colour alone. */
  status: string;
  /** What happened and what to do; empty for a check that passed. */
  title: string;
  text: string;
}

const LOOK: Record<TestRowKind, { tone: TestRow['tone']; icon: string; statusKey: string }> = {
  passed: { tone: 'good', icon: 'icon-check', statusKey: 'uiTestPassedLabel' },
  noData: { tone: 'notice', icon: 'icon-info', statusKey: 'uiTestNoDataLabel' },
  notTested: { tone: 'none', icon: 'icon-block', statusKey: 'uiTestNotTestedLabel' },
  failed: { tone: 'critical', icon: 'icon-alert', statusKey: 'uiTestFailedLabel' },
};

const ROWS: readonly { id: TestRow['id']; labelKey: string }[] = [
  { id: 'key', labelKey: 'uiTestKeyLabel' },
  { id: 'ga4', labelKey: 'uiTestGa4Label' },
  { id: 'searchConsole', labelKey: 'uiTestSearchConsoleLabel' },
];

function kindOf(test: SourceTest): TestRowKind {
  if (test.ok) return test.errorCode === 'noData' ? 'noData' : 'passed';
  return test.errorCode ? 'failed' : 'notTested';
}

/**
 * The three rows of a connection test, written out. A failure's words come from its code, with the code's arguments and the service account's
 * address (`email`, which the texts that say "add this address" read): the tested key's, or else the saved key's (`savedEmail`).
 */
export function testRows(host: Localizer, result: ConnectionTestResult, savedEmail: string | null): TestRow[] {
  const email = result.serviceAccountEmail ?? savedEmail;

  return ROWS.map(({ id, labelKey }) => {
    const test = result[id];
    const kind = kindOf(test);
    const look = LOOK[kind];
    const args: TextArgs = { ...(test.args ?? {}), email };

    const words =
      kind === 'failed' || kind === 'noData'
        ? errorTexts(host, test.errorCode, args)
        : { title: kind === 'notTested' ? text(host, 'uiTestNotTestedText') : '', text: '' };

    return { id, label: text(host, labelKey), kind, tone: look.tone, icon: look.icon, status: text(host, look.statusKey), ...words };
  });
}
