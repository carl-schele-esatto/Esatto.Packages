import { describe, expect, it } from 'vitest';
import {
  TOP_LIST_ROWS,
  pageColumns,
  pageRows,
  queryColumns,
  queryRows,
  segmentColumns,
  segmentRows,
  sourceColumns,
  sourceRows,
} from './table-rows';
import { landingPage, query, searchBlock, segment } from './testing/fixtures';
import { enHost } from './testing/host';

const host = enHost();

const many = <T>(make: (index: number) => T, count: number): T[] => Array.from({ length: count }, (_, index) => make(index));
const texts = (row: { cells: { text: string }[] }) => row.cells.map((cell) => cell.text);

describe('the row limit', () => {
  it('is 25: the report carries more rows than a top list shows', () => {
    expect(TOP_LIST_ROWS).toBe(25);
  });
});

describe('queryRows', () => {
  it('writes a search query with its clicks, impressions, click-through rate and average position', () => {
    const rows = queryRows(host, searchBlock({ queries: [query({ query: 'blue widgets', clicks: 1234, impressions: 56789, ctr_percent: 2.17, position: 4 })] }));

    expect(rows).toHaveLength(1);
    expect(texts(rows[0])).toEqual(['blue widgets', '1,234', '56,789', '2.17%', '4.0']);
    expect(rows[0].cells[0].strong).toBe(true);
  });

  it('gives a query its full text as the title: the table cuts a long name with an ellipsis, and the whole of it is read on hover', () => {
    const long = 'how to choose the right blue widget for a small kitchen with a very low ceiling';

    const rows = queryRows(host, searchBlock({ queries: [query({ query: long }), query({ query: 'short' })] }));

    expect(rows.map((row) => [row.cells[0].text, row.cells[0].title])).toEqual([[long, long], ['short', 'short']]);
  });

  it('keeps the click-through rate to two decimals: it is already a percentage, and 0.45% is not 0.5%', () => {
    const rows = queryRows(host, searchBlock({ queries: [query({ ctr_percent: 0.45 }), query({ ctr_percent: 12.3 }), query({ ctr_percent: 0 })] }));

    expect(rows.map((row) => row.cells[3].text)).toEqual(['0.45%', '12.3%', '0%']);
  });

  it('writes the average position with one decimal, so a column lines up', () => {
    const rows = queryRows(host, searchBlock({ queries: [query({ position: 3 }), query({ position: 14.46 })] }));

    expect(rows.map((row) => row.cells[4].text)).toEqual(['3.0', '14.5']);
  });

  it('cuts the list at the limit and keeps the order the report has (most clicked first)', () => {
    const queries = many((index) => query({ query: `query ${index}`, clicks: 1000 - index }), 40);

    const rows = queryRows(host, searchBlock({ queries }));

    expect(rows).toHaveLength(25);
    expect(rows[0].cells[0].text).toBe('query 0');
    expect(rows[24].cells[0].text).toBe('query 24');
  });

  it('takes a limit of its own', () => {
    expect(queryRows(host, searchBlock({ queries: many(() => query(), 40) }), 10)).toHaveLength(10);
    expect(queryRows(host, searchBlock({ queries: many(() => query(), 3) }), 10)).toHaveLength(3);
    expect(queryRows(host, searchBlock({ queries: many(() => query(), 3) }), 0)).toHaveLength(0);
  });

  it('has no rows for a report without the list, for an empty one and for no search block', () => {
    expect(queryRows(host, searchBlock({ queries: null }))).toEqual([]);
    expect(queryRows(host, searchBlock({ queries: [] }))).toEqual([]);
    expect(queryRows(host, searchBlock())).toEqual([]);
    expect(queryRows(host, null)).toEqual([]);
  });

  it('writes a figure the archive lacks as "No data", never a dash or a zero', () => {
    const rows = queryRows(host, searchBlock({ queries: [{ ...query(), clicks: null as never, position: Number.NaN }] }));

    expect(texts(rows[0])).toEqual(['blue widgets', 'No data', '4,800', '2.5%', 'No data']);
    expect(rows[0].cells[1].missing).toBe(true);
    expect(rows[0].cells[0].missing).toBeUndefined();
  });

  it('writes the numbers for the language of the page', () => {
    const rows = queryRows(enHost('sv'), searchBlock({ queries: [query({ clicks: 1234, ctr_percent: 2.17, position: 4 })] }));

    expect(texts(rows[0]).slice(1)).toEqual(['1 234', '4 800', '2,17 %', '4,0']);
  });

  it('has the five columns of the table, the last four numeric', () => {
    const columns = queryColumns(host);

    expect(columns.map((column) => column.label)).toEqual(['Search query', 'Clicks', 'Impressions', 'Click-through rate', 'Average position']);
    expect(columns.map((column) => column.numeric === true)).toEqual([false, true, true, true, true]);
  });
});

describe('pageRows', () => {
  it('writes the shortened path, with the whole address as its title: shown as text, not as a link', () => {
    const rows = pageRows(host, searchBlock({ pages: [landingPage({ clicks: 80, impressions: 2400, ctr_percent: 3.33, position: 5.1 })] }));

    expect(texts(rows[0])).toEqual(['/widgets/blue', '80', '2,400', '3.33%', '5.1']);
    expect(rows[0].cells[0].title).toBe('https://www.example.com/widgets/blue');
    expect(rows[0].cells[0].strong).toBe(true);
  });

  it('falls back to the whole address when the report has no short form', () => {
    const rows = pageRows(host, searchBlock({ pages: [landingPage({ path: null }), landingPage({ path: '' })] }));

    expect(rows.map((row) => row.cells[0].text)).toEqual(['https://www.example.com/widgets/blue', 'https://www.example.com/widgets/blue']);
  });

  it('cuts at the same limit as the queries, and tolerates a missing list', () => {
    expect(pageRows(host, searchBlock({ pages: many(() => landingPage(), 60) }))).toHaveLength(25);
    expect(pageRows(host, searchBlock({ pages: null }))).toEqual([]);
    expect(pageRows(host, undefined)).toEqual([]);
  });

  it('has a column for the page where the query table has one for the query', () => {
    expect(pageColumns(host).map((column) => column.label)).toEqual(['Page', 'Clicks', 'Impressions', 'Click-through rate', 'Average position']);
  });
});

describe('segmentRows', () => {
  it('has a row per segment and then the unfiltered total, each named by its series label', () => {
    const search = searchBlock({ segments: [segment('brand'), segment('non_brand')], total: segment('total') });

    const rows = segmentRows(host, search);

    expect(rows.map((row) => row.cells[0].text)).toEqual(['Brand', 'Non-brand', 'Total (unfiltered)']);
  });

  it('writes the measures by their format, the rate with its two decimals, and the rise as a pill', () => {
    const rows = segmentRows(host, searchBlock({ segments: [segment('brand', { clicks: 12345, ctr: 12.28, position: 7.4 })] }));

    expect(texts(rows[0])).toEqual(['Brand', '12,345', '9,000', '12.28%', '7.4']);
    expect(rows[0].cells[1].trend?.pill).toEqual({ direction: 'up', arrow: '↑', words: 'Up', figure: '12.3%' });
    expect(rows[0].cells[2].trend ?? null).toBeNull();
  });

  it('writes a measure that is null as "No data"', () => {
    const rows = segmentRows(host, searchBlock({ segments: [segment('brand', { clicks: null })] }));

    expect(rows[0].cells[1]).toMatchObject({ text: 'No data', missing: true });
  });

  it('is only the total when there are no segments (no brand terms)', () => {
    const rows = segmentRows(host, searchBlock({ segments: [], total: segment('total') }));

    expect(rows.map((row) => row.cells[0].text)).toEqual(['Total (unfiltered)']);
  });

  it('is nothing without a search block, or with neither segments nor a total', () => {
    expect(segmentRows(host, null)).toEqual([]);
    expect(segmentRows(host, searchBlock({ segments: [], total: null }))).toEqual([]);
  });

  it('pads a segment that has fewer measures than the first, so the columns stay in line', () => {
    const short = { key: 'non_brand', metrics: segment('non_brand').metrics.slice(0, 2) };

    const rows = segmentRows(host, searchBlock({ segments: [segment('brand'), short] }));

    expect(rows[1].cells).toHaveLength(5);
    expect(rows[1].cells[4]).toMatchObject({ text: 'No data', missing: true });
  });
});

describe('segmentColumns', () => {
  it('names the columns by the measures of the first segment, the first column Segment', () => {
    const columns = segmentColumns(host, searchBlock({ segments: [segment('brand'), segment('non_brand')] }));

    expect(columns.map((column) => column.label)).toEqual(['Segment', 'Clicks', 'Impressions', 'Click-through rate', 'Average position']);
    expect(columns.map((column) => column.numeric === true)).toEqual([false, true, true, true, true]);
  });

  it('takes them from the total when there are no segments', () => {
    const columns = segmentColumns(host, searchBlock({ segments: [], total: segment('total') }));

    expect(columns).toHaveLength(5);
  });
});

describe('sourceRows', () => {
  it('writes where a day brought visits from: channel, source and the number of visits', () => {
    const rows = sourceRows(host, [{ channel: 'Organic Search', source: 'google', sessions: 1234 }]);

    expect(texts(rows[0])).toEqual(['Organic Search', 'google', '1,234']);
  });

  it('writes an empty channel or source as "No data"', () => {
    const rows = sourceRows(host, [{ channel: '', source: '', sessions: 3 }]);

    expect(texts(rows[0])).toEqual(['No data', 'No data', '3']);
    expect(rows[0].cells[0].missing).toBe(true);
  });

  it('has three columns, the last numeric', () => {
    expect(sourceColumns(host).map((column) => [column.label, column.numeric === true])).toEqual([
      ['Channel', false],
      ['Source', false],
      ['Visits', true],
    ]);
  });
});
