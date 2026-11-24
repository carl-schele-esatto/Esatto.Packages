import { describe, expect, it, vi } from 'vitest';
import { ApiError } from './api-error';
import { AnalysisModel, type AnalysisApi } from './analysis-model';
import { analysisState } from './analysis-view';
import { NOTE_MAX_LENGTH } from './note-text';
import { analysis, missingRow, targetRow, targets } from './testing/analysis-fixtures';
import type { AnalysisResponse, NoAnalysisResponse, NoteResponse, TargetsResponse } from './types';

// The Analysis tab's behaviour without a browser: what it reads, when a late answer is dropped, how the two editors save, and what a save does to
// what is held.

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

type Answer = AnalysisResponse | NoAnalysisResponse;

const ofMonth = (period: string, overrides: Partial<AnalysisResponse> = {}): AnalysisResponse => analysis({ period, ...overrides });

function setup() {
  const api = {
    getAnalysis: vi.fn<(period?: string) => Promise<Answer>>(),
    putNote: vi.fn<(period: string, text: string | null) => Promise<NoteResponse>>(),
    putTargets: vi.fn<(terms: string[]) => Promise<TargetsResponse>>(),
  } satisfies AnalysisApi;
  const changed = vi.fn();
  const noteSaved = vi.fn<(period: string, note: string | null) => void>();
  const model = new AnalysisModel(api, changed, { noteSaved });

  api.getAnalysis.mockImplementation(async (period) => ofMonth(period ?? '2026-10'));
  api.putNote.mockImplementation(async (_period, text) => ({ note: text ? text : null }));
  api.putTargets.mockImplementation(async (terms) => ({ terms: terms.map((term) => term.toLowerCase()) }));

  return { api, model, changed, noteSaved };
}

/** A model that is connected and shows October, with the analysis read. */
async function shown() {
  const parts = setup();
  parts.model.connect();
  await parts.model.show('2026-10');
  return parts;
}

describe('reading', () => {
  it('reads the analysis of the shown month, and nothing that goes to Google or saves anything', async () => {
    const { api, model } = setup();
    model.connect();

    await model.show('2026-10');

    expect(api.getAnalysis).toHaveBeenCalledTimes(1);
    expect(api.getAnalysis).toHaveBeenCalledWith('2026-10');
    expect(api.putNote).not.toHaveBeenCalled();
    expect(api.putTargets).not.toHaveBeenCalled();
    expect(model.state.period).toBe('2026-10');
    expect(model.state.loading).toBe(false);
    expect(analysisState(model.state)).toBe('ready');
  });

  it('is loading while the answer is on its way, and holds nothing from the month before meanwhile', async () => {
    const { api, model } = setup();
    model.connect();
    await model.show('2026-10');
    const slow = deferred<Answer>();
    api.getAnalysis.mockReturnValueOnce(slow.promise);

    const reading = model.show('2026-09');

    expect(model.state.loading).toBe(true);
    expect(model.state.analysis).toBeNull();
    expect(analysisState(model.state)).toBe('loading');

    slow.resolve(ofMonth('2026-09'));
    await reading;
    expect(model.state.analysis).toMatchObject({ period: '2026-09' });
  });

  it('is "none" for hasAnalysis false: no cards, and the answer is read first', async () => {
    const { api, model } = setup();
    api.getAnalysis.mockResolvedValue({ hasAnalysis: false });
    model.connect();

    await model.show('2026-10');

    expect(analysisState(model.state)).toBe('none');
  });

  it('reads nothing for no month', async () => {
    const { api, model } = setup();
    model.connect();

    await model.show('');

    expect(api.getAnalysis).not.toHaveBeenCalled();
    expect(analysisState(model.state)).toBe('idle');
  });

  it('shows a failed read as a problem, and Try again reads the same month again', async () => {
    const { api, model } = setup();
    api.getAnalysis.mockRejectedValueOnce(new ApiError(500, 'unknown'));
    model.connect();

    await model.show('2026-10');
    expect(model.state.problem?.code).toBe('unknown');
    expect(analysisState(model.state)).toBe('failed');

    await model.retry();

    expect(api.getAnalysis).toHaveBeenCalledTimes(2);
    expect(api.getAnalysis).toHaveBeenLastCalledWith('2026-10');
    expect(model.state.problem).toBeNull();
    expect(analysisState(model.state)).toBe('ready');
  });

  it('does not ask again for the month it shows', async () => {
    const { api, model } = await shown();

    await model.show('2026-10');

    expect(api.getAnalysis).toHaveBeenCalledTimes(1);
  });
});

// An analysis for a month that is no longer shown must never be on the screen ("against September 2025" while the picker stands on June 2026).
describe('a late answer for a month that is no longer shown', () => {
  it('is dropped when another month has been asked for since', async () => {
    const { api, model } = setup();
    model.connect();
    const slow = deferred<Answer>();
    api.getAnalysis.mockReturnValueOnce(slow.promise);

    const first = model.show('2026-09');
    await model.show('2026-10');
    slow.resolve(ofMonth('2026-09'));
    await first;

    expect(model.state.analysis).toMatchObject({ period: '2026-10' });
    expect(model.state.period).toBe('2026-10');
    expect(model.state.loading).toBe(false);
  });

  it('is dropped when the newer month comes out of the cache before the older answer arrives', async () => {
    const { api, model } = setup();
    model.connect();
    await model.show('2026-10');
    const slow = deferred<Answer>();
    api.getAnalysis.mockReturnValueOnce(slow.promise);

    const first = model.show('2026-09');
    await model.show('2026-10'); // from the cache
    slow.resolve(ofMonth('2026-09'));
    await first;

    expect(model.state.analysis).toMatchObject({ period: '2026-10' });
    expect(model.state.loading).toBe(false);
  });

  it('is not cached either: the month it was for is read again when it is asked for', async () => {
    const { api, model } = setup();
    model.connect();
    const slow = deferred<Answer>();
    api.getAnalysis.mockReturnValueOnce(slow.promise);

    const first = model.show('2026-09');
    await model.show('2026-10');
    slow.resolve(ofMonth('2026-09', { note: 'the dropped one' }));
    await first;
    await model.show('2026-09');

    expect(api.getAnalysis).toHaveBeenCalledTimes(3);
    expect(model.state.analysis).toMatchObject({ period: '2026-09', note: null });
  });

  it('is dropped when the failure of an older read comes after the newer answer', async () => {
    const { api, model } = setup();
    model.connect();
    const slow = deferred<Answer>();
    api.getAnalysis.mockReturnValueOnce(slow.promise);

    const first = model.show('2026-09');
    await model.show('2026-10');
    slow.reject(new ApiError(500, 'unknown'));
    await first;

    expect(model.state.problem).toBeNull();
    expect(analysisState(model.state)).toBe('ready');
  });

  it('is never shown when the server answers for a month other than the one asked for: that is a problem, not another month\'s analysis', async () => {
    const { api, model } = setup();
    api.getAnalysis.mockResolvedValue(ofMonth('2025-09'));
    model.connect();

    await model.show('2026-06');

    expect(model.state.analysis).toBeNull();
    expect(model.state.problem?.code).toBe('unknown');
    expect(analysisState(model.state)).toBe('failed');
  });
});

describe('the analyses of months already read', () => {
  it('are kept per month: a month shown again comes from them and is not read again', async () => {
    const { api, model } = setup();
    model.connect();
    await model.show('2026-10');
    await model.show('2026-09');

    await model.show('2026-10');

    expect(api.getAnalysis).toHaveBeenCalledTimes(2);
    expect(model.state.analysis).toMatchObject({ period: '2026-10' });
    expect(model.state.loading).toBe(false);
  });

  it('do not keep a read that failed', async () => {
    const { api, model } = setup();
    model.connect();
    api.getAnalysis.mockRejectedValueOnce(new ApiError(0, 'unknown'));
    await model.show('2026-10');
    await model.show('2026-09');

    await model.show('2026-10');

    expect(api.getAnalysis).toHaveBeenCalledTimes(3);
    expect(analysisState(model.state)).toBe('ready');
  });
});

// A month that was refreshed from Google, months that a backfill added: the analysis of the month that is shown (and of every other) has changed.
describe('when the archive has changed', () => {
  it('reads the month shown again and shows the new answer', async () => {
    const { api, model } = await shown();
    api.getAnalysis.mockResolvedValue(ofMonth('2026-10', { note: 'after the change', summary: [] }));

    await model.invalidate();

    expect(api.getAnalysis).toHaveBeenCalledTimes(2);
    expect(api.getAnalysis).toHaveBeenLastCalledWith('2026-10');
    expect(model.state.analysis).toMatchObject({ note: 'after the change', summary: [] });
    expect(model.state.loading).toBe(false);
  });

  it('keeps the cards on the screen while it reads: it is the same month, and the keyboard focus stays', async () => {
    const { api, model } = await shown();
    const before = model.state.analysis;
    const slow = deferred<Answer>();
    api.getAnalysis.mockReturnValueOnce(slow.promise);

    const reading = model.invalidate();

    expect(model.state.analysis).toBe(before);
    expect(model.state.loading).toBe(true);
    expect(model.state.targetsStale).toBe(false);
    expect(analysisState(model.state)).toBe('ready');

    slow.resolve(ofMonth('2026-10'));
    await reading;
    expect(model.state.loading).toBe(false);
  });

  it('forgets the other months too: they were computed from the archive as it was', async () => {
    const { api, model } = setup();
    model.connect();
    await model.show('2026-10');
    await model.show('2026-09');
    await model.invalidate();
    api.getAnalysis.mockClear();

    await model.show('2026-10');

    expect(api.getAnalysis).toHaveBeenCalledTimes(1);
    expect(api.getAnalysis).toHaveBeenCalledWith('2026-10');
  });

  it('drops a read that was on its way: it was started before the change', async () => {
    const { api, model } = await shown();
    const old = deferred<Answer>();
    api.getAnalysis.mockReturnValueOnce(old.promise);
    const reading = model.show('2026-09');

    await model.invalidate();
    old.resolve(ofMonth('2026-09', { note: 'before the change' }));
    await reading;

    expect(model.state.analysis).toMatchObject({ period: '2026-09', note: null });
  });

  it('shows a failed read next to the cards that are held, and Try again reads again', async () => {
    const { api, model } = await shown();
    api.getAnalysis.mockRejectedValueOnce(new ApiError(502, 'unknown'));

    await model.invalidate();

    expect(model.state.problem?.code).toBe('unknown');
    expect(analysisState(model.state)).toBe('ready');

    await model.retry();
    expect(model.state.problem).toBeNull();
  });

  it('reads nothing when there is no month, and nothing before it is connected or after it has left', async () => {
    const { api, model } = setup();

    await model.invalidate();
    model.connect();
    await model.invalidate();
    model.disconnect();
    await model.invalidate();

    expect(api.getAnalysis).not.toHaveBeenCalled();
  });
});

describe('saving the target queries', () => {
  it('sends the terms as typed: one per line, trimmed, without blank lines', async () => {
    const { api, model } = await shown();

    await model.saveTargets('  Blue Widgets \n\n widget repair\r\n');

    expect(api.putTargets).toHaveBeenCalledWith(['Blue Widgets', 'widget repair']);
  });

  it('shows the list as the server kept it, normalized, and says Saved', async () => {
    const { api, model } = await shown();
    api.putTargets.mockResolvedValue({ terms: ['blue widgets'] });
    // The analysis that is read after the save is for the list that was kept.
    api.getAnalysis.mockImplementation(async (period) => ofMonth(period ?? '', { targets: targets([targetRow({ term: 'blue widgets' })]) }));

    await model.saveTargets('Blue   Widgets\nBLUE WIDGETS');

    expect(model.terms).toEqual(['blue widgets']);
    expect(model.state.targets).toMatchObject({ saving: false, saved: true, problem: null });
    expect(model.state.targets.revision).toBe(1);
  });

  it('reads the analysis of the shown month again, and the cache of every other month is CLEARED: it was computed for the old list', async () => {
    const { api, model } = setup();
    model.connect();
    await model.show('2026-10');
    await model.show('2026-09');
    api.getAnalysis.mockClear();
    api.getAnalysis.mockImplementation(async (period) => ofMonth(period ?? '', { targets: targets([targetRow({ term: 'new term' })]) }));

    await model.saveTargets('new term');

    expect(api.getAnalysis).toHaveBeenCalledTimes(1);
    expect(api.getAnalysis).toHaveBeenCalledWith('2026-09');

    // The other month was cached before the change: it is read again, with the new list, and not taken from the cache.
    await model.show('2026-10');
    expect(api.getAnalysis).toHaveBeenCalledTimes(2);
    expect(api.getAnalysis).toHaveBeenLastCalledWith('2026-10');
    expect((model.state.analysis as AnalysisResponse).targets?.rows.map((row) => row.term)).toEqual(['new term']);
  });

  it('keeps the cards while it reads again, with the parts that depend on the terms marked as out of date, and is current again when the answer is in', async () => {
    const { api, model } = await shown();
    const slow = deferred<Answer>();
    api.getAnalysis.mockReturnValueOnce(slow.promise);

    const saving = model.saveTargets('anything');
    await vi.waitFor(() => expect(model.state.targetsStale).toBe(true));

    expect(analysisState(model.state)).toBe('ready');
    expect(model.state.loading).toBe(true);
    expect(model.state.targets.saved).toBe(true);
    // The editor shows the list that was kept meanwhile, not the one of the analysis that is out of date.
    expect(model.terms).toEqual(['anything']);

    slow.resolve(ofMonth('2026-10', { targets: null, targetHistory: null }));
    await saving;

    expect(model.state.targetsStale).toBe(false);
    expect(model.state.loading).toBe(false);
    expect(model.state.analysis).toMatchObject({ targets: null });
  });

  it('shows a re-read that failed as a problem next to the cards, which stay, and Try again reads again', async () => {
    const { api, model } = await shown();
    api.getAnalysis.mockRejectedValueOnce(new ApiError(502, 'unknown'));

    await model.saveTargets('anything');

    expect(model.state.problem?.code).toBe('unknown');
    expect(model.state.targetsStale).toBe(true);
    expect(analysisState(model.state)).toBe('ready');

    await model.retry();

    expect(model.state.problem).toBeNull();
    expect(model.state.targetsStale).toBe(false);
  });

  it('re-reads the month shown NOW, not the one it was saved on: the list is not a month\'s', async () => {
    const { api, model } = await shown();
    const slow = deferred<TargetsResponse>();
    api.putTargets.mockReturnValueOnce(slow.promise);

    const saving = model.saveTargets('term');
    await model.show('2026-09');
    api.getAnalysis.mockClear();
    slow.resolve({ terms: ['term'] });
    await saving;

    expect(api.getAnalysis).toHaveBeenCalledTimes(1);
    expect(api.getAnalysis).toHaveBeenCalledWith('2026-09');
  });

  it('drops an older read that was on its way when the list was saved: it was computed for the old list', async () => {
    const { api, model } = await shown();
    const old = deferred<Answer>();
    api.getAnalysis.mockReturnValueOnce(old.promise);
    const reading = model.show('2026-09');

    await model.saveTargets('term');
    old.resolve(ofMonth('2026-09', { targets: targets([targetRow({ term: 'the old list' })]) }));
    await reading;

    expect((model.state.analysis as AnalysisResponse).targets?.rows.map((row) => row.term)).not.toContain('the old list');
    expect(model.state.period).toBe('2026-09');
  });

  it('does nothing else when it fails: no clearing, no re-read, the cards stay, and Try again sends the same text', async () => {
    const { api, model } = await shown();
    api.putTargets.mockRejectedValueOnce(new ApiError(400, 'requestInvalid'));
    const before = model.state.analysis;

    await model.saveTargets('term one\nterm two');

    expect(model.state.targets).toMatchObject({ saving: false, saved: false });
    expect(model.state.targets.problem?.code).toBe('requestInvalid');
    expect(model.state.analysis).toBe(before);
    expect(api.getAnalysis).toHaveBeenCalledTimes(1);

    await model.retryTargets();

    expect(api.putTargets).toHaveBeenCalledTimes(2);
    expect(api.putTargets).toHaveBeenLastCalledWith(['term one', 'term two']);
    expect(model.state.targets.problem).toBeNull();
    expect(model.state.targets.saved).toBe(true);
  });

  it('ignores a second save while one is on its way: the button stays focusable and does nothing', async () => {
    const { api, model } = await shown();
    const slow = deferred<TargetsResponse>();
    api.putTargets.mockReturnValueOnce(slow.promise);

    const first = model.saveTargets('one');
    await model.saveTargets('two');

    expect(api.putTargets).toHaveBeenCalledTimes(1);
    expect(model.state.targets.saving).toBe(true);

    slow.resolve({ terms: ['one'] });
    await first;
    expect(model.state.targets.saving).toBe(false);
  });

  it('takes the Saved announcement back when it is edited again, so that the next save is announced again', async () => {
    const { model } = await shown();
    await model.saveTargets('one');
    expect(model.state.targets.saved).toBe(true);

    model.termsEdited();
    expect(model.state.targets.saved).toBe(false);

    await model.saveTargets('one');
    expect(model.state.targets.saved).toBe(true);
    expect(model.state.targets.revision).toBe(2);
  });

  it('can save an empty list: the terms are then none, and the table is hidden', async () => {
    const { api, model } = await shown();
    api.putTargets.mockResolvedValue({ terms: [] });
    api.getAnalysis.mockImplementation(async (period) => ofMonth(period ?? '', { targets: null, targetHistory: null }));

    await model.saveTargets('  \n ');

    expect(api.putTargets).toHaveBeenCalledWith([]);
    expect(model.terms).toEqual([]);
  });

  it('starts the editor from the terms of the analysis', async () => {
    const { api, model } = setup();
    api.getAnalysis.mockResolvedValue(ofMonth('2026-10', { targets: targets([targetRow({ term: 'b' }), missingRow({ term: 'a' })]) }));
    model.connect();

    await model.show('2026-10');

    expect(model.terms).toEqual(['b', 'a']);
  });
});

describe('saving the note of a month', () => {
  it('sends the TRIMMED text for the month it was written for', async () => {
    const { api, model } = await shown();

    await model.saveNote('2026-10', '  A launch in October.\n');

    expect(api.putNote).toHaveBeenCalledWith('2026-10', 'A launch in October.');
  });

  it('REFUSES a note longer than the limit and sends nothing: it says so, and the text is the person\'s to shorten', async () => {
    const { api, model } = await shown();

    await model.saveNote('2026-10', 'x'.repeat(NOTE_MAX_LENGTH + 1));

    expect(api.putNote).not.toHaveBeenCalled();
    expect(model.state.note).toMatchObject({ refused: true, saving: false, saved: false, problem: null });
  });

  it('sends a note of exactly the limit, and counts the trimmed text only', async () => {
    const { api, model } = await shown();

    await model.saveNote('2026-10', ` ${'x'.repeat(NOTE_MAX_LENGTH)} `);

    expect(api.putNote).toHaveBeenCalledWith('2026-10', 'x'.repeat(NOTE_MAX_LENGTH));
  });

  it('counts UTF-16 code units as the server does: five hundred and one emoji are too long', async () => {
    const { api, model } = await shown();

    await model.saveNote('2026-10', '\u{1F600}'.repeat(501));

    expect(api.putNote).not.toHaveBeenCalled();
    expect(model.state.note.refused).toBe(true);
  });

  it('sends an empty text for white space only: the note is then removed, and the server answers null', async () => {
    const { api, model, noteSaved } = await shown();

    await model.saveNote('2026-10', '   \n ');

    expect(api.putNote).toHaveBeenCalledWith('2026-10', '');
    expect(noteSaved).toHaveBeenCalledWith('2026-10', null);
  });

  it('tells the shell what the server kept, which is what is shown afterwards, and says Saved', async () => {
    const { api, model, noteSaved } = await shown();
    api.putNote.mockResolvedValue({ note: 'what the server kept' });

    await model.saveNote('2026-10', ' what I wrote ');

    expect(noteSaved).toHaveBeenCalledTimes(1);
    expect(noteSaved).toHaveBeenCalledWith('2026-10', 'what the server kept');
    expect(model.state.note).toMatchObject({ saving: false, saved: true, refused: false, problem: null });
    expect(model.state.note.revision).toBe(1);
  });

  it('does not read anything again: a note changes the note and nothing else', async () => {
    const { api, model } = await shown();

    await model.saveNote('2026-10', 'a note');

    expect(api.getAnalysis).toHaveBeenCalledTimes(1);
    expect(api.putTargets).not.toHaveBeenCalled();
  });

  it('shows a failure as a problem, tells the shell nothing, and Try again sends the same text for the same month', async () => {
    const { api, model, noteSaved } = await shown();
    api.putNote.mockRejectedValueOnce(new ApiError(500, 'unknown'));

    await model.saveNote('2026-10', ' a note ');

    expect(model.state.note).toMatchObject({ saving: false, saved: false });
    expect(model.state.note.problem?.code).toBe('unknown');
    expect(noteSaved).not.toHaveBeenCalled();

    await model.retryNote();

    expect(api.putNote).toHaveBeenCalledTimes(2);
    expect(api.putNote).toHaveBeenLastCalledWith('2026-10', 'a note');
    expect(model.state.note.problem).toBeNull();
    expect(noteSaved).toHaveBeenCalledWith('2026-10', 'a note');
  });

  it('ignores a second save while one is on its way', async () => {
    const { api, model } = await shown();
    const slow = deferred<NoteResponse>();
    api.putNote.mockReturnValueOnce(slow.promise);

    const first = model.saveNote('2026-10', 'one');
    await model.saveNote('2026-10', 'two');

    expect(api.putNote).toHaveBeenCalledTimes(1);

    slow.resolve({ note: 'one' });
    await first;
  });

  it('clears what was said about the last save when the text is edited again', async () => {
    const { model } = await shown();
    await model.saveNote('2026-10', 'x'.repeat(NOTE_MAX_LENGTH + 1));
    expect(model.state.note.refused).toBe(true);

    model.noteEdited();

    expect(model.state.note.refused).toBe(false);

    await model.saveNote('2026-10', 'ok');
    expect(model.state.note.saved).toBe(true);
    model.noteEdited();
    expect(model.state.note.saved).toBe(false);
  });

  it('does not tell the editor of another month that a note was saved: the month changed while the save was on its way', async () => {
    const { api, model, noteSaved } = await shown();
    const slow = deferred<NoteResponse>();
    api.putNote.mockReturnValueOnce(slow.promise);

    const saving = model.saveNote('2026-10', 'a note for October');
    await model.show('2026-09');
    slow.resolve({ note: 'a note for October' });
    await saving;

    // The PUT carried the month of the click, and the shell is told that month's note...
    expect(api.putNote).toHaveBeenCalledWith('2026-10', 'a note for October');
    expect(noteSaved).toHaveBeenCalledWith('2026-10', 'a note for October');
    // ...but the editor now stands on another month and says nothing about it.
    expect(model.state.period).toBe('2026-09');
    expect(model.state.note).toMatchObject({ saving: false, saved: false, refused: false, problem: null });
  });

  it('does not let the failure of a save for the month before show up on the month now shown', async () => {
    const { api, model } = await shown();
    const slow = deferred<NoteResponse>();
    api.putNote.mockReturnValueOnce(slow.promise);

    const saving = model.saveNote('2026-10', 'a note');
    await model.show('2026-09');
    slow.reject(new ApiError(500, 'unknown'));
    await saving;

    expect(model.state.note.problem).toBeNull();
  });

  it('lets the month now shown be saved while the save of the month before is still on its way', async () => {
    const { api, model, noteSaved } = await shown();
    const slow = deferred<NoteResponse>();
    api.putNote.mockReturnValueOnce(slow.promise);

    const first = model.saveNote('2026-10', 'for October');
    await model.show('2026-09');
    await model.saveNote('2026-09', 'for September');
    slow.resolve({ note: 'for October' });
    await first;

    expect(noteSaved.mock.calls).toEqual([
      ['2026-09', 'for September'],
      ['2026-10', 'for October'],
    ]);
    expect(model.state.note.saved).toBe(true);
  });
});

describe('when the element is gone', () => {
  it('ignores the answer of a read that was on its way: it touches nothing and caches nothing', async () => {
    const { api, model, changed } = setup();
    model.connect();
    const slow = deferred<Answer>();
    api.getAnalysis.mockReturnValueOnce(slow.promise);

    const reading = model.show('2026-10');
    model.disconnect();
    const before = changed.mock.calls.length;
    slow.resolve(ofMonth('2026-10'));
    await reading;

    expect(model.state.analysis).toBeNull();
    expect(changed.mock.calls.length).toBe(before);
  });

  it('ignores the answer of a note save that was on its way: the shell is not told, and nothing is touched', async () => {
    const { api, model, noteSaved, changed } = await shown();
    const slow = deferred<NoteResponse>();
    api.putNote.mockReturnValueOnce(slow.promise);

    const saving = model.saveNote('2026-10', 'a note');
    model.disconnect();
    const before = changed.mock.calls.length;
    slow.resolve({ note: 'a note' });
    await saving;

    expect(noteSaved).not.toHaveBeenCalled();
    expect(changed.mock.calls.length).toBe(before);
  });

  it('ignores the answer of a target save that was on its way: nothing is read again', async () => {
    const { api, model, changed } = await shown();
    const slow = deferred<TargetsResponse>();
    api.putTargets.mockReturnValueOnce(slow.promise);

    const saving = model.saveTargets('term');
    model.disconnect();
    const before = changed.mock.calls.length;
    slow.resolve({ terms: ['term'] });
    await saving;

    expect(api.getAnalysis).toHaveBeenCalledTimes(1);
    expect(changed.mock.calls.length).toBe(before);
  });

  it('ignores the failure of a save that was on its way', async () => {
    const { api, model } = await shown();
    const slow = deferred<NoteResponse>();
    api.putNote.mockReturnValueOnce(slow.promise);

    const saving = model.saveNote('2026-10', 'a note');
    model.disconnect();
    slow.reject(new ApiError(500, 'unknown'));
    await saving;

    expect(model.state.note.problem).toBeNull();
  });

  it('reads again when it is connected again and holds nothing', async () => {
    const { api, model } = setup();
    model.connect();
    await model.show('2026-10');
    model.disconnect();
    model.connect();
    await vi.waitFor(() => expect(api.getAnalysis).toHaveBeenCalledTimes(2));

    expect(model.state.period).toBe('2026-10');
  });

  it('reads nothing before it is connected: the month is only remembered', async () => {
    const { api, model } = setup();

    await model.show('2026-10');
    expect(api.getAnalysis).not.toHaveBeenCalled();

    model.connect();
    await vi.waitFor(() => expect(api.getAnalysis).toHaveBeenCalledTimes(1));
    expect(model.state.analysis).toMatchObject({ period: '2026-10' });
  });
});

describe('telling the element', () => {
  it('says so every time the state changes, so it can draw again', async () => {
    const { model, changed } = setup();
    model.connect();

    await model.show('2026-10');

    expect(changed).toHaveBeenCalled();
  });
});
