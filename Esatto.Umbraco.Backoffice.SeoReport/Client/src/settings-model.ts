// What the Settings tab reads, does and remembers, without any DOM: the element only draws this and passes clicks to it.
//
// THE KEY IS WRITE-ONLY. The service-account key a person pastes (or reads from a file) is held in ONE place, the private field #draftKey, and
// nowhere else: not in the state (which shows only that there is one, whose it looks like, and what is wrong with it: hasDraftKey, draftEmail,
// draftProblem), not in an event, an error, a log or any storage. It leaves in exactly two requests: PUT settings (to be saved) and POST
// settings/test (to be tested; a test does not save it, so the box keeps it). It is forgotten as soon as a save or a removal has succeeded, on
// Cancel and Discard changes, and when the element leaves; a save that FAILS keeps it, so the person can fix what was wrong and send it again. No
// response contains it (the server never sends it back), and responseOf copies only the fields the tab knows, so not even a response that did
// could put it into the state. `draftRevision` counts the times the model forgot it: the key field empties its box when it changes.
//
// THE ELEMENT MAY BE GONE WHILE A REQUEST IS ON ITS WAY. Every awaited call is checked on return (`#epoch`) and dropped when the element has
// disconnected since; only a save or a removal the server ACCEPTED still calls `saved` (with nothing), so the shell's status does not go stale.
// Each action runs once at a time: a second Save, Test connection or Fetch now while one is on its way is ignored (the buttons stay focusable and
// say they are busy). Fetch now hands the run to the shell, which follows it with its one poller; nothing here polls.
//
// A TEST RESULT IS ABOUT THE VALUES IT RAN ON. Any change of what a test reads (the GA4 ID, the Search Console property, the key: an edit, a paste,
// Replace, Cancel, Discard changes, a save or a removal that succeeded) clears the result shown and ends a test on its way, whose answer is then
// dropped (`#connection`). So the result, and the service account it names, always belong to what is on the screen.
//
// THE FORM IS REBUILT ONLY FROM THE SERVER'S ANSWER TO A SAVE (and on Discard changes): a status the shell reads again does not touch it, so what
// is being typed is never lost to a refresh. While a save is on its way every field is read-only and edits are ignored, because the answer
// replaces the form.

import { toApiError, type ApiError } from './api-error';
import { checkKey, type KeyProblem } from './key-check';
import { acceptsDraft, FIELD_OF, formOf, isLocked, keyModeOf, responseOf, sameForm, settingsOf, testRequestOf, type KeyMode, type SettingsForm } from './settings-view';
import type {
  ConnectionTestRequest,
  ConnectionTestResult,
  FetchStatus,
  PutSettingsRequest,
  SettingsResponse,
  StartFetchResponse,
  UserGroupItem,
} from './types';

/** The calls the model makes: the functions of api.ts, or stand-ins in a test. */
export interface SettingsApi {
  getSettings(): Promise<SettingsResponse>;
  putSettings(body: PutSettingsRequest): Promise<SettingsResponse>;
  testConnection(body: ConnectionTestRequest): Promise<ConnectionTestResult>;
  startFetch(): Promise<StartFetchResponse>;
  getUserGroups(): Promise<UserGroupItem[]>;
}

/** What the element tells the shell. Neither carries anything of the key. */
export interface SettingsEvents {
  /**
   * The settings (or the key) were saved: the setup, the display name and a run the save started are the status's to say. Called for a save or a
   * removal that the server accepted even when the tab has left meanwhile (so the shell must not hear it through the tab's events), never with
   * anything of what was saved.
   */
  saved(): void;
  /** Fetch now started a run: follow it. Null: one was running already (409), so follow whatever is going. */
  fetchStarted(progress: FetchStatus | null): void;
}

/** The event the element dispatches for fetchStarted, and what it carries. (saved is a callback the shell hands the element: see SettingsEvents.) */
export const FETCH_STARTED_EVENT = 'fetch-started';

export interface FetchStartedDetail {
  progress: FetchStatus | null;
}

/** How an action that writes went. */
export interface ActionState {
  /** On its way: the button stays focusable (aria-disabled) and ignores clicks. */
  busy: boolean;
  /** It succeeded and nothing has been edited since: the polite announcement. */
  done: boolean;
  problem: ApiError | null;
}

/** How the last Test connection went: on its way, its result, or why it could not be run. */
export interface TestState {
  busy: boolean;
  result: ConnectionTestResult | null;
  problem: ApiError | null;
}

/** How the last Fetch now went. A run that was going already is information (`alreadyRunning`), not a problem. */
export interface FetchNowState {
  busy: boolean;
  alreadyRunning: boolean;
  problem: ApiError | null;
}

export interface SettingsModelState {
  /** The settings are being read. */
  loading: boolean;
  /** The settings could not be read: the tab shows only this, with Try again. */
  loadProblem: ApiError | null;
  /** What the server last said (the settings in force, the locked fields, the key status; never the key). */
  saved: SettingsResponse | null;
  /** What the form holds; null until the settings have been read. */
  form: SettingsForm | null;

  groups: UserGroupItem[] | null;
  groupsLoading: boolean;
  groupsProblem: ApiError | null;

  /** Replace was chosen: the key field takes a new key while the saved one stays. */
  replacing: boolean;
  /** Remove was chosen and waits for its confirmation. */
  confirmingRemove: boolean;
  /** A key was entered (the text itself is not here). */
  hasDraftKey: boolean;
  /** The service account the entered key looks like it belongs to. */
  draftEmail: string | null;
  /** Why the entered key cannot be a key, as far as the browser can tell. A warning: the server decides. */
  draftProblem: KeyProblem | null;
  /** Counts the times the entered key was forgotten: the key field empties its box when it changes. */
  draftRevision: number;

  save: ActionState;
  remove: ActionState;
  test: TestState;
  fetch: FetchNowState;
}

const IDLE: ActionState = { busy: false, done: false, problem: null };
const IDLE_TEST: TestState = { busy: false, result: null, problem: null };

// The values of the form a connection test reads (with the key, which is not in the form).
const CONNECTION_FIELDS: readonly (keyof SettingsForm)[] = ['ga4PropertyId', 'searchConsoleKind', 'searchConsoleText'];

const INITIAL: SettingsModelState = {
  loading: false,
  loadProblem: null,
  saved: null,
  form: null,
  groups: null,
  groupsLoading: false,
  groupsProblem: null,
  replacing: false,
  confirmingRemove: false,
  hasDraftKey: false,
  draftEmail: null,
  draftProblem: null,
  draftRevision: 0,
  save: IDLE,
  remove: IDLE,
  test: IDLE_TEST,
  fetch: { busy: false, alreadyRunning: false, problem: null },
};

const NO_DRAFT = { hasDraftKey: false, draftEmail: null, draftProblem: null } as const;

export class SettingsModel {
  readonly #api: SettingsApi;
  readonly #changed: () => void;
  readonly #events: SettingsEvents;

  #state: SettingsModelState = INITIAL;
  #connected = false;
  // Bumped by connect and disconnect: an awaited call that started under an earlier one is dropped when it returns.
  #epoch = 0;
  // THE ONLY PLACE THE ENTERED KEY IS HELD. See the file's header.
  #draftKey: string | null = null;
  // Counts the changes of what a connection test reads (the GA4 ID, the Search Console property, the key): a result belongs to the count it was
  // started under, and is dropped (or cleared) once the count has moved on. See #connectionChanged.
  #connection = 0;

  constructor(api: SettingsApi, changed: () => void, events: SettingsEvents) {
    this.#api = api;
    this.#changed = changed;
    this.#events = events;
  }

  get state(): Readonly<SettingsModelState> {
    return this.#state;
  }

  /** What the key field shows (settings-view.ts, keyModeOf); `none` until the settings have been read. */
  get keyMode(): KeyMode {
    const saved = this.#state.saved;
    return saved ? keyModeOf(saved.key, this.#state.replacing) : 'none';
  }

  /** The form differs from what is saved, or a key was entered. */
  get dirty(): boolean {
    const { saved, form, hasDraftKey } = this.#state;
    return hasDraftKey || (saved !== null && form !== null && !sameForm(form, formOf(saved.settings)));
  }

  /** Whether an entered key is held at all: what the tests check after the key should have been forgotten. Never the key itself. */
  get holdsDraftKey(): boolean {
    return this.#draftKey !== null;
  }

  /**
   * Whether the person is in the middle of something here: an action on its way, unsaved changes or an entered key, Replace or a removal waiting
   * for its confirmation. The tab tells the shell, which then keeps the tab open whatever the status does next.
   */
  get engaged(): boolean {
    const { save, remove, test, fetch, confirmingRemove, replacing } = this.#state;
    return save.busy || remove.busy || test.busy || fetch.busy || confirmingRemove || replacing || this.dirty;
  }

  // ---- Lifetime ------------------------------------------------------------------------------------------------------------------

  /** The element is on the page: reads the settings and the user groups, unless they are held already. Resolves when both are read. */
  async connect(): Promise<void> {
    const reconnected = this.#connected;
    this.#connected = true;
    if (!reconnected) this.#epoch++;
    if (this.#state.saved !== null || this.#state.loading) return;

    await Promise.all([this.#load(), this.#loadGroups()]);
  }

  /** The element has left the page: the key is forgotten, nothing that is on its way may touch anything, and nothing it held is kept. */
  disconnect(): void {
    this.#connected = false;
    this.#epoch++;
    this.#draftKey = null;
    // Not announced: the element is gone.
    this.#state = INITIAL;
  }

  #current(epoch: number): boolean {
    return this.#connected && epoch === this.#epoch;
  }

  // ---- Reading -------------------------------------------------------------------------------------------------------------------

  async #load(): Promise<void> {
    const epoch = this.#epoch;
    this.#patch({ loading: true, loadProblem: null });

    try {
      const response = responseOf(await this.#api.getSettings());
      if (this.#current(epoch)) this.#patch({ loading: false, saved: response, form: formOf(response.settings) });
    } catch (error) {
      if (this.#current(epoch)) this.#patch({ loading: false, loadProblem: toApiError(error) });
    }
  }

  async #loadGroups(): Promise<void> {
    const epoch = this.#epoch;
    this.#patch({ groupsLoading: true, groupsProblem: null });

    try {
      const groups = await this.#api.getUserGroups();
      if (this.#current(epoch)) this.#patch({ groupsLoading: false, groups: Array.isArray(groups) ? groups : [] });
    } catch (error) {
      if (this.#current(epoch)) this.#patch({ groupsLoading: false, groupsProblem: toApiError(error) });
    }
  }

  /** "Try again" after the settings could not be read. */
  async retryLoad(): Promise<void> {
    if (this.#connected && this.#state.loadProblem) await this.#load();
  }

  /** "Try again" after the user groups could not be read. */
  async retryGroups(): Promise<void> {
    if (this.#connected && this.#state.groupsProblem) await this.#loadGroups();
  }

  // ---- Editing -------------------------------------------------------------------------------------------------------------------

  #writing(): boolean {
    return this.#state.save.busy || this.#state.remove.busy;
  }

  /** Changes values of the form. A value of a field configuration locks is ignored, and so is every edit while a save is on its way. */
  edit(patch: Partial<SettingsForm>): void {
    const { form, saved } = this.#state;
    if (!form || !saved || this.#writing()) return;

    const allowed = Object.fromEntries(
      Object.entries(patch).filter(([name]) => {
        const field = FIELD_OF[name as keyof SettingsForm];
        return field !== undefined && !isLocked(saved.locked, field);
      }),
    ) as Partial<SettingsForm>;
    if (Object.keys(allowed).length === 0) return;

    const next = { ...form, ...allowed };
    const connection = CONNECTION_FIELDS.some((name) => next[name] !== form[name]) ? this.#connectionChanged() : {};
    this.#patch({ form: next, save: { ...this.#state.save, done: false }, ...connection });
  }

  /**
   * What a connection test reads has changed: a result shown is about values that are no longer on the screen, and a test on its way will answer
   * for them. The result is cleared, a test on its way is ended (its answer is dropped when it comes: see test()), and the edited values can be
   * tested at once.
   */
  #connectionChanged(): Partial<SettingsModelState> {
    this.#connection++;
    return { test: IDLE_TEST };
  }

  /**
   * What is in the key box (typed, pasted or read from a file). Held privately; the state says only that there is one, whose it looks like and
   * what is wrong with it. Ignored where the field has no box (a saved key not being replaced, a configured key) and while a save is on its way.
   */
  setDraftKey(text: string): void {
    if (!this.#state.saved || this.#writing() || !acceptsDraft(this.keyMode)) return;

    if (text.trim().length === 0) {
      this.#draftKey = null;
      this.#patch({ ...NO_DRAFT, ...this.#connectionChanged() });
      return;
    }

    this.#draftKey = text;
    const check = checkKey(text);
    this.#patch({
      hasDraftKey: true,
      draftEmail: check.email,
      draftProblem: check.problem,
      save: { ...this.#state.save, done: false },
      ...this.#connectionChanged(),
    });
  }

  // Forgets the entered key, and has the key field empty its box. The key a test would use changes with it.
  #forgetDraft(): Partial<SettingsModelState> {
    this.#draftKey = null;
    return { ...NO_DRAFT, draftRevision: this.#state.draftRevision + 1, ...this.#connectionChanged() };
  }

  /** Replace: the box opens beside the saved key, which stays until a new one is saved. */
  replaceKey(): void {
    if (this.keyMode === 'saved' && !this.#writing()) this.#patch({ replacing: true, confirmingRemove: false, ...this.#connectionChanged() });
  }

  /** Cancel after Replace: what was entered is forgotten. */
  cancelReplace(): void {
    if (this.#state.replacing && !this.#writing()) this.#patch({ replacing: false, ...this.#forgetDraft() });
  }

  /** Discard changes: the form as it is saved, the key box empty. */
  revert(): void {
    const saved = this.#state.saved;
    if (!saved || this.#writing()) return;

    this.#patch({
      form: formOf(saved.settings),
      replacing: false,
      confirmingRemove: false,
      save: IDLE,
      remove: IDLE,
      ...this.#forgetDraft(),
    });
  }

  // ---- Save -----------------------------------------------------------------------------------------------------------------------

  /**
   * Saves the form and the entered key (none: the key stays as it is). The server checks the connection with what it saved before it answers,
   * which can take up to a minute, and may start the backfill; the answer is the settings in force, and the form is rebuilt from it. A failure
   * keeps everything that was typed, the key included. "Try again" is Save again.
   */
  async save(): Promise<void> {
    const { saved, form } = this.#state;
    if (!saved || !form || this.#writing()) return;

    const epoch = this.#epoch;
    const body: PutSettingsRequest = { settings: settingsOf(form, saved.settings, saved.locked), serviceAccountJson: this.#draftKey, removeKey: false };
    this.#patch({ save: { busy: true, done: false, problem: null }, remove: IDLE });

    try {
      const response = responseOf(await this.#api.putSettings(body));
      // The server has the settings even when the tab has left: the shell is told, so its status does not go stale. Nothing here is touched.
      if (!this.#current(epoch)) return this.#events.saved();

      this.#patch({
        saved: response,
        form: formOf(response.settings),
        replacing: false,
        confirmingRemove: false,
        save: { busy: false, done: true, problem: null },
        ...this.#forgetDraft(),
      });
      this.#events.saved();
    } catch (error) {
      if (this.#current(epoch)) this.#patch({ save: { busy: false, done: false, problem: toApiError(error) } });
    }
  }

  // ---- Remove ---------------------------------------------------------------------------------------------------------------------

  /** Remove: asks for a confirmation first. Only a key saved here can be removed. */
  askRemove(): void {
    if (this.#state.saved?.key.source === 'database' && !this.#writing()) this.#patch({ confirmingRemove: true, replacing: false, remove: IDLE });
  }

  cancelRemove(): void {
    if (this.#state.confirmingRemove) this.#patch({ confirmingRemove: false });
  }

  /**
   * Removes the saved key, once it is confirmed. The settings are sent as they are SAVED, not as the form holds them: Remove removes the key and
   * nothing else, and what is being typed stays in the form, unsaved.
   */
  async removeKey(): Promise<void> {
    const saved = this.#state.saved;
    if (!saved || !this.#state.confirmingRemove || this.#writing()) return;

    const epoch = this.#epoch;
    this.#patch({ remove: { busy: true, done: false, problem: null }, save: { ...this.#state.save, done: false } });

    try {
      const response = responseOf(await this.#api.putSettings({ settings: saved.settings, serviceAccountJson: null, removeKey: true }));
      if (!this.#current(epoch)) return this.#events.saved();

      this.#patch({ saved: response, replacing: false, confirmingRemove: false, remove: { busy: false, done: true, problem: null }, ...this.#forgetDraft() });
      this.#events.saved();
    } catch (error) {
      if (this.#current(epoch)) this.#patch({ remove: { busy: false, done: false, problem: toApiError(error) } });
    }
  }

  // ---- Test connection ------------------------------------------------------------------------------------------------------------

  /**
   * Tests the values in the form, before they are saved: the entered key when there is one (it stays in the box), the key in force when the box is
   * empty. The result before is forgotten while the next is on its way, so its arrival is news. An answer for values that have changed since the
   * test started (the GA4 ID, the Search Console property, the key: #connectionChanged) is about nothing on the screen and is dropped.
   */
  async test(): Promise<void> {
    const { saved, form } = this.#state;
    if (!saved || !form || this.#state.test.busy) return;

    const epoch = this.#epoch;
    const connection = this.#connection;
    const current = () => this.#current(epoch) && connection === this.#connection;
    const request = testRequestOf(form, saved.settings, saved.locked, this.#draftKey);
    this.#patch({ test: { busy: true, result: null, problem: null } });

    try {
      const result = await this.#api.testConnection(request);
      if (current()) this.#patch({ test: { busy: false, result, problem: null } });
    } catch (error) {
      if (current()) this.#patch({ test: { busy: false, result: null, problem: toApiError(error) } });
    }
  }

  // ---- Fetch now ------------------------------------------------------------------------------------------------------------------

  /**
   * Starts the backfill of the missing months and hands the run to the shell, which follows it. A run that is going already (409 alreadyRunning) is
   * information, and the shell follows that one; any other refusal (nothing set up, a key that cannot be read) is a problem.
   */
  async fetchNow(): Promise<void> {
    if (!this.#state.saved || this.#state.fetch.busy) return;

    const epoch = this.#epoch;
    this.#patch({ fetch: { busy: true, alreadyRunning: false, problem: null } });

    try {
      const started = await this.#api.startFetch();
      if (!this.#current(epoch)) return;

      this.#patch({ fetch: { busy: false, alreadyRunning: false, problem: null } });
      this.#events.fetchStarted(started.progress);
    } catch (error) {
      if (!this.#current(epoch)) return;

      const problem = toApiError(error);
      if (problem.code === 'alreadyRunning') {
        this.#patch({ fetch: { busy: false, alreadyRunning: true, problem: null } });
        this.#events.fetchStarted(null);
      } else {
        this.#patch({ fetch: { busy: false, alreadyRunning: false, problem } });
      }
    }
  }

  // ---- Keeping the state ----------------------------------------------------------------------------------------------------------

  #patch(patch: Partial<SettingsModelState>): void {
    this.#state = { ...this.#state, ...patch };
    this.#changed();
  }
}
