// What the Analysis tab reads, does and remembers, without any DOM: the element only draws this and passes clicks to it.
//
// IT READS THE ARCHIVE AND NOTHING ELSE. GET analysis is archive-only on the server, and saving a note or the target queries never starts a fetch.
// Nothing here asks for anything that goes to Google.
//
// AN ANSWER MAY BE LATE, AND THE MONTH IT WAS FOR MAY NO LONGER BE THE ONE SHOWN. Every read is checked on return (`#reads`, `#epoch`) and dropped,
// and cached nowhere, when a newer one has replaced it or the element has left. A month's analysis must never be on the screen under another month's
// picker ("against September 2025" while the picker stands on June 2026). An answer for a month other than the one asked for is not shown either.
//
// THE ANALYSES ARE KEPT PER MONTH, AND THE CACHE IS CLEARED WHEN THE TARGET QUERIES CHANGE: each of them was computed for the old list, and a month
// opened again afterwards would otherwise show it.
//
// THE TWO EDITORS. Saving the target queries sends the terms as typed, shows the list as the server kept it, clears the cache and reads the shown
// month again (the cards stay while it does, with the parts that depend on the terms marked as out of date, so that the keyboard's place and the
// "Saved" announcement are not lost). Saving the month's note refuses a text over the limit without sending it, sends the trimmed text for the month
// it was written for (the month of the click, not the one shown when the answer arrives), and tells the shell what the server kept. A save that is
// on its way is ignored when another is asked for (the button stays focusable and does nothing), and an answer that arrives after the element has
// gone is ignored.

import { isAnalysis } from './analysis-view';
import { ApiError, toApiError } from './api-error';
import { noteLimit } from './note-text';
import { enteredTerms } from './target-terms';
import { termsOf } from './target-view';
import type { AnalysisResponse, NoAnalysisResponse, NoteResponse, TargetsResponse } from './types';

/** The calls the model makes: the functions of api.ts, or stand-ins in a test. */
export interface AnalysisApi {
  getAnalysis(period?: string): Promise<AnalysisResponse | NoAnalysisResponse>;
  putNote(period: string, text: string | null): Promise<NoteResponse>;
  putTargets(terms: string[]): Promise<TargetsResponse>;
}

/** What the element hands the shell when the model has something to tell it. */
export interface AnalysisEvents {
  /** A note was saved for a month: `note` is what the server kept (trimmed), or null when it was removed. */
  noteSaved(period: string, note: string | null): void;
}

/** The event the element dispatches for it, and what it carries. */
export const NOTE_SAVED_EVENT = 'note-saved';

export interface NoteSavedDetail {
  period: string;
  note: string | null;
}

/** How the last save of an editor went. */
export interface SaveState {
  /** A save is on its way: the button stays focusable (aria-disabled) and ignores clicks. */
  saving: boolean;
  /** The last save succeeded and the text has not been edited since: the polite "Saved" announcement. */
  saved: boolean;
  /** Counts the saves that succeeded: the editor shows what was kept again whenever it changes, even when it equals what it showed. */
  revision: number;
  problem: ApiError | null;
  /** The note was over the limit and was not sent. */
  refused: boolean;
}

export interface AnalysisModelState {
  /** The month shown, "YYYY-MM"; empty until one has been asked for. */
  period: string;
  analysis: AnalysisResponse | NoAnalysisResponse | null;
  /** The analysis is being read. */
  loading: boolean;
  /** The analysis could not be read. */
  problem: ApiError | null;
  /** The target queries were saved and the analysis is being read again (or that failed): what depends on the terms is out of date. */
  targetsStale: boolean;
  /** The target queries as the server kept them, after a save and until the next analysis arrives; null otherwise. */
  terms: string[] | null;
  note: SaveState;
  targets: SaveState;
}

const IDLE_SAVE: SaveState = { saving: false, saved: false, revision: 0, problem: null, refused: false };

const INITIAL: AnalysisModelState = {
  period: '',
  analysis: null,
  loading: false,
  problem: null,
  targetsStale: false,
  terms: null,
  note: IDLE_SAVE,
  targets: IDLE_SAVE,
};

export class AnalysisModel {
  readonly #api: AnalysisApi;
  readonly #changed: () => void;
  readonly #events: AnalysisEvents;

  #state: AnalysisModelState = INITIAL;
  #connected = false;
  // Bumped by connect and disconnect: an awaited call that started under an earlier one is dropped when it returns.
  #epoch = 0;
  // Bumped by every read of an analysis and by disconnect: only the newest is used.
  #reads = 0;
  // Bumped when the month changes (and by disconnect): a note save that began under an earlier one says nothing to the editor.
  #months = 0;
  // The month asked for: remembered while the model is not connected, and what Try again reads.
  #period = '';
  readonly #cache = new Map<string, AnalysisResponse | NoAnalysisResponse>();
  #retryNote: (() => Promise<void>) | null = null;
  #retryTargets: (() => Promise<void>) | null = null;

  constructor(api: AnalysisApi, changed: () => void, events: AnalysisEvents) {
    this.#api = api;
    this.#changed = changed;
    this.#events = events;
  }

  get state(): Readonly<AnalysisModelState> {
    return this.#state;
  }

  /** The target queries the editor shows: the list as the server kept it after a save, else the terms of the analysis. */
  get terms(): string[] {
    const held = this.#state.analysis;
    return this.#state.terms ?? termsOf(isAnalysis(held) ? held.targets : null);
  }

  // ---- Lifetime ------------------------------------------------------------------------------------------------------------------

  /** The element is on the page: reads the month it was given, if it holds nothing. */
  connect(): void {
    this.#connected = true;
    this.#epoch++;
    if (this.#period && this.#state.analysis === null && !this.#state.loading) void this.#read(this.#period, false);
  }

  /** The element has left the page: nothing that is on its way may touch anything, and nothing it held is kept. */
  disconnect(): void {
    this.#connected = false;
    this.#epoch++;
    this.#reads++;
    this.#months++;
    this.#cache.clear();
    // Not announced: the element is gone.
    this.#state = INITIAL;
  }

  // ---- Reading -------------------------------------------------------------------------------------------------------------------

  /** The month shown has changed (or is shown for the first time). Resolves when the analysis of it is shown. */
  async show(period: string): Promise<void> {
    if (period === this.#period && (this.#state.analysis !== null || this.#state.loading)) return;

    this.#period = period;
    this.#months++;
    if (!this.#connected) return; // connect() reads it

    // The note editor belongs to the month: what was said about the last save does not carry over to another month.
    this.#patch({ note: { ...IDLE_SAVE, revision: this.#state.note.revision } });

    if (!period) {
      this.#reads++;
      this.#patch({ period: '', analysis: null, loading: false, problem: null, targetsStale: false, terms: null });
      return;
    }

    const cached = this.#cache.get(period);
    if (cached) {
      this.#reads++;
      this.#patch({ period, analysis: cached, loading: false, problem: null, targetsStale: false, terms: null });
      return;
    }

    await this.#read(period, false);
  }

  // Reads one month and shows it. `keep`: what is held stays on the screen meanwhile (the target queries changed: the cards stay and the part that
  // depends on the terms is marked as out of date); otherwise the month before is not shown while the next is read.
  async #read(period: string, keep: boolean): Promise<void> {
    const read = ++this.#reads;
    const epoch = this.#epoch;
    const current = () => this.#connected && epoch === this.#epoch && read === this.#reads;

    this.#patch({ period, loading: true, problem: null, analysis: keep ? this.#state.analysis : null });

    try {
      const answer = await this.#api.getAnalysis(period);
      if (!current()) return;

      // The server echoes the month it was asked for. Another one is not this month's analysis, and never goes on the screen as one.
      if (isAnalysis(answer) && answer.period !== period) {
        this.#patch({ loading: false, problem: new ApiError(0, 'unknown') });
        return;
      }

      this.#cache.set(period, answer);
      this.#patch({ analysis: answer, loading: false, problem: null, targetsStale: false, terms: null });
    } catch (error) {
      if (current()) this.#patch({ loading: false, problem: toApiError(error) });
    }
  }

  /**
   * The archive has changed under the month shown (it was refreshed from Google, or a backfill added months): every analysis held was computed
   * from the archive as it was, so none is kept, and the month shown is read again. What is held stays on the screen meanwhile: it is the same
   * month, and the cards, and the keyboard's place in them, do not go away.
   */
  async invalidate(): Promise<void> {
    this.#cache.clear();
    if (!this.#connected || !this.#period) return;

    await this.#read(this.#period, this.#state.analysis !== null);
  }

  /** "Try again" after a read that failed: reads the month again. */
  async retry(): Promise<void> {
    if (!this.#connected || !this.#period || !this.#state.problem) return;
    await this.#read(this.#period, this.#state.analysis !== null);
  }

  // ---- The month's note -----------------------------------------------------------------------------------------------------------

  /**
   * Saves the note of `period`, the month it was written for. A text over the limit is refused without a request. What the server kept (trimmed,
   * null when the text was blank) goes to the shell, which holds the note of the month shown; the editor says Saved only if the month is still
   * the one shown.
   */
  async saveNote(period: string, text: string): Promise<void> {
    if (!this.#connected || this.#state.note.saving) return;

    const limit = noteLimit(text);
    if (limit.over) {
      this.#patchNote({ refused: true, saved: false, problem: null });
      return;
    }

    const epoch = this.#epoch;
    const months = this.#months;
    const current = () => this.#connected && epoch === this.#epoch;

    this.#retryNote = () => this.saveNote(period, text);
    this.#patchNote({ saving: true, saved: false, refused: false, problem: null });

    try {
      const response = await this.#api.putNote(period, limit.trimmed);
      if (!current()) return;

      this.#events.noteSaved(period, response.note ?? null);
      if (months === this.#months) this.#patchNote({ saving: false, saved: true, revision: this.#state.note.revision + 1 });
    } catch (error) {
      if (current() && months === this.#months) this.#patchNote({ saving: false, problem: toApiError(error) });
    }
  }

  /** The text of the note was edited: what was said about the last save no longer applies. */
  noteEdited(): void {
    const { saved, refused, problem } = this.#state.note;
    if (saved || refused || problem) this.#patchNote({ saved: false, refused: false, problem: null });
  }

  /** "Try again" after a save that failed. */
  async retryNote(): Promise<void> {
    if (this.#state.note.problem) await this.#retryNote?.();
  }

  // ---- The target queries ------------------------------------------------------------------------------------------------------

  /**
   * Saves the target queries (the terms as typed, one per line; the server normalizes them), shows the list it kept, clears the analyses held and
   * reads the month shown NOW again: the list belongs to no month.
   */
  async saveTargets(text: string): Promise<void> {
    if (!this.#connected || this.#state.targets.saving) return;

    const epoch = this.#epoch;
    const current = () => this.#connected && epoch === this.#epoch;

    this.#retryTargets = () => this.saveTargets(text);
    this.#patchTargets({ saving: true, saved: false, problem: null });

    let kept: string[];
    try {
      kept = (await this.#api.putTargets(enteredTerms(text))).terms ?? [];
    } catch (error) {
      if (current()) this.#patchTargets({ saving: false, problem: toApiError(error) });
      return;
    }
    if (!current()) return;

    // Every analysis held was computed for the old list.
    this.#cache.clear();
    this.#patch({
      targets: { ...this.#state.targets, saving: false, saved: true, revision: this.#state.targets.revision + 1 },
      terms: kept,
      targetsStale: true,
    });

    if (this.#period) await this.#read(this.#period, this.#state.analysis !== null);
    else this.#patch({ targetsStale: false });
  }

  /** The text of the target queries was edited: what was said about the last save no longer applies. */
  termsEdited(): void {
    const { saved, problem } = this.#state.targets;
    if (saved || problem) this.#patchTargets({ saved: false, problem: null });
  }

  /** "Try again" after a save that failed. */
  async retryTargets(): Promise<void> {
    if (this.#state.targets.problem) await this.#retryTargets?.();
  }

  // ---- Keeping the state --------------------------------------------------------------------------------------------------------------

  #patchNote(patch: Partial<SaveState>): void {
    this.#patch({ note: { ...this.#state.note, ...patch } });
  }

  #patchTargets(patch: Partial<SaveState>): void {
    this.#patch({ targets: { ...this.#state.targets, ...patch } });
  }

  #patch(patch: Partial<AnalysisModelState>): void {
    this.#state = { ...this.#state, ...patch };
    this.#changed();
  }
}
