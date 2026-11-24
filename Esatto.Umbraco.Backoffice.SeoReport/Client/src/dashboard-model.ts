// What the dashboard shell reads, does and remembers, without any DOM: the element only draws this and passes clicks to it.
//
// OPENING THE DASHBOARD NEVER FETCHES FROM GOOGLE. load() reads the status and the newest archived report and nothing else. What goes to
// Google is a person's click: refresh() (one month) and fetchNow() (the missing months, in the background, followed by polling).
//
// THE ELEMENT MAY BE GONE WHILE A REQUEST IS ON ITS WAY. Every awaited call is checked on return (`#current`): it is dropped, and touches no
// state and starts nothing, when the element has disconnected or a newer load has replaced it. The poller is disposed on disconnect, so a
// status or an answer to Fetch now that arrives afterwards cannot bring it back, and a model that is connected again makes a new one.
//
// EVERY FRESH STATUS GOES THROUGH #setStatus, which starts the poller when the status says a run is going (and only while connected).

import { toApiError, type ApiError } from './api-error';
import { FetchPoller, type FetchPollerOptions } from './fetch-poller';
import type { FetchStatus, Report, ReportResponse, StartFetchResponse, StatusResponse } from './types';
import { runOutcomeOf, selectedPeriod, shouldAcceptPeriodChange } from './view-state';

/** The calls the model makes: the functions of api.ts, or stand-ins in a test. */
export interface DashboardApi {
  getStatus(): Promise<StatusResponse>;
  getReport(period?: string): Promise<ReportResponse>;
  refreshReport(period: string): Promise<ReportResponse>;
  startFetch(): Promise<StartFetchResponse>;
  getFetchProgress(): Promise<FetchStatus>;
}

/** How the last backfill ended, for the notice that follows it. */
export interface Outcome {
  kind: ReturnType<typeof runOutcomeOf>;
  done: number;
  errorCode: string | null;
}

export interface DashboardState {
  status: StatusResponse | null;
  report: Report | null;
  note: string | null;
  fetchedUtc: string | null;
  /** The month of the report that is shown, "YYYY-MM"; empty until a report has been read. */
  period: string;
  /** The month being read, while it is read: the picker stands on it meanwhile. */
  pendingPeriod: string | null;
  /**
   * Counts the changes of the archive this model has seen: a month that was refreshed from Google, and the end of a backfill. A tab that is worked
   * out from many months (the analysis) reads again when it changes: the month it shows can be the very same one, so the month does not say.
   */
  archiveRevision: number;

  loading: boolean;
  /** The report of another month is being read. */
  switching: boolean;
  /** "Refresh" is fetching the selected month from Google. */
  refreshing: boolean;
  /** "Fetch now" is asking the server to start. */
  starting: boolean;

  problem: ApiError | null;
  /** The report could not be read and there is none to show instead: the error state. */
  readFailed: boolean;
  outcome: Outcome | null;
  /** Following the backfill failed too many times in a row. */
  lost: boolean;
}

const INITIAL: DashboardState = {
  status: null,
  report: null,
  note: null,
  fetchedUtc: null,
  period: '',
  pendingPeriod: null,
  archiveRevision: 0,
  loading: true,
  switching: false,
  refreshing: false,
  starting: false,
  problem: null,
  readFailed: false,
  outcome: null,
  lost: false,
};

type PollOptions = Pick<FetchPollerOptions, 'intervalMs' | 'maxIntervalMs' | 'maxFailures'>;

export class DashboardModel {
  readonly #api: DashboardApi;
  readonly #changed: () => void;
  readonly #poll: PollOptions;

  #state: DashboardState = INITIAL;
  #poller: FetchPoller | null = null;
  #connected = false;
  // Bumped by connect, disconnect and load: an awaited call that started under an earlier one is dropped when it returns.
  #epoch = 0;
  // Bumped by every read of a report and by disconnect: only the newest read is used.
  #reads = 0;
  // What "Try again" does: the action that failed.
  #retry: (() => void) | null = null;
  // The newest month has been read during the run that is going (see #setFetch): once per run.
  #firstMonthRead = false;

  constructor(api: DashboardApi, changed: () => void, poll: PollOptions = {}) {
    this.#api = api;
    this.#changed = changed;
    this.#poll = poll;
  }

  get state(): Readonly<DashboardState> {
    return this.#state;
  }

  // ---- Lifetime ------------------------------------------------------------------------------------------------------------------

  /** The element is on the page: starts reading. Resolves when the first read is over. */
  connect(): Promise<void> {
    this.#poller?.dispose();
    this.#connected = true;
    this.#poller = new FetchPoller({
      load: () => this.#api.getFetchProgress(),
      onProgress: (fetch) => this.#setFetch(fetch),
      onFinished: (fetch) => void this.#finished(fetch),
      onGiveUp: () => this.#patch({ lost: true }),
      ...this.#poll,
    });
    return this.load();
  }

  /** The element has left the page: nothing that is on its way may touch anything, and nothing may keep polling. */
  disconnect(): void {
    this.#connected = false;
    this.#epoch++;
    this.#reads++;
    this.#poller?.dispose();
    this.#poller = null;
  }

  #current(epoch: number): boolean {
    return this.#connected && epoch === this.#epoch;
  }

  // ---- Reading ---------------------------------------------------------------------------------------------------------------------

  /** Reads the status, and the newest report when the package is set up and has months. Replaces any load that is on its way. */
  async load(): Promise<void> {
    const epoch = ++this.#epoch;
    this.#firstMonthRead = false;
    this.#patch({ loading: true, problem: null, readFailed: false, switching: false, refreshing: false, starting: false, pendingPeriod: null });

    try {
      const status = await this.#api.getStatus();
      if (!this.#current(epoch)) return;

      this.#setStatus(status);
      if (status.setup === 'ready' && status.periods.length > 0) await this.#read(undefined, epoch);
      else this.#apply({ report: null, note: null, fetchedUtc: null, period: null });
    } catch (error) {
      if (this.#current(epoch)) this.#failRead(error, () => void this.load());
    } finally {
      if (this.#current(epoch)) this.#patch({ loading: false });
    }
  }

  // Reads one month's report (none: the newest) and shows it. Throws what the read threw. An answer that a newer read has replaced, or that
  // arrives after the element left, is dropped.
  async #read(period: string | undefined, epoch: number, archiveChanged = false): Promise<void> {
    const read = ++this.#reads;
    this.#patch({ switching: true });

    try {
      const response = await this.#api.getReport(period);
      if (read === this.#reads && this.#current(epoch)) this.#apply(response, archiveChanged);
    } finally {
      if (read === this.#reads && this.#current(epoch)) this.#patch({ switching: false });
    }
  }

  // `archiveChanged`: the response is the month read again after the archive changed (a refresh, the end of a backfill), as opposed to a month
  // that was merely chosen.
  #apply(response: ReportResponse, archiveChanged = false): void {
    this.#patch({
      report: response.report,
      note: response.note,
      fetchedUtc: response.fetchedUtc,
      period: response.period ?? '',
      readFailed: false,
      ...(archiveChanged ? { archiveRevision: this.#state.archiveRevision + 1 } : {}),
    });
  }

  /** Shows the report of another month. Ignored while something else is going on and for a month that is not archived. */
  async selectPeriod(period: string): Promise<void> {
    const state = this.#state;
    const busy = state.loading || state.switching || state.refreshing;
    if (!shouldAcceptPeriodChange(busy, state.pendingPeriod ?? state.period, period, state.status?.periods ?? [])) return;

    const epoch = this.#epoch;
    this.#patch({ problem: null, pendingPeriod: period });

    try {
      await this.#read(period, epoch);
    } catch (error) {
      if (this.#current(epoch)) this.#failRead(error, () => void this.selectPeriod(period));
    } finally {
      if (this.#current(epoch)) this.#patch({ pendingPeriod: null });
    }
  }

  // ---- Acting: a person's click ---------------------------------------------------------------------------------------------------------

  /** Fetches the selected month from Google (about fifteen seconds), shows it, and reads the status again. */
  async refresh(): Promise<void> {
    const state = this.#state;
    const period = selectedPeriod(state.period, state.status?.periods ?? []);
    if (state.refreshing || state.switching || !period) return;

    const epoch = this.#epoch;
    this.#patch({ refreshing: true, problem: null, outcome: null });

    try {
      const response = await this.#api.refreshReport(period);
      if (!this.#current(epoch)) return;
      this.#apply(response, true);

      const status = await this.#api.getStatus();
      if (!this.#current(epoch)) return;
      this.#setStatus(status);
    } catch (error) {
      if (this.#current(epoch)) this.#fail(error, () => void this.refresh());
    } finally {
      if (this.#current(epoch)) this.#patch({ refreshing: false });
    }
  }

  /** Starts the backfill of the missing months and follows it. */
  async fetchNow(): Promise<void> {
    if (this.#state.starting) return;

    const epoch = this.#epoch;
    this.#patch({ starting: true, problem: null, outcome: null, lost: false });

    try {
      const started = await this.#api.startFetch();
      if (!this.#current(epoch)) return;
      await this.#follow(started.progress);
    } catch (error) {
      if (!this.#current(epoch)) return;

      const problem = toApiError(error);
      // Someone else started it (or another server did): it is going, so follow it instead of reporting a failure.
      if (problem.code === 'alreadyRunning' && this.#state.status) this.#followRunning();
      else this.#fail(problem, () => void this.fetchNow());
    } finally {
      if (this.#current(epoch)) this.#patch({ starting: false });
    }
  }

  // Right after the 202 the run is "starting" (running, no month counted): show it and poll, never read it as "nothing to fetch". A run
  // that is over already (it had nothing to do) is finished at once.
  async #follow(progress: FetchStatus): Promise<void> {
    const status = this.#state.status;
    if (!status) return;

    if (progress.running) this.#setStatus({ ...status, fetch: progress });
    else await this.#finished(progress);
  }

  // A run is going that this model did not see start (a 409 alreadyRunning): mark it running, and the poller reads how it goes.
  #followRunning(): void {
    const status = this.#state.status;
    if (status) this.#setStatus({ ...status, fetch: { ...status.fetch, running: true } });
  }

  /**
   * Follows a run another part of the dashboard started (the Settings tab's Fetch now) with the one poller, as fetchNow follows its own: `progress`
   * is what the start answered, null when the start found a run going already.
   */
  async followRun(progress: FetchStatus | null): Promise<void> {
    if (!this.#connected) return;

    this.#patch({ outcome: null, lost: false });
    if (progress) await this.#follow(progress);
    else this.#followRunning();
  }

  /**
   * Reads the status again WITHOUT the loading screen, which would take the open tab, and what is being typed in it, off the page: the Settings
   * tab saved settings (the setup may be ready now, the display name may have changed, and the save may have started a run, which the poller then
   * follows). The newest report is read when none is shown yet and the archive has months; a report that is shown stays.
   */
  async reloadStatus(): Promise<void> {
    if (!this.#connected) return;

    const epoch = this.#epoch;
    this.#patch({ problem: null });

    try {
      const status = await this.#api.getStatus();
      if (!this.#current(epoch)) return;

      this.#setStatus(status);
      if (status.setup === 'ready' && status.periods.length > 0 && this.#state.report === null) await this.#read(undefined, epoch);
    } catch (error) {
      if (this.#current(epoch)) this.#failRead(error, () => void this.reloadStatus());
    }
  }

  /** Follows the backfill again after contact was lost. */
  followAgain(): void {
    this.#patch({ lost: false });
    this.#poller?.start();
  }

  dismissOutcome(): void {
    this.#patch({ outcome: null });
  }

  /** Repeats what failed. */
  retry(): void {
    this.#retry?.();
  }

  /**
   * A note was saved (the Analysis tab does that): keeps the note this model holds in step, so that a tab that is opened again shows what was
   * saved and not what the report was read with. `note` is what the server kept: trimmed, or null when it was removed.
   *
   * ONLY THE SHOWN MONTH'S NOTE IS HELD. A note for another month (the month was changed while the save was on its way) is ignored: the server has
   * it, and the next read of that month brings it. After the element has left nothing is touched.
   */
  setNote(period: string, note: string | null): void {
    if (!this.#connected || period !== this.#state.period) return;
    this.#patch({ note });
  }

  // ---- The status and the run ----------------------------------------------------------------------------------------------------------------

  // The ONE place a status read from the server is stored: it starts the poller when the status says a run is going. A status that could be
  // read ends the error state of a read that failed before it: what follows (a report read, or none for an empty archive) decides again, so
  // a read that is mended can never leave the error state behind with no problem in it.
  #setStatus(status: StatusResponse): void {
    this.#patch({ status, readFailed: false });
    if (status.fetch.running && this.#connected) this.#poller?.start();
  }

  // The progress of the run that is being followed: only the run's part of the status changes, and the poller is already going. Once the run
  // has archived its first month (the newest: a backfill goes newest first) and no report is shown, that month is read, once, so it shows under
  // the progress card while the older months load.
  #setFetch(fetch: FetchStatus): void {
    const status = this.#state.status;
    if (status) this.#patch({ status: { ...status, fetch } });
    if (fetch.running && fetch.done >= 1 && this.#state.report === null && !this.#firstMonthRead) void this.#readFirstMonth();
  }

  // One read of the newest month while the run goes on. Its month joins the months the picker offers (the status is read again when the run
  // ends). A failure is shown beside the progress card and is not the error state: the run is still going, and its end reads everything again.
  async #readFirstMonth(): Promise<void> {
    this.#firstMonthRead = true;
    const epoch = this.#epoch;

    try {
      await this.#read(undefined, epoch, true);
      const { status, period } = this.#state;
      if (this.#current(epoch) && status && period && !status.periods.includes(period)) {
        this.#patch({ status: { ...status, periods: [...status.periods, period].sort() } });
      }
    } catch (error) {
      if (this.#current(epoch)) this.#fail(error, () => void this.#readFirstMonth());
    }
  }

  async #finished(fetch: FetchStatus): Promise<void> {
    this.#firstMonthRead = false;
    this.#setFetch(fetch);
    this.#patch({ outcome: { kind: runOutcomeOf(fetch), done: fetch.done, errorCode: fetch.errorCode } });
    await this.#readArchiveAgain();
  }

  // The archive has grown: read the status and the selected month again (none: the newest), WITHOUT the loading screen, and so does Try again
  // after it failed (load() would take the open tab off the page, and with it what is being typed on the Settings tab).
  async #readArchiveAgain(): Promise<void> {
    const epoch = this.#epoch;

    try {
      const status = await this.#api.getStatus();
      if (!this.#current(epoch)) return;

      this.#setStatus(status);
      if (status.setup === 'ready' && status.periods.length > 0) await this.#read(this.#state.period || undefined, epoch, true);

      // Everything shown has been read again and nothing failed, so a problem left from the way (the early read of the newest month while the
      // run went on) is out of date: it must not stay above a report that was read.
      if (this.#current(epoch)) this.#patch({ problem: null });
    } catch (error) {
      if (this.#current(epoch)) this.#failRead(error, () => void this.#retryArchive());
    }
  }

  async #retryArchive(): Promise<void> {
    this.#patch({ problem: null });
    await this.#readArchiveAgain();
  }

  // ---- Failure ---------------------------------------------------------------------------------------------------------------------------------

  #fail(error: unknown, retry: () => void): void {
    this.#retry = retry;
    this.#patch({ problem: toApiError(error) });
  }

  // A failed read: with no report to show instead it is the error state, never "no report".
  #failRead(error: unknown, retry: () => void): void {
    this.#fail(error, retry);
    this.#patch({ readFailed: this.#state.report === null });
  }

  #patch(patch: Partial<DashboardState>): void {
    this.#state = { ...this.#state, ...patch };
    this.#changed();
  }
}
