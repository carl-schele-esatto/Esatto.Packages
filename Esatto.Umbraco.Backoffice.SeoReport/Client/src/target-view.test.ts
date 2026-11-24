import { describe, expect, it } from 'vitest';
import en from './lang/en';
import { lookupKey } from './localize';
import { TARGET_STATUSES, historyView, statusLabelKey, targetColumns, targetRows, targetsView, termsOf } from './target-view';
import { history, missingRow, targetPage, targetRow, targets } from './testing/analysis-fixtures';
import { enHost } from './testing/host';

const host = enHost();
const texts = en.esattoSeoReport as Record<string, unknown>;
const cells = (row: { cells: { text: string }[] }) => row.cells.map((cell) => cell.text);

describe('statusLabelKey', () => {
  it('maps each status code of the server to its label key, which is the key the server-side lookup builds', () => {
    expect(TARGET_STATUSES).toEqual(['missing', 'deep', 'near', 'title', 'performing']);

    for (const status of TARGET_STATUSES) {
      const key = statusLabelKey(status);

      expect(key, status).toBe(lookupKey('target', status, 'Label'));
      expect(typeof texts[key!], key!).toBe('string');
    }
  });

  it('gives the expected English label for each code', () => {
    const labels = TARGET_STATUSES.map((status) => texts[statusLabelKey(status)!]);

    expect(labels).toEqual(['Not visible', 'Ranking deep', 'Close to page one', 'Not clicked', 'Performing']);
  });

  it('has no key for a code it does not know', () => {
    expect(statusLabelKey('unheard-of')).toBeNull();
    expect(statusLabelKey('')).toBeNull();
    expect(statusLabelKey(undefined as never)).toBeNull();
  });
});

describe('targetRows', () => {
  it('writes each term with its status as an icon AND a label, never a colour alone', () => {
    const rows = targetRows(host, [targetRow({ status: 'deep' }), targetRow({ status: 'performing' })]);

    expect(rows.map((row) => row.cells[1].text)).toEqual(['Ranking deep', 'Performing']);
    expect(rows.every((row) => typeof row.cells[1].icon === 'string' && row.cells[1].icon!.startsWith('icon-'))).toBe(true);
    // One glyph per status: the icon repeats the label for the eye, it does not replace it.
    expect(new Set(TARGET_STATUSES.map((status) => targetRows(host, [targetRow({ status })])[0].cells[1].icon)).size).toBe(TARGET_STATUSES.length);
  });

  it('writes the figures: whole numbers grouped, the click-through rate as the percentage it already is, the position with one decimal', () => {
    const [row] = targetRows(host, [targetRow({ impressions: 12345, clicks: 1234, ctrPercent: 0.45, position: 14.46, matches: 1200 })]);

    expect(cells(row)).toEqual([
      'blue widgets',
      'Close to page one',
      '12,345',
      '1,234',
      '0.45%',
      '14.5',
      '1,200',
      'One step from page one. Strengthen the page that already ranks: it does not need to be replaced, it needs to get better at this very search.',
    ]);
    expect(row.cells[0].strong).toBe(true);
  });

  it('names the largest matching search under the term', () => {
    const [row] = targetRows(host, [targetRow({ example: 'buy blue widgets online' })]);

    expect(row.cells[0].detail).toBe('Largest search: "buy blue widgets online"');
  });

  it('writes a term that nothing matched as "No data" and not as zeros: it was not measured, it was not found', () => {
    const [row] = targetRows(host, [missingRow()]);

    expect(cells(row).slice(0, 7)).toEqual(['widget insurance', 'Not visible', 'No data', 'No data', 'No data', 'No data', '0']);
    expect(row.cells.slice(2, 6).every((cell) => cell.missing === true)).toBe(true);
    expect(row.cells[0].detail).toBeUndefined();
  });

  it('writes the advice from its reference, with the arguments the server sent', () => {
    const rows = targetRows(host, [
      missingRow({ advice: { key: 'missing', args: { rowsFetched: 1250 } } }),
      targetRow({ advice: { key: 'pageNear', args: { path: '/products/widgets', position: 14.5, queries: 2 } }, page: targetPage() }),
      targetRow({ advice: { key: 'pagePerforming', args: { path: '/', position: 2, queries: 1 } } }),
      missingRow({ advice: { key: 'missingNoQueries' } }),
    ]);
    const advice = rows.map((row) => row.cells[7].text);

    expect(advice[0]).toContain('among the 1,250 most shown search queries');
    expect(advice[1]).toMatch(/^\/products\/widgets ranks at position 14\.5 for 2 search queries\. /);
    expect(advice[2]).toMatch(/^The home page ranks at position 2\.0 for 1 search query\. /);
    // The text that names the button: the month's search queries are missing, and "Refresh" fetches them.
    expect(advice[3]).toContain('"Refresh"');
  });

  it('keeps the order of the list: the editor puts what matters first', () => {
    const rows = targetRows(host, [targetRow({ term: 'zebra' }), targetRow({ term: 'apple' }), missingRow({ term: 'mango' })]);

    expect(rows.map((row) => row.cells[0].text)).toEqual(['zebra', 'apple', 'mango']);
  });

  it('says "Unknown" for a status it has no words for instead of showing a raw key', () => {
    const [row] = targetRows(host, [targetRow({ status: 'brand-new' })]);

    expect(row.cells[1].text).toBe('Unknown');
  });

  it('tolerates an advice with no text: the row is still there', () => {
    const [row] = targetRows(host, [targetRow({ advice: { key: 'not a key' } })]);

    expect(row.cells[7].text).toBe('');
    expect(row.cells[0].text).toBe('blue widgets');
  });
});

describe('targetColumns', () => {
  it('labels the columns, with the status and the advice in words', () => {
    expect(targetColumns(host).map((column) => column.label)).toEqual([
      'Search query',
      'Status',
      'Impressions',
      'Clicks',
      'Click-through rate',
      'Average position',
      'Matching searches',
      'What to do',
    ]);
  });

  it('right-aligns the figures and gives the advice room', () => {
    const columns = targetColumns(host);

    expect(columns.map((column) => column.numeric === true)).toEqual([false, false, true, true, true, true, true, false]);
    expect(columns[7].wide).toBe(true);
  });

  it('has one cell in every row for every column', () => {
    const rows = targetRows(host, [targetRow(), missingRow()]);

    expect(rows.every((row) => row.cells.length === targetColumns(host).length)).toBe(true);
  });
});

describe('targetsView', () => {
  it('is the table of the terms, with how many search queries the month holds', () => {
    const view = targetsView(host, targets([targetRow(), missingRow()], 250));

    expect(view.state).toBe('ready');
    expect(view.rowsFetched).toBe(250);
    expect(view.rows).toHaveLength(2);
    expect(view.terms).toEqual(['blue widgets', 'widget insurance']);
  });

  it('HIDES the table for an empty list of terms, and says so: an empty table would be headers over nothing', () => {
    for (const none of [null, undefined, targets([])]) {
      const view = targetsView(host, none);

      expect(view.state).toBe('noTerms');
      expect(view.rows).toEqual([]);
      expect(view.terms).toEqual([]);
    }
  });
});

describe('termsOf', () => {
  it('is the terms of the analysis, in the editor\'s order', () => {
    expect(termsOf(targets([targetRow({ term: 'b' }), targetRow({ term: 'a' })]))).toEqual(['b', 'a']);
    expect(termsOf(null)).toEqual([]);
  });
});

describe('historyView', () => {
  it('has the months as the columns and one row per term with its positions, one decimal, in the order of the months', () => {
    const view = historyView(host, history())!;

    expect(view.columns.map((column) => column.label)).toEqual(['Search query', 'Jul 2026', 'Aug 2026', 'Oct 2026']);
    expect(view.columns.slice(1).every((column) => column.numeric === true)).toBe(true);
    expect(view.rows.map(cells)).toEqual([
      ['blue widgets', 'No data', '18.3', '14.5'],
      ['widget repair', '4.0', '3.5', '3.0'],
    ]);
  });

  it('writes a month without a match as "No data" and mutes it: an empty cell would read as a position', () => {
    const view = historyView(host, history())!;

    expect(view.rows[0].cells[1].missing).toBe(true);
    expect(view.rows[0].cells[2].missing).toBeUndefined();
  });

  it('is absent without terms or without months: an empty table is worse than none', () => {
    expect(historyView(host, null)).toBeNull();
    expect(historyView(host, history({ rows: [] }))).toBeNull();
    expect(historyView(host, history({ months: [] }))).toBeNull();
  });

  it('keeps a row whose positions are shorter than the months: the missing cells read "No data"', () => {
    const view = historyView(host, history({ rows: [{ term: 'short', positions: [5] }] }))!;

    expect(cells(view.rows[0])).toEqual(['short', '5.0', 'No data', 'No data']);
  });
});
