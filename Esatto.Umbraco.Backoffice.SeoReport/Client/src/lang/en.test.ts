import { describe, expect, it } from 'vitest';
import en from './en';

// A text with arguments is a function of one object, which the client fills from a TextRef's "args". The server leaves
// "args" out when a text has none to send (a missing date, say), and Umbraco calls a function-valued term with whatever
// it was given, so every function must cope with no object at all and with an empty one, and still read as English.
const texts = Object.entries(en.esattoSeoReport as Record<string, unknown>);
const functions = texts.filter(([, value]) => typeof value === 'function') as [string, (args?: unknown) => unknown][];

// What a text shows when it printed a missing value instead of leaving it out.
const leaked = /undefined|null|NaN|Invalid Date|\[object/;

describe('en.ts', () => {
  it('has function-valued texts to check', () => {
    expect(functions.length).toBeGreaterThan(0);
  });

  for (const [key, text] of functions) {
    for (const [name, args] of [['no arguments', undefined], ['an empty object', {}]] as const) {
      it(`${key} reads as English with ${name}`, () => {
        const result = text(args);

        expect(typeof result).toBe('string');
        expect((result as string).trim()).not.toBe('');
        expect(result).not.toMatch(leaked);
      });
    }
  }

  it('every text is a string or a function', () => {
    for (const [key, value] of texts) expect(['string', 'function'], key).toContain(typeof value);
  });
});

// The analysis texts (opportunities, target queries): the server sends a key and raw figures, and these entries carry the
// wording that used to be built on the server.
describe('en.ts analysis texts', () => {
  const text = en.esattoSeoReport;

  it('names the home page instead of starting a sentence with a slash', () => {
    const advice = text.targetPageDeepText({ path: '/', position: 30.5, queries: 1 });

    expect(advice.startsWith('The home page ranks at position 30.5 for 1 search query. ')).toBe(true);
    expect(advice.startsWith('/')).toBe(false);
  });

  it('writes any other path as it is and counts the queries', () => {
    expect(text.targetPageNearText({ path: '/products/widgets', position: 14, queries: 2 })).toMatch(
      /^\/products\/widgets ranks at position 14\.0 for 2 search queries\. /,
    );
  });

  it('adds the click-through rate to the title advice only', () => {
    expect(text.targetPageTitleText({ path: '/a', position: 6, queries: 1, ctrPercent: 0.2 })).toContain(
      'with a click-through rate of 0.2%',
    );
    expect(text.targetPageDeepText({ path: '/a', position: 30, queries: 1 })).not.toContain('click-through');
  });

  it('says how many search queries were examined', () => {
    expect(text.targetMissingText({ rowsFetched: 1250 })).toContain('among the 1,250 most shown search queries');
    expect(text.targetMissingText()).toContain('among the most shown search queries');
  });

  it('points to the Refresh button by its name when the month has no search queries', () => {
    expect(text.targetMissingNoQueriesText).toContain('"Refresh"');
  });

  it('writes an opportunity from the query and the figures', () => {
    expect(text.opportunityNearTitle({ query: 'blue widgets' })).toBe('"blue widgets" is close to page one');
    expect(text.opportunityNearText({ position: 12.5, impressions: 1 })).toMatch(/^Position 12\.5 with 1 impression\. /);
  });

  it('names both competing pages with their figures', () => {
    const body = text.opportunityCannibalText({
      firstPath: '/products/widgets',
      firstPosition: 8,
      firstImpressions: 1200,
      secondPath: '/blog/blue-widgets',
      secondPosition: 18.5,
      secondImpressions: 300,
    });

    expect(body).toContain('/products/widgets (position 8.0, 1,200 impressions)');
    expect(body).toContain('/blog/blue-widgets (position 18.5, 300 impressions)');
  });

  it('has a label for every status code the server sends', () => {
    const labels = text as unknown as Record<string, unknown>;

    for (const status of ['missing', 'deep', 'near', 'title', 'performing']) {
      expect(labels[`target${status[0].toUpperCase()}${status.slice(1)}Label`], status).toEqual(expect.any(String));
    }
  });
});

// The narrative statements and the daily chart's texts: the server sends a key and raw figures (null for a part it has nothing
// to say about), and these entries carry the wording that used to be built on the server.
describe('en.ts narrative texts', () => {
  const text = en.esattoSeoReport;

  it('sets Search Console against GA4 and blames the measurement when they disagree', () => {
    const said = text.narrativeSourcesText({ clicks: 120, clicksChange: 10, sessions: 80, sessionsChange: -20, agree: false });

    expect(said).toContain('Search Console shows 120 clicks (+10.0%), GA4 shows 80 visits from search engines (-20.0%).');
    expect(said).toContain('different directions');
    expect(said).toContain('measurement rather than traffic');
  });

  it('does not hint at a measurement error when the sources agree', () => {
    const said = text.narrativeSourcesText({ clicks: 120, clicksChange: 10, sessions: 80, sessionsChange: 9, agree: true });

    expect(said).toContain('point the same way');
    expect(said).not.toContain('measurement rather than traffic');
    expect(said).not.toContain('different directions');
  });

  it('counts one click and one visit in the singular and never shows a negative zero', () => {
    const said = text.narrativeSourcesText({ clicks: 1, clicksChange: -0.04, sessions: 1, sessionsChange: 0.04, agree: true });

    expect(said).toContain('shows 1 click (+0.0%), GA4 shows 1 visit from search engines (+0.0%)');
    expect(said).not.toContain('-0.0');
  });

  it('says the non-brand share with both figures', () => {
    expect(text.narrativeNonBrandText({ clicks: 20, total: 120, percent: 16.7 })).toMatch(
      /^Non-brand accounts for 20 of 120 clicks \(16\.7%\)\. Growth has to come from there/,
    );
  });

  it('compares the top-three share with the same month last year without a caveat', () => {
    const said = text.narrativeVisibilityText({
      top3: 20,
      total: 200,
      percent: 10,
      comparePeriod: '2025-07',
      compareTop3: 10,
      comparePercent: 5,
      yearOnYear: true,
    });

    expect(said).toBe('20 of your 200 most shown search queries rank in the top 3 (10.0%). Against 10 (5.0%) in July 2025.');
  });

  it('says that the comparison month is only the nearest earlier one when it is not year on year', () => {
    const said = text.narrativeVisibilityText({
      top3: 20,
      total: 200,
      percent: 10,
      comparePeriod: '2026-06',
      compareTop3: 10,
      comparePercent: 5,
      yearOnYear: false,
    });

    expect(said).toContain('in June 2026, the nearest earlier month in the archive');
    expect(said).toContain('Season can therefore explain part of the movement.');
  });

  it('names the best month only when the server sends one', () => {
    const said = text.narrativeVisibilityText({
      top3: 20,
      total: 200,
      percent: 10,
      comparePeriod: '2025-07',
      compareTop3: 10,
      comparePercent: 5,
      yearOnYear: true,
      bestPeriod: '2026-01',
      bestTop3: 40,
      bestPercent: 20,
    });

    expect(said).toContain(' The best month in the archive is January 2026 with 40 (20.0%).');
  });

  it('leaves out a comparison and a best month the server sent as null', () => {
    const said = text.narrativeVisibilityText({
      top3: 20,
      total: 200,
      percent: 10,
      comparePeriod: null,
      compareTop3: null,
      comparePercent: null,
      yearOnYear: null,
      bestPeriod: null,
      bestTop3: null,
      bestPercent: null,
    } as never);

    expect(said).toBe('20 of your 200 most shown search queries rank in the top 3 (10.0%).');
  });

  it('writes both comparisons of the biggest drop, each with its own direction, and says the noun once', () => {
    const said = text.narrativeMovementText({
      path: '/example/page',
      yearAgoPeriod: '2025-07',
      yearAgoDelta: -6000,
      previousPeriod: '2026-06',
      previousDelta: 300,
      position: 35.5,
      positionBefore: 8.5,
    });

    expect(said).toBe(
      'The biggest drop is /example/page: 6,000 fewer impressions than July 2025 and 300 more than June 2026. The position went from 8.5 to 35.5.',
    );
  });

  it('names the impressions on the first comparison that is there', () => {
    const said = text.narrativeMovementText({ path: '/example/page', previousPeriod: '2026-06', previousDelta: -1 });

    expect(said).toBe('The biggest drop is /example/page: 1 fewer impression than June 2026.');
  });

  it('names the home page instead of starting with a slash, and leaves out a position that is not there', () => {
    const said = text.narrativeMovementText({
      path: '/',
      previousPeriod: '2026-06',
      previousDelta: -400,
      position: null,
      positionBefore: null,
    } as never);

    expect(said).toBe('The biggest drop is the home page: 400 fewer impressions than June 2026.');
  });

  it('says which one month the movement is compared with', () => {
    expect(text.narrativeSeasonText({ period: '2026-06' })).toMatch(
      /^The movement is only compared with June 2026, because the same month last year is not in the archive\./,
    );
  });

  it('sets the key events against the visits, in the singular and the plural', () => {
    expect(text.narrativeConversionText({ sessions: 30, events: 1 })).toBe('30 visits from search engines led to 1 key event.');
    expect(text.narrativeConversionText({ sessions: 1, events: 2 })).toBe('1 visit from search engines led to 2 key events.');
  });
});

// The problems the server names by a code (ErrorCodes): what happened, what to do, who to ask. The arguments are "api" and "source"
// from the server and "email" from the client, so each text must also read well when the client could not add the address, and
// when a value arrives as null.
describe('en.ts error texts', () => {
  const text = en.esattoSeoReport as unknown as Record<string, unknown>;
  const codes = [
    'notConfigured',
    'keyUnreadable',
    'keyInvalid',
    'apiDisabled',
    'noAccess',
    'propertyNotFound',
    'noData',
    'quota',
    'unknown',
    'ga4PropertyInvalid',
    'searchConsoleInvalid',
  ];

  // The key the client builds from a code: "error", the code with a capital first letter, the part.
  const key = (code: string, part: 'Title' | 'Text') => `error${code[0].toUpperCase()}${code.slice(1)}${part}`;

  const render = (code: string, part: 'Title' | 'Text', args?: unknown): string => {
    const entry = text[key(code, part)];
    return typeof entry === 'function' ? (entry as (a?: unknown) => string)(args) : (entry as string);
  };

  it('has a title and a text for every code', () => {
    for (const code of codes) {
      for (const part of ['Title', 'Text'] as const) expect(text[key(code, part)], key(code, part)).toBeDefined();
    }
  });

  it('reads as English with no arguments, an empty object and null values, and never prints a missing value', () => {
    const leaked = /undefined|null|NaN|Invalid Date|\[object/;

    for (const code of codes) {
      for (const part of ['Title', 'Text'] as const) {
        for (const args of [undefined, {}, { api: null, source: null, email: null }]) {
          const said = render(code, part, args);

          expect(said.trim(), key(code, part)).not.toBe('');
          expect(said, key(code, part)).not.toMatch(leaked);
        }
      }
    }
  });

  it('names the service account address the person has to add, when the client knows it', () => {
    const email = 'svc@proj.iam.gserviceaccount.com';

    expect(render('noAccess', 'Text', { source: 'ga4', email })).toContain(`add ${email} with the Viewer role`);
    expect(render('noAccess', 'Text', { source: 'searchConsole', email })).toContain(`add ${email} as a user`);
    expect(render('propertyNotFound', 'Text', { source: 'searchConsole', email })).toContain(email);
    expect(render('noAccess', 'Text', { source: 'ga4' })).toContain("the service account's e-mail address");
  });

  it('says where to give the access, per source', () => {
    expect(render('noAccess', 'Title', { source: 'ga4' })).toBe('No access to GA4');
    expect(render('noAccess', 'Title', { source: 'searchConsole' })).toBe('No access to Search Console');
    expect(render('noAccess', 'Text', { source: 'ga4' })).toContain('Property access management');
    expect(render('noAccess', 'Text', { source: 'searchConsole' })).toContain('Users and permissions');
  });

  it('names the API that is switched off and where to switch it on', () => {
    expect(render('apiDisabled', 'Title', { api: 'analyticsData' })).toBe('The Google Analytics Data API is switched off');
    expect(render('apiDisabled', 'Text', { api: 'analyticsData' })).toContain('/apis/library/analyticsdata.googleapis.com');
    expect(render('apiDisabled', 'Text', { api: 'searchConsole' })).toContain('/apis/library/searchconsole.googleapis.com');
    expect(render('apiDisabled', 'Title')).toBe('A Google API is switched off');
  });

  it('says the Search Console property has to be written in its exact form', () => {
    const said = render('propertyNotFound', 'Text', { source: 'searchConsole' });

    expect(said).toContain('sc-domain:example.com');
    expect(said).toContain('trailing slash');
    expect(said).toContain('http or https');
    expect(render('propertyNotFound', 'Title', { source: 'ga4' })).toBe('GA4 property not found');
  });

  it('covers both "not found" and "no access" in the Search Console property text, with the address to add', () => {
    const said = render('propertyNotFound', 'Text', { source: 'searchConsole', email: 'svc@proj.iam.gserviceaccount.com' });

    expect(said).toContain('does not know the property you entered, or the service account cannot see it');
    expect(said).toContain('svc@proj.iam.gserviceaccount.com has been added as a user');
  });

  it('asks the person to check the GA4 property ID too, not only to add the service account', () => {
    const said = render('noAccess', 'Text', { source: 'ga4', email: 'svc@proj.iam.gserviceaccount.com' });

    expect(said).toContain('add svc@proj.iam.gserviceaccount.com with the Viewer role');
    expect(said).toContain('Property details');
    expect(said).toContain('not an account or stream ID');
    expect(render('noAccess', 'Text', { source: 'searchConsole' })).not.toContain('stream ID');
  });

  it('tells a key Google no longer accepts what to do, whether the key is pasted or comes from configuration', () => {
    const said = render('keyInvalid', 'Text');

    expect(said).toContain('deleted or disabled');
    expect(said).toContain('Settings tab');
    expect(said).toContain('site configuration');
  });

  it('does not ask an editor to edit the site configuration: a configured key is the developer\'s to replace', () => {
    const said = render('keyInvalid', 'Text');

    expect(said).toContain('paste the whole JSON file on the Settings tab');
    expect(said).toContain('ask your developer to replace it there');
    expect(said).not.toContain('update it there');
  });

  it('says Search Console has no data for a month it returned nothing for, and not that it lags', () => {
    const noData = en.esattoSeoReport.uiGscNoDataText;

    expect(noData({ period: '2026-06', dataThrough: '2026-06-12' })).toMatch(/^Search Console has no data for .*2026\.( Final data through .*)?$/);
    expect(noData({ period: '2026-06' })).not.toContain('lag');
    expect(noData({ period: null, dataThrough: null })).toBe('Search Console has no data for this month.');
  });

  it('does not blame Google for a failure no code names: it can as well be the site or its database', () => {
    expect(text.errorUnknownText).toBe(
      'The request failed for a reason the report cannot name. Try again in a moment. If it keeps failing, ask your developer to check the site log.',
    );
  });

  it('calls a source without data connected, not broken, and asks for nothing to be fixed', () => {
    expect(render('noData', 'Title', { source: 'searchConsole' })).toBe('Search Console is connected, but has no data yet');
    expect(render('noData', 'Text', { source: 'ga4' })).toContain('There is nothing to fix here');
    expect(render('noData', 'Text', { source: 'searchConsole' })).toContain('The connection works');
  });

});

describe('en.ts daily chart texts', () => {
  const text = en.esattoSeoReport;

  it('titles the chart with the calendar month or the length of the rolling window', () => {
    expect(text.chartDailyTrafficTitle({ period: '2026-07', windowDays: 30 })).toBe('July 2026');
    expect(text.chartDailyTrafficTitle({ period: null, windowDays: 30 } as never)).toBe('The last 30 days');
    expect(text.chartDailyTrafficTitle()).toBe('Visits per day');
  });

  it('names what the dashed comparison is the average of', () => {
    expect(text.chartDailyTrafficDescription({ period: null, windowDays: 30, provisionalDays: 2 } as never)).toContain(
      'the average of the previous 30 days as a dashed comparison',
    );
    expect(text.chartDailyTrafficDescription({ period: '2026-07', windowDays: 30, provisionalDays: 2 })).toContain(
      'the average of the previous month as a dashed comparison',
    );
  });

  it('carries both caveats that would otherwise make the curve misleading', () => {
    const said = text.chartDailyTrafficDescription({ windowDays: 30, provisionalDays: 2 });

    expect(said).toContain('The last 2 days are preliminary');
    expect(said).toContain('cookie consent');
    expect(said).toContain('a floor, not the whole picture');
  });

  it('agrees the verb with the number of provisional days', () => {
    expect(text.chartDailyTrafficDescription({ provisionalDays: 1 })).toContain('The last day is preliminary');
    expect(text.chartDailyTrafficDescription({ provisionalDays: 3 })).toContain('The last 3 days are preliminary');
  });

  it('does not call the last days preliminary when no day can still be revised: no count, none, or zero', () => {
    // A finished month older than the margin has provisionalDays null; a running month on its first day has no days at all.
    expect(text.chartDailyTrafficDescription()).not.toContain('preliminary');
    expect(text.chartDailyTrafficDescription({ provisionalDays: null } as never)).not.toContain('preliminary');
    expect(text.chartDailyTrafficDescription({ provisionalDays: 0 })).not.toContain('preliminary');
    expect(text.chartDailyTrafficDescription({ provisionalDays: 0 })).not.toContain('can be revised');
  });

  it('still carries the other caveats and the hint without a provisional count', () => {
    const said = text.chartDailyTrafficDescription({ period: '2026-03', windowDays: 31, provisionalDays: null } as never);

    expect(said).toContain('cookie consent');
    expect(said).toContain('a floor, not the whole picture');
    expect(said).toContain('Click a day in the chart');
  });

  it('labels both series', () => {
    expect(text.seriesDailyLabel).toBe('Visits per day');
    expect(text.seriesBaselineLabel).toBe('Average of the preceding period');
  });
});

// The Analysis tab's own texts: the client writes these (the server sends none of them), with figures it has already formatted.
describe('en.ts analysis tab texts', () => {
  const text = en.esattoSeoReport;

  it('counts the note against its limit, and says how far over it is, in the singular and the plural', () => {
    expect(text.uiNoteCounterText({ length: 1234, max: 1000 })).toBe('1,234 of 1,000 characters');
    expect(text.uiNoteTooLongText({ over: 1, max: 1000 })).toContain('The note is 1 character too long');
    expect(text.uiNoteTooLongText({ over: 25, max: 1000 })).toContain('The note is 25 characters too long');
    expect(text.uiNoteTooLongText({ over: 25, max: 1000 })).toContain('The longest note is 1,000 characters');
  });

  it('counts the target queries against their limits, and agrees the verb with how many are too long', () => {
    expect(text.uiTermsCounterText({ count: 3, max: 50 })).toBe('3 of 50 target queries');
    expect(text.uiTermsTooManyText({ max: 50 })).toBe('Only the first 50 are kept.');
    expect(text.uiTermsTooLongText({ count: 1, max: 100 })).toBe('1 target query is longer than 100 characters and will be left out.');
    expect(text.uiTermsTooLongText({ count: 2, max: 100 })).toBe('2 target queries are longer than 100 characters and will be left out.');
  });

  it('names the status "not visible" in the help and says how many search queries it looked in, when it knows', () => {
    expect(text.uiTargetsHelpText({ label: 'Not visible', rowsFetched: 1250 })).toContain('"Not visible" means the search is not among the 1,250 most shown search queries');
    expect(text.uiTargetsHelpText({ label: 'Not visible', rowsFetched: null })).toContain('not among the most shown search queries');
  });

  it('writes a change with its sign, its percentage and where the position went, and a page that is new without a percentage', () => {
    expect(text.uiMoveDeltaText({ delta: '-250', percent: '-25.0%' })).toBe('-250 (-25.0%)');
    expect(text.uiMoveNewText({ delta: '+300' })).toBe('+300 (new)');
    expect(text.uiMovePositionText({ before: '8.2', now: '9.4' })).toBe('Position 8.2 to 9.4');
  });

  it('says which month the lists are sorted on, and why when it is the year', () => {
    expect(text.uiMovementRankedPreviousText({ period: '2026-09' })).toContain('against September 2026, the previous month');
    expect(text.uiMovementRankedYearAgoText({ period: '2025-10' })).toContain('against October 2025, because the previous month is not in the archive');
  });

  it('has the words for every effort and for both ends of the comparison columns', () => {
    expect([text.uiEffortSmallLabel, text.uiEffortMediumLabel, text.uiEffortLargeLabel]).toEqual(['Small effort', 'Medium effort', 'Large effort']);
    expect(text.uiMoveAgainstLabel({ period: '2026-09' })).toBe('Against September 2026');
  });
});

// The Settings tab's own texts: the client writes these, with the service account's address and the values of the form.
describe('en.ts settings tab texts', () => {
  const text = en.esattoSeoReport;
  const email = 'svc@proj.iam.gserviceaccount.com';

  it('names the service account of a saved key, a configured key and a key in the box, and reads well without it', () => {
    expect(text.uiKeySavedText({ email })).toBe(`Key saved · service account ${email}`);
    expect(text.uiKeyConfiguredText({ email })).toBe(`Set in configuration · service account ${email}`);
    expect(text.uiKeyDraftText({ email })).toContain(email);
    expect(text.uiKeySavedText()).toBe('Key saved');
  });

  it('puts the address to add into the step that grants access, with where to add it in both products', () => {
    const said = text.uiGuideStepAccessText({ email });

    expect(said).toContain(email);
    expect(said).toContain('Viewer role');
    expect(said).toContain('Users and permissions');
    expect(text.uiGuideStepAccessText()).toContain('client_email');
  });

  it('counts the brand terms against both limits, and names the terms that will be left out', () => {
    expect(text.uiBrandTermsCounterText({ count: 3, max: 50, maxLength: 100 })).toBe('3 of 50 brand terms, up to 100 characters each');
    expect(text.uiBrandTermsTooLongText({ count: 1, max: 100 })).toBe('1 brand term is longer than 100 characters and will be left out.');
    expect(text.uiBrandTermsTooLongText({ count: 2, max: 100 })).toBe('2 brand terms are longer than 100 characters and will be left out.');
  });

  it('does not say the archive keeps only 13 months: automatic fetching looks after the latest 13, older months stay', () => {
    expect(text.uiAutoFetchHelpText).not.toContain('keeps the last 13 months');
    expect(text.uiAutoFetchHelpText).toContain('latest 13 months');
    expect(text.uiAutoFetchHelpText).toContain('Older months stay in the archive.');
  });

  it('says that brand terms apply from then on, and names the Refresh button by its label', () => {
    expect(text.uiBrandTermsChangeText).toBe('Changing brand terms applies to months fetched from then on; use "Refresh" on a month to update it.');
  });

  it('says what the Search Console property will be saved as, and nothing for an empty one', () => {
    expect(text.uiSearchConsoleSentText({ value: 'sc-domain:example.com' })).toBe('Will be saved as sc-domain:example.com');
    expect(text.uiSearchConsoleSentText({ value: '' })).toBe('Nothing is saved for the property.');
  });

  it('gives the size limit of a key in KB', () => {
    expect(text.uiKeyTooLargeText({ max: 16 })).toContain('larger than 16 KB');
  });
});
