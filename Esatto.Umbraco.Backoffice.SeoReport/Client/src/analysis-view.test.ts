import { describe, expect, it } from 'vitest';
import { ApiError } from './api-error';
import { analysisState, analysisView, isAnalysis, opportunitiesView, statementsView } from './analysis-view';
import { analysis, missingRow, opportunity, statement, targetRow, targets } from './testing/analysis-fixtures';
import { enHost } from './testing/host';
import type { AnalysisResponse, NoAnalysisResponse } from './types';

const host = enHost();
const none: NoAnalysisResponse = { hasAnalysis: false };

describe('statementsView', () => {
  it('writes each statement from its reference and its arguments, in the order the server sent them', () => {
    const view = statementsView(host, [
      statement('nonBrand', { clicks: 300, total: 900, percent: 33.3 }),
      statement('conversion', { sessions: 1200, events: 1 }),
    ]);

    expect(view.map((item) => item.key)).toEqual(['nonBrand', 'conversion']);
    expect(view[0].text).toContain('Non-brand accounts for 300 of 900 clicks (33.3%).');
    expect(view[1].text).toBe('1,200 visits from search engines led to 1 key event.');
  });

  it('writes the figures it was given and says the sentence without figures for an argument that is null: null is "nothing to say"', () => {
    const [item] = statementsView(host, [statement('visibility', { top3: 12, total: 40, percent: 30, comparePeriod: null, compareTop3: null, comparePercent: null, bestPeriod: null })]);

    expect(item.text).toBe('12 of your 40 most shown search queries rank in the top 3 (30.0%).');
    expect(item.text).not.toMatch(/null|undefined|NaN/);
  });

  it('reads a statement with no arguments at all', () => {
    const [item] = statementsView(host, [statement('season')]);

    expect(item.text).toContain('only compared with one earlier month');
  });

  it('names a month from its period, in the language of the host', () => {
    const [item] = statementsView(host, [statement('season', { period: '2026-09' })]);

    expect(item.text).toContain('compared with September 2026');
  });

  it('leaves out a statement whose key is malformed, so that one bad value does not stop the others', () => {
    const view = statementsView(host, [statement('not a key'), statement('season')]);

    expect(view.map((item) => item.key)).toEqual(['season']);
  });

  it('is empty for no statements and for a missing list', () => {
    expect(statementsView(host, [])).toEqual([]);
    expect(statementsView(host, null)).toEqual([]);
    expect(statementsView(host, undefined)).toEqual([]);
  });
});

describe('opportunitiesView', () => {
  it('writes the title and the text of an opportunity from ONE reference: the key is the kind, and both get the arguments', () => {
    const [item] = opportunitiesView(host, [opportunity()]);

    expect(item.kind).toBe('near');
    expect(item.title).toBe('"blue widgets" is close to page one');
    expect(item.text).toMatch(/^Position 12\.5 with 480 impressions\. /);
  });

  it('writes the competing pages of a cannibalization with their figures', () => {
    const [item] = opportunitiesView(host, [
      opportunity({
        kind: 'cannibal',
        text: {
          key: 'cannibal',
          args: { query: 'widgets', firstPath: '/a', firstPosition: 8, firstImpressions: 1200, secondPath: '/b', secondPosition: 18.5, secondImpressions: 300 },
        },
        effort: 3,
      }),
    ]);

    expect(item.title).toBe('Two pages compete for "widgets"');
    expect(item.text).toContain('/a (position 8.0, 1,200 impressions) and /b (position 18.5, 300 impressions)');
  });

  it('keeps the server order: the cheapest first, and the order within a kind', () => {
    const view = opportunitiesView(host, [opportunity({ text: { key: 'near', args: { query: 'first' } } }), opportunity({ text: { key: 'near', args: { query: 'second' } } }), opportunity({ kind: 'cannibal', text: { key: 'cannibal', args: { query: 'third' } }, effort: 3 })]);

    expect(view.map((item) => item.title)).toEqual(['"first" is close to page one', '"second" is close to page one', 'Two pages compete for "third"']);
  });

  it('labels the effort in words: a small one, a medium one, a large one', () => {
    const labels = [1, 2, 3, 4, 9].map((effort) => opportunitiesView(host, [opportunity({ effort })])[0].effort);

    expect(labels.map((effort) => effort?.level)).toEqual(['small', 'small', 'medium', 'large', 'large']);
    expect(labels.map((effort) => effort?.label)).toEqual(['Small effort', 'Small effort', 'Medium effort', 'Large effort', 'Large effort']);
  });

  it('has no effort for a value that is not a number', () => {
    expect(opportunitiesView(host, [opportunity({ effort: Number.NaN })])[0].effort).toBeNull();
    expect(opportunitiesView(host, [opportunity({ effort: null as never })])[0].effort).toBeNull();
  });

  it('is empty when the server withheld them (null: a user outside the findings groups), so the card is not drawn at all', () => {
    expect(opportunitiesView(host, null)).toEqual([]);
    expect(opportunitiesView(host, undefined)).toEqual([]);
  });

  it('is empty when none were found: no empty card either', () => {
    expect(opportunitiesView(host, [])).toEqual([]);
  });

  it('skips one whose key is malformed', () => {
    expect(opportunitiesView(host, [opportunity({ text: { key: 'not a key' } }), opportunity()])).toHaveLength(1);
  });
});

describe('isAnalysis', () => {
  it('reads hasAnalysis, and only a true one is an analysis', () => {
    expect(isAnalysis(analysis())).toBe(true);
    expect(isAnalysis(none)).toBe(false);
    expect(isAnalysis(null)).toBe(false);
    expect(isAnalysis(undefined)).toBe(false);
    // A body without the member is not read as an analysis: hasAnalysis is the first thing a client reads.
    expect(isAnalysis({} as never)).toBe(false);
    expect(isAnalysis({ period: '2026-10', summary: [] } as never)).toBe(false);
  });
});

describe('analysisState', () => {
  const idle = { analysis: null, loading: false, problem: null };

  it('is ready for an analysis, even while it is being read again or the re-read failed: the cards stay where they are', () => {
    expect(analysisState({ ...idle, analysis: analysis() })).toBe('ready');
    expect(analysisState({ ...idle, analysis: analysis(), loading: true })).toBe('ready');
    expect(analysisState({ ...idle, analysis: analysis(), problem: new ApiError(500, 'unknown') })).toBe('ready');
  });

  it('is "none" for hasAnalysis false: nothing to analyze, no cards', () => {
    expect(analysisState({ ...idle, analysis: none })).toBe('none');
  });

  it('is "none" for a body that is neither: it shows no cards either', () => {
    expect(analysisState({ ...idle, analysis: {} as AnalysisResponse })).toBe('none');
  });

  it('is loading while nothing is held and a read is going on, failed when it failed, idle otherwise', () => {
    expect(analysisState({ ...idle, loading: true })).toBe('loading');
    expect(analysisState({ ...idle, problem: new ApiError(500, 'unknown') })).toBe('failed');
    expect(analysisState({ ...idle, loading: true, problem: new ApiError(500, 'unknown') })).toBe('failed');
    expect(analysisState(idle)).toBe('idle');
  });
});

describe('analysisView', () => {
  it('has the statements, the terms against the month, their history, the movement and the quick wins', () => {
    const view = analysisView(host, analysis());

    expect(view.statements).toHaveLength(1);
    expect(view.targets.state).toBe('ready');
    expect(view.targets.terms).toEqual(['blue widgets', 'widget insurance']);
    expect(view.history).not.toBeNull();
    expect(view.movement.state).toBe('ready');
    expect(view.opportunities).toHaveLength(1);
  });

  it('hides the quick wins when they are withheld, and keeps everything else', () => {
    const view = analysisView(host, analysis({ opportunities: null }));

    expect(view.opportunities).toEqual([]);
    expect(view.statements).toHaveLength(1);
    expect(view.movement.state).toBe('ready');
  });

  it('hides the table and the history of the terms for an empty list of terms, and says the card has none', () => {
    const view = analysisView(host, analysis({ targets: null, targetHistory: null }));

    expect(view.targets.state).toBe('noTerms');
    expect(view.targets.rows).toEqual([]);
    expect(view.history).toBeNull();

    expect(analysisView(host, analysis({ targets: targets([]) })).targets.state).toBe('noTerms');
  });

  it('shows no movement when there is none to compare with', () => {
    expect(analysisView(host, analysis({ movement: null })).movement.state).toBe('none');
  });

  it('has no statements for a month the server had nothing to say about', () => {
    expect(analysisView(host, analysis({ summary: [] })).statements).toEqual([]);
  });

  it('writes a month that is not archived as the server sends it: rows that are all "not visible", the history of the archive', () => {
    const view = analysisView(host, analysis({ movement: null, summary: [], opportunities: [], targets: targets([missingRow({ advice: { key: 'missingNoQueries' } }), missingRow({ term: 'other', advice: { key: 'missingNoQueries' } })], 0) }));

    expect(view.targets.rows.map((row) => row.cells[1].text)).toEqual(['Not visible', 'Not visible']);
    expect(view.targets.rowsFetched).toBe(0);
    expect(view.history).not.toBeNull();
  });

  it('keeps a row that has a match', () => {
    const view = analysisView(host, analysis({ targets: targets([targetRow({ status: 'performing' })]) }));

    expect(view.targets.rows[0].cells[1].text).toBe('Performing');
  });
});
