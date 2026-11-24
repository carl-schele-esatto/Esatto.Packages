// What the daily traffic card reads, does and remembers, without any DOM: the element only draws this and passes clicks to it.
//
// THIS IS AN EXPLICIT READ FROM GA4, through the server's cached service (GET daily, GET daily/sources). It happens when the card is opened (the
// rolling window), when a month is chosen and when a day is chosen, and NEVER otherwise: no polling, no timer, no refresh by itself, and it
// never starts the backfill. A card that is connected again with its curve still in hand does not ask again.
//
// A request may be on its way when the element goes, and a later answer may belong to a month or a day that is no longer chosen. Every awaited
// call is checked on return and dropped when the element has left (a new connection is a new epoch) or a newer choice has replaced it, so a
// slower older answer never overwrites the one the person asked for last.

import { toApiError, type ApiError } from './api-error';
import type { DailyResponse, DailySourcesResponse } from './types';

/** The calls the model makes: the functions of api.ts, or stand-ins in a test. */
export interface DailyApi {
  getDaily(month?: string): Promise<DailyResponse>;
  getDailySources(date: string): Promise<DailySourcesResponse>;
}

export interface DailyState {
  /** The curve that is shown; null while another is read, or after a failure. */
  daily: DailyResponse | null;
  /** What the picker offers ("" is the rolling window), as last read: it survives a month change and a failed read. */
  months: string[];
  /** How many days the rolling window has, once it has been read: it names the picker's first option while another month is shown. */
  rollingDays: number | null;
  /** The month being read ("" for the rolling window), kept after a failed read so that Try again repeats it; null once a curve is shown. */
  pendingMonth: string | null;
  loading: boolean;
  /** The curve could not be read. */
  problem: ApiError | null;
  /** The day picked ("YYYY-MM-DD"), for the sources under the chart. */
  day: string | null;
  sources: DailySourcesResponse | null;
  sourcesLoading: boolean;
  /** The sources of the day could not be read: shown beside the curve, which stays. */
  sourcesProblem: ApiError | null;
}

const INITIAL: DailyState = {
  daily: null,
  months: [],
  rollingDays: null,
  pendingMonth: null,
  loading: false,
  problem: null,
  day: null,
  sources: null,
  sourcesLoading: false,
  sourcesProblem: null,
};

export class DailyModel {
  readonly #api: DailyApi;
  readonly #changed: () => void;

  #state: DailyState = INITIAL;
  #connected = false;
  // Bumped by connect and disconnect: an awaited call that started under an earlier one is dropped when it returns.
  #epoch = 0;
  // Bumped by every read of a curve (and of a day) and by disconnect: only the newest is used.
  #reads = 0;
  #dayReads = 0;
  // The month last asked for ("" is the rolling window): what Try again repeats.
  #requested = '';

  constructor(api: DailyApi, changed: () => void) {
    this.#api = api;
    this.#changed = changed;
  }

  get state(): Readonly<DailyState> {
    return this.#state;
  }

  /** The month the picker stands on: the one being read, else the one shown. */
  get selected(): string {
    return this.#state.pendingMonth ?? this.#state.daily?.window.month ?? '';
  }

  // ---- Lifetime ------------------------------------------------------------------------------------------------------------------

  /** The card is on the page: reads the curve unless it already holds one. Resolves when that read is over. */
  async connect(): Promise<void> {
    this.#connected = true;
    this.#epoch++;
    if (this.#state.daily !== null) return;

    await this.#read(this.#requested);
  }

  /** The card has left the page: nothing that is on its way may touch anything. */
  disconnect(): void {
    this.#connected = false;
    this.#epoch++;
    this.#reads++;
    this.#dayReads++;
  }

  #patch(changes: Partial<DailyState>): void {
    this.#state = { ...this.#state, ...changes };
    this.#changed();
  }

  // ---- Reading -------------------------------------------------------------------------------------------------------------------

  async #read(month: string): Promise<void> {
    const read = ++this.#reads;
    const epoch = this.#epoch;
    const current = () => this.#connected && epoch === this.#epoch && read === this.#reads;

    this.#requested = month;
    // The day that was picked belongs to the window that is being replaced.
    this.#dayReads++;
    this.#patch({
      loading: true,
      problem: null,
      daily: null,
      pendingMonth: month,
      day: null,
      sources: null,
      sourcesLoading: false,
      sourcesProblem: null,
    });

    try {
      const daily = await this.#api.getDaily(month === '' ? undefined : month);
      if (!current()) return;

      this.#patch({
        daily,
        months: daily.months?.length ? [...daily.months] : this.#state.months,
        rollingDays: daily.window?.month == null ? daily.windowDays : this.#state.rollingDays,
        pendingMonth: null,
        loading: false,
      });
    } catch (error) {
      if (current()) this.#patch({ problem: toApiError(error), loading: false });
    }
  }

  /** Shows the curve of another month ("" is the rolling window). A month the server does not offer is ignored. */
  async selectMonth(month: string): Promise<void> {
    const { months, problem } = this.#state;
    if (!this.#connected) return;
    if (month === this.selected && problem === null) return;
    if (months.length > 0 && !months.includes(month)) return;

    await this.#read(month);
  }

  /** Shows where one day's visits came from; no day clears it. */
  async selectDay(date: string | null): Promise<void> {
    if (!this.#connected) return;

    const read = ++this.#dayReads;
    const epoch = this.#epoch;
    const current = () => this.#connected && epoch === this.#epoch && read === this.#dayReads;

    if (!date) {
      this.#patch({ day: null, sources: null, sourcesLoading: false, sourcesProblem: null });
      return;
    }

    this.#patch({ day: date, sources: null, sourcesLoading: true, sourcesProblem: null });

    try {
      const sources = await this.#api.getDailySources(date);
      if (current()) this.#patch({ sources, sourcesLoading: false });
    } catch (error) {
      if (current()) this.#patch({ sourcesProblem: toApiError(error), sourcesLoading: false });
    }
  }

  /** "Try again": repeats the read that failed (the sources of the day, or else the curve). */
  async retry(): Promise<void> {
    const { sourcesProblem, problem, day } = this.#state;
    if (sourcesProblem && day) await this.selectDay(day);
    else if (problem) await this.#read(this.#requested);
  }
}
