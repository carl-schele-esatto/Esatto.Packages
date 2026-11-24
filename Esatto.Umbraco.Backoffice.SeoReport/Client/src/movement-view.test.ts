import { describe, expect, it } from 'vitest';
import { moversView } from './movement-view';
import { delta, move, movement } from './testing/analysis-fixtures';
import { enHost } from './testing/host';

const host = enHost();
const cells = (row: { cells: { text: string }[] }) => row.cells.map((cell) => cell.text);

describe('moversView', () => {
  it('is two lists, what was lost most and what was gained most, each a table with the page, its impressions and its position', () => {
    const view = moversView(host, movement());

    expect(view.state).toBe('ready');
    expect(view.lists.map((list) => [list.kind, list.title])).toEqual([
      ['losses', 'Lost the most'],
      ['gains', 'Gained the most'],
    ]);
    expect(view.lists[0].columns.map((column) => column.label)).toEqual([
      'Page',
      'Impressions',
      'Average position',
      'Against September 2026',
      'Against October 2025',
    ]);
  });

  it('puts BOTH comparisons on every row: the previous month and the same month a year earlier, as they can point different ways', () => {
    const [row] = moversView(host, movement({ losses: [move()] })).lists[0].rows;

    expect(cells(row)).toEqual(['/products/widgets', '750', '9.4', '-250 (-25.0%)', '+50 (+7.1%)']);
    expect(row.cells[0].strong).toBe(true);
    expect(row.cells[0].title).toBe('https://www.example.com/products/widgets');
  });

  it('writes the direction in a sign AND in the figure, never in a colour: a loss is minus, a gain is plus', () => {
    const view = moversView(host, movement());

    expect(view.lists[0].rows[0].cells[3].text.startsWith('-')).toBe(true);
    expect(view.lists[1].rows[0].cells[3].text.startsWith('+')).toBe(true);
  });

  it('says where the position went, under the change, for a page that had one', () => {
    const [row] = moversView(host, movement({ losses: [move({ position: 9.44, vsPrevious: delta({ positionBefore: 8.2 }) })] })).lists[0].rows;

    expect(row.cells[3].detail).toBe('Position 8.2 to 9.4');
  });

  it('leaves the position out when either end of it is unknown', () => {
    const rows = moversView(
      host,
      movement({
        losses: [move({ position: null }), move({ vsPrevious: delta({ positionBefore: null }) })],
      }),
    ).lists[0].rows;

    expect(rows.map((row) => row.cells[3].detail)).toEqual([undefined, undefined]);
    expect(rows[0].cells[2].text).toBe('No data');
    expect(rows[0].cells[2].missing).toBe(true);
  });

  it('calls a page that was not shown in the comparison month new, and gives it no percentage: there is nothing to divide by', () => {
    const view = moversView(host, movement({ gains: [move({ vsPrevious: delta({ before: 0, delta: 300, changePercent: null, positionBefore: null }) })] }));

    expect(view.lists[1].rows[0].cells[3].text).toBe('+300 (new)');
  });

  it('shows a comparison month that is not archived as no column at all, not as zeros: the other basis does not creep in', () => {
    const view = moversView(host, movement({ yearAgo: null, losses: [move({ vsYearAgo: null })], gains: [move({ vsYearAgo: null })] }));

    expect(view.lists[0].columns.map((column) => column.label)).toEqual(['Page', 'Impressions', 'Average position', 'Against September 2026']);
    expect(view.lists[0].rows[0].cells).toHaveLength(4);
  });

  it('can have only the year comparison, for the oldest month of an archive', () => {
    const view = moversView(host, movement({ previous: null, rankedBy: 'yearAgo', losses: [move({ vsPrevious: null })], gains: [] }));

    expect(view.lists[0].columns.map((column) => column.label)).toEqual(['Page', 'Impressions', 'Average position', 'Against October 2025']);
    expect(cells(view.lists[0].rows[0])).toEqual(['/products/widgets', '750', '9.4', '+50 (+7.1%)']);
  });

  it('shows "No data" in a comparison cell the row has no figure for', () => {
    const [row] = moversView(host, movement({ losses: [move({ vsYearAgo: null })] })).lists[0].rows;

    expect(row.cells[4].text).toBe('No data');
    expect(row.cells[4].missing).toBe(true);
  });

  it('says what the lists are sorted on: the previous month, normally', () => {
    const { ranked } = moversView(host, movement({ rankedBy: 'previous' }));

    expect(ranked).toContain('sorted on the change against September 2026, the previous month');
    expect(ranked).toContain('the same basis every month');
  });

  it('says what the lists are sorted on when the previous month is not in the archive: the year comparison', () => {
    const { ranked } = moversView(host, movement({ previous: null, rankedBy: 'yearAgo' }));

    expect(ranked).toContain('sorted on the change against October 2025');
    expect(ranked).toContain('previous month is not in the archive');
  });

  it('leaves a list out when it has no rows, and says so when neither has', () => {
    const lossesOnly = moversView(host, movement({ gains: [] }));
    const nothing = moversView(host, movement({ losses: [], gains: [] }));

    expect(lossesOnly.lists.map((list) => list.kind)).toEqual(['losses']);
    expect(nothing.state).toBe('empty');
    expect(nothing.lists).toEqual([]);
  });

  it('is "none" when there is no movement: nothing to compare with yet', () => {
    expect(moversView(host, null).state).toBe('none');
    expect(moversView(host, undefined).state).toBe('none');
    // A result with no comparison month at all would be rows with nothing to say.
    expect(moversView(host, movement({ previous: null, yearAgo: null })).state).toBe('none');
  });

  it('writes a page by its address when it has no path, and keeps the order of the server', () => {
    const view = moversView(host, movement({ losses: [move({ path: '', url: 'https://www.example.com/b' }), move({ path: '/a' })] }));

    expect(view.lists[0].rows.map((row) => row.cells[0].text)).toEqual(['https://www.example.com/b', '/a']);
  });
});
