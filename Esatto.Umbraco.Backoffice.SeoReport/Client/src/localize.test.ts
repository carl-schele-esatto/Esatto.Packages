import { describe, expect, it, vi } from 'vitest';
import vectors from './lang/key-vectors.json';
import { API_ERROR_CODES, GOOGLE_ERROR_CODES } from './error-codes';
import { errorKey, errorTexts, idText, lookupKey, refText, resolveId, resolveTextRef, seriesText, seriesYear, text, type Area, type Part } from './localize';

interface Vector {
  area: Area;
  baseId: string;
  part: Part;
  expected: string;
  year?: number;
}

// key-vectors.json is the set of examples the C# ReportKeys is tested against too (ReportKeysTests): the two sides cannot drift.
describe('lookupKey against the shared key vectors', () => {
  it('has rows to run', () => {
    expect((vectors as Vector[]).length).toBeGreaterThan(20);
  });

  for (const row of vectors as Vector[]) {
    it(`${row.area} ${row.baseId} ${row.part} is ${row.expected}`, () => {
      expect(lookupKey(row.area, row.baseId, row.part)).toBe(row.expected);
    });

    if (row.area === 'series') {
      it(`${row.baseId} carries ${row.year === undefined ? 'no year' : `the year ${row.year}`}`, () => {
        expect(seriesYear(row.baseId)).toBe(row.year ?? null);
      });
    }
  }

  it('covers the year-suffix rule both ways, and the rule applying to series only', () => {
    const rows = vectors as Vector[];

    expect(rows.some((r) => r.area === 'series' && r.year !== undefined)).toBe(true);
    expect(rows.some((r) => r.area === 'series' && r.year === undefined && /_\d+$/.test(r.baseId))).toBe(true);
    expect(rows.some((r) => r.area !== 'series' && /_\d{4}$/.test(r.baseId))).toBe(true);
  });
});

describe('lookupKey', () => {
  it('capitalises each word and keeps the rest, so an id that is already camelCase survives', () => {
    expect(lookupKey('annotation', 'noKeyEvents', 'Title')).toBe('annotationNoKeyEventsTitle');
    expect(lookupKey('kpi', 'organic_session_seconds', 'Label')).toBe('kpiOrganicSessionSecondsLabel');
  });

  it('strips the year only from a series id, and only when it is exactly four digits after an underscore', () => {
    expect(lookupKey('series', 'sessions_2026', 'Label')).toBe('seriesSessionsLabel');
    expect(lookupKey('series', 'sessions_20260', 'Label')).toBe('seriesSessions20260Label');
    expect(lookupKey('series', 'sessions_202', 'Label')).toBe('seriesSessions202Label');
    expect(lookupKey('kpi', 'sessions_2026', 'Label')).toBe('kpiSessions2026Label');
  });

  it('refuses an unknown area, an unknown part and an id that is not letters and digits joined by single underscores', () => {
    expect(() => lookupKey('nope' as Area, 'a', 'Label')).toThrow(RangeError);
    expect(() => lookupKey('kpi', 'a', 'Nope' as Part)).toThrow(RangeError);
    for (const bad of ['', '_a', 'a_', 'a__b', 'a b', 'a-b', 'å', 'a_b\n']) {
      expect(() => lookupKey('kpi', bad, 'Label'), JSON.stringify(bad)).toThrow(RangeError);
    }
  });
});

describe('seriesText', () => {
  it('gives the label key without the year and the year as the argument the label reads', () => {
    expect(seriesText('sessions_2026')).toEqual({ key: 'seriesSessionsLabel', args: { year: 2026 } });
    expect(seriesText('year_2024')).toEqual({ key: 'seriesYearLabel', args: { year: 2024 } });
  });

  it('gives no arguments for a series without a year', () => {
    expect(seriesText('brand')).toEqual({ key: 'seriesBrandLabel', args: undefined });
    expect(seriesText('non_brand')).toEqual({ key: 'seriesNonBrandLabel', args: undefined });
  });
});

describe('resolveTextRef', () => {
  it('looks a text up by where it sits, as Report.cs says', () => {
    const ref = { key: 'noKeyEvents', args: { period: '2026-07' } };

    expect(resolveTextRef('annotation', ref)).toEqual({
      keys: { Title: 'annotationNoKeyEventsTitle', Text: 'annotationNoKeyEventsText' },
      args: { period: '2026-07' },
    });
    expect(resolveTextRef('trend', { key: 'vsSameMonthLastYear' }).keys).toEqual({ Text: 'kpiVsSameMonthLastYearText' });
    expect(resolveTextRef('sourceNote', { key: 'gscLag' }).keys).toEqual({ Text: 'uiGscLagText' });
    expect(resolveTextRef('dataStatusNote', { key: 'noData' }).keys).toEqual({ Text: 'uiNoDataText' });
    expect(resolveTextRef('insight', { key: 'sessions' }).keys).toEqual({ Text: 'insightSessionsText' });
  });

  it('looks the analysis texts up where the analysis classes say they sit', () => {
    expect(resolveTextRef('narrative', { key: 'sources' }).keys).toEqual({ Text: 'narrativeSourcesText' });
    expect(resolveTextRef('narrative', { key: 'nonBrand' }).keys).toEqual({ Text: 'narrativeNonBrandText' });
    // An opportunity has ONE reference for both parts: its key is the kind, and the title and the text are looked up beside each other.
    expect(resolveTextRef('opportunity', { key: 'near', args: { query: 'q' } })).toEqual({
      keys: { Title: 'opportunityNearTitle', Text: 'opportunityNearText' },
      args: { query: 'q' },
    });
    expect(resolveTextRef('targetAdvice', { key: 'pageNear' }).keys).toEqual({ Text: 'targetPageNearText' });
    expect(resolveTextRef('targetAdvice', { key: 'missingNoQueries' }).keys).toEqual({ Text: 'targetMissingNoQueriesText' });
  });

  it('hands the arguments over unchanged, an empty object when there are none, and keeps explicit nulls', () => {
    expect(resolveTextRef('trend', { key: 'engagementShare' }).args).toEqual({});
    expect(resolveTextRef('insight', { key: 'x', args: { a: null, b: 0 } }).args).toEqual({ a: null, b: 0 });
  });
});

describe('resolveId', () => {
  it('looks an id up by where it sits', () => {
    expect(resolveId('kpi', 'organic_sessions')).toEqual({
      Label: 'kpiOrganicSessionsLabel',
      Description: 'kpiOrganicSessionsDescription',
    });
    expect(resolveId('segmentMetric', 'brand_clicks')).toEqual({ Label: 'kpiBrandClicksLabel' });
    expect(resolveId('chart', 'traffic_organic')).toEqual({
      Title: 'chartTrafficOrganicTitle',
      Subtitle: 'chartTrafficOrganicSubtitle',
      Description: 'chartTrafficOrganicDescription',
    });
    expect(resolveId('searchBlock', 'traffic_search')).toEqual({
      Title: 'chartTrafficSearchTitle',
      Subtitle: 'chartTrafficSearchSubtitle',
    });
    expect(resolveId('source', 'ga4')).toEqual({ Label: 'uiGa4Label' });
    expect(resolveId('source', 'gsc')).toEqual({ Label: 'uiGscLabel' });
  });
});

describe('errorKey', () => {
  it('builds the key from the code the way ErrorCodes.BaseId and ReportKeys.Lookup do', () => {
    expect(errorKey('apiDisabled', 'Title')).toBe('errorApiDisabledTitle');
    expect(errorKey('ga4PropertyInvalid', 'Text')).toBe('errorGa4PropertyInvalidText');
    expect(errorKey('searchConsoleInvalid', 'Title')).toBe('errorSearchConsoleInvalidTitle');
    expect(errorKey('periodOutOfRange', 'Text')).toBe('errorPeriodOutOfRangeText');
    expect(errorKey('unknown', 'Text')).toBe('errorUnknownText');
  });

  it('builds a well-formed key for every code the client knows', () => {
    for (const code of [...GOOGLE_ERROR_CODES, ...API_ERROR_CODES]) {
      for (const part of ['Title', 'Text'] as const) {
        expect(errorKey(code, part), code).toMatch(/^error[A-Z][A-Za-z0-9]*(Title|Text)$/);
      }
    }
  });
});

// A stand-in for what the host element gives the client: a localize controller whose term() records what it was called with.
function host() {
  const term = vi.fn((key: string, ..._args: unknown[]) => `<${key}>`);
  return { term, localizer: { localize: { term, lang: () => 'en-gb' } } };
}

describe('text', () => {
  it('looks the key up in the esattoSeoReport set', () => {
    const { term, localizer } = host();

    expect(text(localizer, 'uiGa4Label')).toBe('<esattoSeoReport_uiGa4Label>');
    expect(term).toHaveBeenCalledWith('esattoSeoReport_uiGa4Label', {});
  });

  it('passes ONE arguments object, the one it was given, and never spreads it', () => {
    const { term, localizer } = host();
    const args = { period: '2026-07', value: 12, unit: 'visits' };

    text(localizer, 'kpiAnomalousBaselineText', args);

    expect(term).toHaveBeenCalledTimes(1);
    const call = term.mock.calls[0];
    expect(call).toHaveLength(2);
    expect(call[1]).toBe(args);
  });

  it('passes an empty object for no arguments and for null, so a text that destructures them never gets null', () => {
    for (const args of [undefined, null]) {
      const { term, localizer } = host();

      text(localizer, 'x', args);

      expect(term.mock.calls[0][1]).toEqual({});
    }
  });

  it('keeps an explicit null inside the arguments: the text reads it as missing', () => {
    const { term, localizer } = host();

    text(localizer, 'x', { comparePeriod: null, compareTop3: 5 });

    expect(term.mock.calls[0][1]).toEqual({ comparePeriod: null, compareTop3: 5 });
  });
});

describe('errorTexts', () => {
  it('looks the title and the text of a problem up by its code', () => {
    const { term, localizer } = host();

    expect(errorTexts(localizer, 'apiDisabled', { api: 'searchConsole' })).toEqual({
      title: '<esattoSeoReport_errorApiDisabledTitle>',
      text: '<esattoSeoReport_errorApiDisabledText>',
    });
    expect(term.mock.calls.map((c) => c[1])).toEqual([{ api: 'searchConsole' }, { api: 'searchConsole' }]);
  });

  it('shows a code it has no words for as unknown', () => {
    const { localizer } = host();

    expect(errorTexts(localizer, 'somethingNew').title).toBe('<esattoSeoReport_errorUnknownTitle>');
    expect(errorTexts(localizer, undefined).text).toBe('<esattoSeoReport_errorUnknownText>');
  });
});

describe('refText and idText', () => {
  it('write the text of a text reference by where it sits, with its arguments', () => {
    const { term, localizer } = host();

    expect(refText(localizer, 'trend', { key: 'noComparison', args: { period: '2025-07' } })).toBe('<esattoSeoReport_kpiNoComparisonText>');
    expect(term).toHaveBeenCalledWith('esattoSeoReport_kpiNoComparisonText', { period: '2025-07' });
  });

  it('write the title of an annotation when asked for the Title part', () => {
    const { localizer } = host();

    expect(refText(localizer, 'annotation', { key: 'noKeyEvents' }, 'Title')).toBe('<esattoSeoReport_annotationNoKeyEventsTitle>');
  });

  it('write nothing for a missing reference, and for an id that cannot make a key, instead of throwing in the middle of a render', () => {
    const { term, localizer } = host();

    expect(refText(localizer, 'trend', null)).toBe('');
    expect(refText(localizer, 'trend', undefined)).toBe('');
    expect(refText(localizer, 'trend', { key: 'not valid!' })).toBe('');
    expect(idText(localizer, 'source', 'not valid!', 'Label')).toBe('');
    expect(term).not.toHaveBeenCalled();
  });

  it('write the label of an id by where it sits', () => {
    const { localizer } = host();

    expect(idText(localizer, 'kpi', 'organic_sessions', 'Label')).toBe('<esattoSeoReport_kpiOrganicSessionsLabel>');
    expect(idText(localizer, 'source', 'gsc', 'Label')).toBe('<esattoSeoReport_uiGscLabel>');
    expect(idText(localizer, 'segmentMetric', 'brand_clicks', 'Description')).toBe('');
  });
});
