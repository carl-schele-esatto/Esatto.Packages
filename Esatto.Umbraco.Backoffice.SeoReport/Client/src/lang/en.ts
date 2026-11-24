// English texts (the fallback for every other language). Looked up as esattoSeoReport_<key>.
//
// A value is a string, or a function when the text needs values. A function takes ONE object whose property names are
// the argument names the server sends in a TextRef's "args" (the client passes args as they are), so the order never
// matters; the comment above each function lists them. Arguments arrive raw: numbers unformatted, periods "YYYY-MM",
// dates "YYYY-MM-DD"; each text formats them below. Keys of report texts follow ReportKeys (area + PascalCase id + part);
// EnglishTextsTests reads this file and fails when a registered key is missing or a text reads an argument the server
// never sends, so keep one entry per line start, "key: value", with a function's argument object on that same line.
//
// Every function defaults its object (" = {}") and reads well without it: the server leaves "args" out when it has
// nothing to send, and then the text says the same thing without the figures. en.test.ts calls each one with none.

// Whether every value a sentence needs arrived; when one did not, the text falls back to the sentence without figures. An
// argument the server sent as null (a part of a statement it had nothing to say about) counts as missing, like an absent one.
const has = (...values: unknown[]): boolean => values.every((value) => value !== undefined && value !== null);

const month = (period: string): string =>
  new Date(`${period}-01T00:00:00Z`).toLocaleDateString('en', { month: 'long', year: 'numeric', timeZone: 'UTC' });

const day = (date: string): string =>
  new Date(`${date}T00:00:00Z`).toLocaleDateString('en', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });

const num = (value: number): string => value.toLocaleString('en');

const dec1 = (value: number): string => value.toLocaleString('en', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

const dec2 = (value: number): string => value.toLocaleString('en', { maximumFractionDigits: 2 });

const duration = (seconds: number): string => `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;

const plural = (count: number, one: string, many: string): string => (count === 1 ? one : many);

// The "unit" argument of a year-on-year note: what the figures count.
const units: Record<string, [string, string]> = {
  visits: ['visit', 'visits'],
  keyEvents: ['key event', 'key events'],
  clicks: ['click', 'clicks'],
};

// A figure with its noun ("1 visit", "1,234 clicks"); an unknown or missing unit gives the figure alone.
const counted = (value: number, unit?: string): string => {
  const names = unit ? units[unit] : undefined;
  return names ? `${num(value)} ${plural(value, names[0], names[1])}` : num(value);
};

const through = (dataThrough?: string): string => (dataThrough ? ` Final data through ${day(dataThrough)}.` : '');

// A figure with its noun ("1 impression", "1,234 impressions").
const impressionCount = (value: number): string => `${num(value)} ${plural(value, 'impression', 'impressions')}`;

// The opening of a target-query advice that names a page: "/products ranks at position 14.5 for 2 search queries". The server sends
// the path as it is, so "/" is named here: a sentence cannot start with a slash. Without all three figures it says only that a page ranks.
const pageRanks = (path?: string, position?: number, queries?: number): string =>
  has(path, position, queries)
    ? `${path === '/' ? 'The home page' : path} ranks at position ${dec1(position!)} for ${num(queries!)} ${plural(queries!, 'search query', 'search queries')}`
    : 'A page ranks for this search';

// A change in percent with its sign, also for a rise ("+12.0%", "-18.2%"); the sign follows the rounded figure, so no "-0.0%".
const signed = (value: number): string => {
  const rounded = Math.round(value * 10) / 10;
  return `${rounded >= 0 ? '+' : '-'}${dec1(Math.abs(rounded))}%`;
};

// The path of a page as a sentence names it: "/" is the home page, any other path is written as it is.
const pageName = (path: string): string => (path === '/' ? 'the home page' : path);

// The conclusion of the sources statement: where Search Console and GA4 disagree it is the measurement, where they agree it is not.
const sourcesConclusion = (agree?: boolean): string =>
  agree === true
    ? ' Both sources point the same way, so the development is not a measurement artifact.'
    : agree === false
      ? ' The sources point in different directions, and the difference is measurement rather than traffic: Search Console counts clicks at Google, GA4 counts visits in the browser, where blocked scripts and declined cookie consent are not measured. Search Console is the one to trust for the trend.'
      : '';

// "6,000 fewer impressions than July 2025": the direction is carried by the word and not only by a sign. The noun is said once.
const impressionsAgainst = (delta: number, period: string, withNoun: boolean): string => {
  const size = Math.abs(delta);
  return `${num(size)} ${delta < 0 ? 'fewer' : 'more'}${withNoun ? ` ${plural(size, 'impression', 'impressions')}` : ''} than ${month(period)}`;
};

// The sentence about the biggest drop. Each comparison appears only when its month and figure arrived; a page can be below last
// year and above last month, so each keeps its own direction. Without any comparison it only names the page.
const biggestDrop = (
  path?: string,
  yearAgoPeriod?: string,
  yearAgoDelta?: number,
  previousPeriod?: string,
  previousDelta?: number,
): string => {
  const parts: string[] = [];
  if (has(yearAgoPeriod, yearAgoDelta)) parts.push(impressionsAgainst(yearAgoDelta!, yearAgoPeriod!, parts.length === 0));
  if (has(previousPeriod, previousDelta)) parts.push(impressionsAgainst(previousDelta!, previousPeriod!, parts.length === 0));

  return `The biggest drop is ${has(path) ? pageName(path!) : 'a page'}${parts.length > 0 ? `: ${parts.join(' and ')}` : ''}.`;
};

// The sentence about the share of top-three search queries: this month, then the comparison month (the same month a year earlier,
// or the nearest earlier one with its caveat), then the best month when it is a third.
const topThreeShare = (
  top3?: number,
  total?: number,
  percent?: number,
  comparePeriod?: string,
  compareTop3?: number,
  comparePercent?: number,
  yearOnYear?: boolean,
  bestPeriod?: string,
  bestTop3?: number,
  bestPercent?: number,
): string => {
  const now = has(top3, total, percent)
    ? `${num(top3!)} of your ${num(total!)} most shown search queries rank in the top 3 (${dec1(percent!)}%).`
    : 'Some of your most shown search queries rank in the top 3.';

  const caveat =
    ', the nearest earlier month in the archive, because the same month last year is not archived yet. Season can therefore explain part of the movement.';
  const compared = has(comparePeriod, compareTop3, comparePercent)
    ? ` Against ${num(compareTop3!)} (${dec1(comparePercent!)}%) in ${month(comparePeriod!)}${yearOnYear === true ? '.' : caveat}`
    : '';

  const best = has(bestPeriod, bestTop3, bestPercent)
    ? ` The best month in the archive is ${month(bestPeriod!)} with ${num(bestTop3!)} (${dec1(bestPercent!)}%).`
    : '';

  return now + compared + best;
};

// The sentence about the provisional days of the daily chart ("The last 2 days are preliminary: ..."), with the verb that agrees with the count.
// Without a count above zero it is no sentence at all: a month that ended long ago has no day GA4 can still revise, and a text that said
// "the last days are preliminary" about it would be wrong.
const preliminary = (count?: number | null): string =>
  has(count) && count! > 0
    ? ` ${count === 1 ? 'The last day is' : `The last ${count} days are`} preliminary: GA4 finishes processing over a couple of days, so those figures can be revised afterwards.`
    : '';

const up = (text: string): string => text.charAt(0).toUpperCase() + text.slice(1);

// The source a problem is about ("source" argument: ga4 | searchConsole), as a name; an unknown or missing one gives "Google".
const sourceName = (source?: string | null): string =>
  source === 'ga4' ? 'GA4' : source === 'searchConsole' ? 'Search Console' : 'Google';

// The API that is switched off ("api" argument: analyticsData | searchConsole), as a noun phrase for the middle of a sentence.
const apiName = (api?: string | null): string =>
  api === 'analyticsData' ? 'the Google Analytics Data API' : api === 'searchConsole' ? 'the Google Search Console API' : 'a Google API';

// Where that API is switched on in the Google Cloud console.
const apiLibrary = (api?: string | null): string =>
  api === 'analyticsData'
    ? 'https://console.cloud.google.com/apis/library/analyticsdata.googleapis.com'
    : api === 'searchConsole'
      ? 'https://console.cloud.google.com/apis/library/searchconsole.googleapis.com'
      : 'https://console.cloud.google.com/apis/library';

// For GA4 the property ID is the other usual cause of a refusal (an account or stream ID is easily taken for it), so a refusal says so.
const checkPropertyId = (source?: string | null): string =>
  source === 'ga4'
    ? ' Also check that the number you entered is the property ID shown under Admin, then Property details, and not an account or stream ID.'
    : '';

// How a text names the address that has to be given access. The "email" argument is added by the client from the key status (the
// server cannot know it); without it the text points at the Settings tab, where the address is shown.
const accountAddress = (email?: string | null): string => (has(email) ? email! : "the service account's e-mail address");

// What to do in Google Analytics or Search Console to let the service account in.
const grantAccess = (source?: string | null, email?: string | null): string =>
  source === 'ga4'
    ? `In Google Analytics, open Admin, then Property access management, and add ${accountAddress(email)} with the Viewer role.`
    : source === 'searchConsole'
      ? `In Search Console, open Settings, then Users and permissions, and add ${accountAddress(email)} as a user.`
      : `Add ${accountAddress(email)} as a user in Google Analytics and in Search Console.`;

export default {
  esattoSeoReport: {
    dashboardTitle: 'SEO Report',

    // ---- kpi: the traffic KPIs (label and description) -------------------------------------------------------------
    kpiOrganicSessionsLabel: 'Visits from search engines',
    kpiOrganicSessionsDescription: 'Through the result lists of Google and other search engines',
    kpiOrganicEngagementRateLabel: 'Engaged visits',
    kpiOrganicEngagementRateDescription: 'Share who stayed or clicked on',
    kpiOrganicSessionSecondsLabel: 'Average visit length',
    kpiOrganicSessionSecondsDescription: 'Minutes and seconds, as in GA4',
    kpiOrganicKeyEventsLabel: 'Key events from search engines',
    kpiOrganicKeyEventsDescription: 'Key events GA4 recorded on visits from search engines',
    kpiTotalSessionsLabel: 'All visits',
    kpiTotalSessionsDescription: 'All channels, not just search engines',
    kpiGscClicksLabel: 'Clicks in Google',
    kpiGscClicksDescription: 'Times someone clicked through from the result list',
    kpiClicksVsSessionsLabel: 'Clicks minus visits',
    kpiClicksVsSessionsDescription: 'Clicks are normally more; see the note below the table',

    // ---- kpi: a Search Console segment's measures (label) --------------------------------------------------------------
    kpiBrandClicksLabel: 'Clicks',
    kpiBrandImpressionsLabel: 'Impressions',
    kpiBrandCtrLabel: 'Click-through rate',
    kpiBrandPositionLabel: 'Average position',
    kpiNonBrandClicksLabel: 'Clicks',
    kpiNonBrandImpressionsLabel: 'Impressions',
    kpiNonBrandCtrLabel: 'Click-through rate',
    kpiNonBrandPositionLabel: 'Average position',
    kpiTotalClicksLabel: 'Clicks',
    kpiTotalImpressionsLabel: 'Impressions',
    kpiTotalCtrLabel: 'Click-through rate',
    kpiTotalPositionLabel: 'Average position',

    // ---- kpi: trend texts ----------------------------------------------------------------------------------------------
    kpiVsSameMonthLastYearText: 'vs the same month last year',
    // args: period (the month a year back, YYYY-MM)
    kpiNoComparisonText: ({ period }: { period?: string } = {}) =>
      has(period) ? `no comparison: ${month(period!)} has no data` : 'no comparison: last year has no data',
    // args: period (YYYY-MM), value (that month's figure), unit (visits | keyEvents | clicks)
    kpiAnomalousBaselineText: ({ period, value, unit }: { period?: string; value?: number; unit?: string } = {}) =>
      has(period, value) ? `vs ${month(period!)} (${counted(value!, unit)}), an anomalous baseline` : 'vs an anomalous baseline',
    // args: percent (last year's engagement rate, one decimal)
    kpiEngagementVsLastYearText: ({ percent }: { percent?: number } = {}) =>
      has(percent) ? `vs ${dec1(percent!)}% last year` : 'vs the same month last year',
    kpiEngagementShareText: 'share of visits with engagement',
    // args: seconds (last year's average visit, whole seconds)
    kpiDurationVsLastYearText: ({ seconds }: { seconds?: number } = {}) =>
      has(seconds) ? `vs ${duration(seconds!)} last year` : 'vs the same month last year',
    kpiDurationPerVisitText: 'minutes:seconds per visit',
    kpiClicksMinusVisitsText: 'Clicks in Google minus visits from Google in GA4',

    // ---- chart: title, subtitle, description -----------------------------------------------------------------------
    chartTrafficOrganicTitle: 'Visits from search engines: historical trend',
    chartTrafficOrganicSubtitle: "Visits from the search engines' result lists, per month",
    chartTrafficOrganicDescription:
      'This chart shows visits from the result lists of Google and other search engines, with one line per year. A visit is one stay on the site: everything someone does during the same stay counts as ONE visit, and if they come back another day, that is a new one. So the same person can account for several visits.',
    chartTrafficTotalTitle: 'All visits: every channel',
    chartTrafficTotalSubtitle: 'All visits per month, wherever they came from',
    chartTrafficTotalDescription:
      'This chart shows all visits together, however they found the site: search engines, typed straight into the address bar, links from other sites and social media. It shows whether it is the search traffic that grows or everything.',
    chartTrafficSearchComparisonTitle: 'Visits against clicks: GA4 compared with Search Console',
    chartTrafficSearchComparisonSubtitle: 'Visits according to GA4, clicks according to Search Console',
    chartTrafficSearchComparisonDescription:
      "This chart shows visits (GA4) and clicks (Search Console) side by side, year by year. A click is a click in Google's result list; a visit is the stay that follows. Clicks are therefore normally MORE: if someone clicks, goes back to Google and clicks again within half an hour, that is two clicks but one visit. On top of that, GA4 only counts visitors who have accepted cookies, while Google counts the click anyway.",
    chartTrafficSearchClicksTitle: 'Clicks: brand vs non-brand',
    chartTrafficSearchClicksSubtitle: 'Clicks per month, split by the brand terms in the search query',
    chartTrafficSearchClicksDescription:
      'This chart shows clicks per month, split by whether the search contained the brand. Brand is people already looking for you; non-brand is new visitors who find the site through something else.',
    chartTrafficSearchImpressionsTitle: 'Impressions: brand vs non-brand',
    chartTrafficSearchImpressionsSubtitle: 'Impressions per month, same split',
    chartTrafficSearchImpressionsDescription:
      'This chart shows how often the site was shown in the search results, with the same split as the clicks chart. Many impressions without clicks mean the site is seen but does not draw visits.',
    chartTrafficSearchTitle: 'Search queries: brand vs non-brand (Search Console)',
    chartTrafficSearchSubtitle: 'Clicks, impressions, CTR and average position',

    // ---- chart: the daily traffic chart (ChartBlock.Id "daily_traffic"). Its texts are written with the arguments the daily
    // endpoint sends beside the chart (DailyChart.TextArgs): period is null in the rolling mode. -----------------------------
    // args: period (the calendar month, YYYY-MM), windowDays (the length of the rolling window)
    chartDailyTrafficTitle: ({ period, windowDays }: { period?: string; windowDays?: number } = {}) =>
      has(period) ? month(period!) : has(windowDays) ? `The last ${windowDays} days` : 'Visits per day',
    chartDailyTrafficSubtitle: 'Live from GA4',
    // args: period, windowDays, provisionalDays (how many of the last days GA4 may still revise: the client passes the smaller of this and
    // windowDays, and null or 0 leaves the sentence about preliminary figures out)
    chartDailyTrafficDescription: ({ period, windowDays, provisionalDays }: { period?: string; windowDays?: number; provisionalDays?: number | null } = {}) =>
      `Visits per day, with the average of ${has(period) ? 'the previous month' : has(windowDays) ? `the previous ${windowDays} days` : 'the preceding period'} as a dashed comparison.${preliminary(provisionalDays)} The figures count only visits GA4 can measure (visitors who decline cookie consent or block scripts are not counted), so they are a floor, not the whole picture. Click a day in the chart to see where the visits came from.`,

    // ---- series: label -------------------------------------------------------------------------------------------------
    // args: year (from the series key's "_YYYY" suffix)
    seriesYearLabel: ({ year }: { year?: number } = {}) => (has(year) ? `${year}` : 'Year'),
    // args: year
    seriesSessionsLabel: ({ year }: { year?: number } = {}) => (has(year) ? `${year} visits` : 'Visits'),
    // args: year
    seriesClicksLabel: ({ year }: { year?: number } = {}) => (has(year) ? `${year} clicks` : 'Clicks'),
    seriesBrandLabel: 'Brand',
    seriesNonBrandLabel: 'Non-brand',
    seriesTotalLabel: 'Total (unfiltered)',
    seriesDailyLabel: 'Visits per day',
    seriesBaselineLabel: 'Average of the preceding period',

    // ---- annotation: title and text ------------------------------------------------------------------------------------
    annotationNoKeyEventsTitle: 'No key events in GA4',
    annotationNoKeyEventsText:
      'GA4 reports zero key events in every month. That means no event is marked as a key event, not that no enquiries came in. As long as that is the case, the report can only say which pages bring traffic, not which bring business.',
    annotationAnomalousBaselineTitle: 'The comparison month is not representative',
    // args: period (the comparison month, YYYY-MM), value, median (of the months around it), unit (visits | keyEvents | clicks)
    annotationAnomalousBaselineText: ({ period, value, median, unit }: { period?: string; value?: number; median?: number; unit?: string } = {}) =>
      `${has(period, value, median) ? `${month(period!)} has ${counted(value!, unit)} against a median of ${num(median!)} in the months around it.` : 'The comparison month lies far below the months around it.'} The year-on-year comparison is still calculated against that month, but it measures the measurement outage rather than the development.`,
    annotationAnonymizedQueriesTitle: 'Anonymized search queries',
    // args: percent (brand + non-brand as a share of all clicks, one decimal), period (YYYY-MM)
    annotationAnonymizedQueriesText: ({ percent, period }: { percent?: number; period?: string } = {}) =>
      `${has(percent, period) ? `Brand and non-brand add up to ${dec1(percent!)}% of the clicks in ${month(period!)}.` : 'Brand and non-brand add up to less than all the clicks.'} Search Console leaves out rare queries as soon as data is filtered by query, and the difference cannot be split between brand and non-brand.`,
    annotationSegmentsExceedTotalTitle: 'The segments exceed the total',
    // args: percent (brand + non-brand as a share of all clicks, one decimal), period (YYYY-MM)
    annotationSegmentsExceedTotalText: ({ percent, period }: { percent?: number; period?: string } = {}) =>
      `${has(percent, period) ? `Brand and non-brand add up to ${dec1(percent!)}% of the clicks in ${month(period!)}, so more than 100%.` : 'Brand and non-brand add up to more than all the clicks.'} Search Console rounds the three aggregates separately, so a click or two of difference is expected. A larger difference suggests that the brand terms or the property setting are wrong.`,
    annotationOutsideGscWindowTitle: "Outside Search Console's 16-month window",
    // args: count (months of the curve before the window), windowStart (the window's first month, YYYY-MM)
    annotationOutsideGscWindowText: ({ count, windowStart }: { count?: number; windowStart?: string } = {}) =>
      has(count, windowStart) && count === 1
        ? `1 month of the curve falls before ${month(windowStart!)} and is no longer kept in Search Console. It is drawn as a gap in the curve, not as zero.`
        : `${has(count, windowStart) ? `${count} months of the curve fall before ${month(windowStart!)}` : 'Some months of the curve fall before the window'} and are no longer kept in Search Console. They are drawn as gaps in the curve, not as zero.`,
    annotationMissingGscMonthsTitle: 'Months without Search Console data',
    // args: from, to (the first and last month without data, YYYY-MM)
    annotationMissingGscMonthsText: ({ from, to }: { from?: string; to?: string } = {}) =>
      `${!has(from, to) ? 'Some months have' : from === to ? `${month(from!)} has` : `${month(from!)} to ${month(to!)} have`} no data in Search Console. Missing data is drawn as gaps in the curve.`,
    // args: count (pages found)
    annotationPagesNotClickedTitle: ({ count }: { count?: number } = {}) =>
      has(count) ? `${count} ${plural(count!, 'page', 'pages')} seen but not clicked` : 'Pages seen but not clicked',
    // args: count, pages (up to three shortened paths, comma separated), position, impressions, ctrPercent (the thresholds)
    annotationPagesNotClickedText: ({ count, pages, position, impressions, ctrPercent }: { count?: number; pages?: string; position?: number; impressions?: number; ctrPercent?: number } = {}) =>
      `${has(count, pages, position, impressions, ctrPercent) ? `${pages} ${plural(count!, 'ranks', 'rank')} at position ${dec1(position!)} or better with at least ${num(impressions!)} impressions, but under ${num(ctrPercent!)}% CTR.` : 'These pages rank well and are seen often, but are hardly clicked.'} So the ranking is already there; it is the title and meta description in the search result that make nobody click.`,
    // args: count (pages found)
    annotationPagesRankDeepTitle: ({ count }: { count?: number } = {}) =>
      has(count) ? `${count} ${plural(count!, 'page', 'pages')} shown but ranking deep` : 'Pages shown but ranking deep',
    // args: count, pages (up to three shortened paths, comma separated), impressions, position (the thresholds)
    annotationPagesRankDeepText: ({ count, pages, impressions, position }: { count?: number; pages?: string; impressions?: number; position?: number } = {}) =>
      `${has(count, pages, impressions, position) ? `${pages} ${plural(count!, 'has', 'have')} at least ${num(impressions!)} impressions but an average position worse than ${dec1(position!)}:` : 'These pages are seen often but rank on average'} page two or deeper, where almost nobody clicks. The demand is there; the ranking is not.`,
    annotationNonBrandNotClickedTitle: 'Non-brand: visibility without clicks',
    // args: impressions, clicks, ctrPercent (up to two decimals), position (one decimal)
    annotationNonBrandNotClickedText: ({ impressions, clicks, ctrPercent, position }: { impressions?: number; clicks?: number; ctrPercent?: number; position?: number } = {}) =>
      `${has(impressions, clicks, ctrPercent, position) ? `${num(impressions!)} non-brand impressions gave ${num(clicks!)} ${plural(clicks!, 'click', 'clicks')} (${dec2(ctrPercent!)}% CTR) at an average position of ${dec1(position!)}.` : 'Non-brand searches show the site often but give few clicks.'} The site is shown for searches that do not contain the brand, but does not capture them.`,

    // ---- opportunity: title and text (a reference's arguments go to both) ------------------------------------------
    // args: query (the search query)
    opportunityNearTitle: ({ query }: { query?: string } = {}) =>
      has(query) ? `"${query}" is close to page one` : 'A search query is close to page one',
    // args: position (one decimal), impressions
    opportunityNearText: ({ position, impressions }: { position?: number; impressions?: number } = {}) =>
      `${has(position, impressions) ? `Position ${dec1(position!)} with ${impressionCount(impressions!)}.` : 'It ranks on page two.'} The page exists and is seen: it needs to get better at this very query, not be replaced.`,
    // args: query
    opportunityCannibalTitle: ({ query }: { query?: string } = {}) =>
      has(query) ? `Two pages compete for "${query}"` : 'Two pages compete for the same query',
    // args: firstPath, firstPosition (one decimal), firstImpressions, secondPath, secondPosition, secondImpressions
    opportunityCannibalText: ({ firstPath, firstPosition, firstImpressions, secondPath, secondPosition, secondImpressions }: { firstPath?: string; firstPosition?: number; firstImpressions?: number; secondPath?: string; secondPosition?: number; secondImpressions?: number } = {}) =>
      `${has(firstPath, firstPosition, firstImpressions, secondPath, secondPosition, secondImpressions) ? `${firstPath} (position ${dec1(firstPosition!)}, ${impressionCount(firstImpressions!)}) and ${secondPath} (position ${dec1(secondPosition!)}, ${impressionCount(secondImpressions!)}) are both shown for the query.` : "Two of the site's own pages are both shown for the query."} Choose which one should own it, and let the other link to it.`,

    // ---- target: a target query's status (label) and its advice (text) -------------------------------------------------
    targetMissingLabel: 'Not visible',
    targetDeepLabel: 'Ranking deep',
    targetNearLabel: 'Close to page one',
    targetTitleLabel: 'Not clicked',
    targetPerformingLabel: 'Performing',
    // Names the "Refresh" button of the month by its label in the dashboard: keep the two the same.
    targetMissingNoQueriesText:
      `The month's search queries are missing from the report, so this search cannot be assessed. Use "Refresh" on the month to fetch them.`,
    // args: rowsFetched (how many search queries were fetched)
    targetMissingText: ({ rowsFetched }: { rowsFetched?: number } = {}) =>
      `No content answers the topic. The search is not among the ${has(rowsFetched) ? `${num(rowsFetched!)} ` : ''}most shown search queries this month. Google anonymizes the long tail, so this means "invisible in what we can see", not proven zero.`,
    targetDeepText:
      'Demand is there, the ranking is not. Work on the content of the page that should answer the search, and link to it internally from pages that already rank.',
    targetNearText:
      'One step from page one. Strengthen the page that already ranks: it does not need to be replaced, it needs to get better at this very search.',
    targetTitleText:
      'The ranking is won, the click is not. Rewrite the title and meta description so they answer the search: it is the cheapest fix there is.',
    targetPerformingText: 'Nothing to do.',
    // args: path, position (one decimal), queries (how many of the term's search queries the page ranks for)
    targetPageDeepText: ({ path, position, queries }: { path?: string; position?: number; queries?: number } = {}) =>
      `${pageRanks(path, position, queries)}. Demand is there, the ranking is not: work on the content of that page and link to it internally from pages that already rank.`,
    // args: path, position, queries
    targetPageNearText: ({ path, position, queries }: { path?: string; position?: number; queries?: number } = {}) =>
      `${pageRanks(path, position, queries)}. One step from page one: strengthen that page, it does not need to be replaced.`,
    // args: path, position, queries, ctrPercent (up to two decimals)
    targetPageTitleText: ({ path, position, queries, ctrPercent }: { path?: string; position?: number; queries?: number; ctrPercent?: number } = {}) =>
      `${pageRanks(path, position, queries)}${has(ctrPercent) ? ` with a click-through rate of ${dec2(ctrPercent!)}%` : ''}. The ranking is won, the click is not: rewrite the title and meta description of that very page.`,
    // args: path, position, queries
    targetPagePerformingText: ({ path, position, queries }: { path?: string; position?: number; queries?: number } = {}) =>
      `${pageRanks(path, position, queries)}. Nothing to do.`,

    // ---- narrative: the month in words (Statement.Text, part Text). A part a statement has nothing to say about arrives as null.
    // args: clicks, clicksChange (percent), sessions, sessionsChange (percent), agree (true when both sources move the same way)
    narrativeSourcesText: ({ clicks, clicksChange, sessions, sessionsChange, agree }: { clicks?: number; clicksChange?: number; sessions?: number; sessionsChange?: number; agree?: boolean } = {}) =>
      `${has(clicks, clicksChange, sessions, sessionsChange) ? `Search Console shows ${counted(clicks!, 'clicks')} (${signed(clicksChange!)}), GA4 shows ${counted(sessions!, 'visits')} from search engines (${signed(sessionsChange!)}).` : 'Search Console clicks are set against GA4 visits from search engines.'}${sourcesConclusion(agree)}`,
    // args: top3, total (the search queries examined), percent (one decimal); comparePeriod (YYYY-MM), compareTop3, comparePercent,
    // yearOnYear (true: the same month a year earlier, false: the nearest earlier month); bestPeriod, bestTop3, bestPercent
    narrativeVisibilityText: ({ top3, total, percent, comparePeriod, compareTop3, comparePercent, yearOnYear, bestPeriod, bestTop3, bestPercent }: { top3?: number; total?: number; percent?: number; comparePeriod?: string; compareTop3?: number; comparePercent?: number; yearOnYear?: boolean; bestPeriod?: string; bestTop3?: number; bestPercent?: number } = {}) =>
      topThreeShare(top3, total, percent, comparePeriod, compareTop3, comparePercent, yearOnYear, bestPeriod, bestTop3, bestPercent),
    // args: clicks (non-brand clicks), total (all clicks), percent (the share, one decimal)
    narrativeNonBrandText: ({ clicks, total, percent }: { clicks?: number; total?: number; percent?: number } = {}) =>
      `${has(clicks, total, percent) ? `Non-brand accounts for ${num(clicks!)} of ${counted(total!, 'clicks')} (${dec1(percent!)}%).` : 'Non-brand accounts for part of the clicks.'} Growth has to come from there: brand searches follow awareness of the brand, not the content work.`,
    // args: sessions (visits from search engines), events (key events from them)
    narrativeConversionText: ({ sessions, events }: { sessions?: number; events?: number } = {}) =>
      has(sessions, events) ? `${counted(sessions!, 'visits')} from search engines led to ${counted(events!, 'keyEvents')}.` : 'Visits from search engines led to key events.',
    // args: path ("/" is the home page), yearAgoPeriod (YYYY-MM), yearAgoDelta (impressions against that month, negative is fewer),
    // previousPeriod, previousDelta; position, positionBefore (one decimal)
    narrativeMovementText: ({ path, yearAgoPeriod, yearAgoDelta, previousPeriod, previousDelta, position, positionBefore }: { path?: string; yearAgoPeriod?: string; yearAgoDelta?: number; previousPeriod?: string; previousDelta?: number; position?: number; positionBefore?: number } = {}) =>
      `${biggestDrop(path, yearAgoPeriod, yearAgoDelta, previousPeriod, previousDelta)}${has(position, positionBefore) ? ` The position went from ${dec1(positionBefore!)} to ${dec1(position!)}.` : ''}`,
    // args: period (the one month the movement is compared with, YYYY-MM)
    narrativeSeasonText: ({ period }: { period?: string } = {}) =>
      `The movement is only compared with ${has(period) ? month(period!) : 'one earlier month'}, because the same month last year is not in the archive. Part of the movement can therefore be season and not development.`,

    // ---- ui: sources, their notes, the report status ---------------------------------------------------------------
    uiGa4Label: 'GA4',
    uiGscLabel: 'Search Console',
    uiGa4IncompleteText: 'The report period lacks complete GA4 data.',
    // args: dataThrough (optional, the last day with final data, YYYY-MM-DD)
    uiGscLagText: ({ dataThrough }: { dataThrough?: string } = {}) => `2-3 days of data lag in Search Console.${through(dataThrough)}`,
    // args: period (the month the Search Console figures refer to, YYYY-MM), dataThrough (optional, YYYY-MM-DD)
    uiGscIncompleteText: ({ period, dataThrough }: { period?: string; dataThrough?: string } = {}) =>
      `The report period is not complete in Search Console: all Search Console figures refer to ${has(period) ? month(period!) : 'an earlier month'}.${through(dataThrough)}`,
    // args: period (the report month, YYYY-MM), dataThrough (optional, YYYY-MM-DD)
    uiGscNoDataText: ({ period, dataThrough }: { period?: string | null; dataThrough?: string | null } = {}) =>
      `Search Console has no data for ${has(period) ? month(period!) : 'this month'}.${through(dataThrough ?? undefined)}`,
    uiNoDataText: 'No data yet.',

    // ---- error: what went wrong and what to do about it (a problem's code gives the key: apiDisabled -> errorApiDisabledTitle and
    // errorApiDisabledText, see ErrorCodes). The server sends the code and, for some, "api" or "source"; the CLIENT adds "email", the
    // service account's address from the key status, to the problems whose cure is "add this address". -----------------------------
    errorNotConfiguredTitle: 'SEO Report is not set up yet',
    errorNotConfiguredText:
      'There is no Google service account key yet. Open the Settings tab, paste the key, enter the GA4 property ID and the Search Console property, and run "Test connection". If you do not have a key, ask whoever manages your Google Cloud project to create one.',
    errorKeyUnreadableTitle: "The saved key can't be read",
    errorKeyUnreadableText:
      "A key is saved, but this environment cannot decrypt it. That happens after a database is restored into another environment or when the site's data protection keys change. Open the Settings tab and enter the key again, or ask your developer to supply it in the site configuration.",
    errorKeyInvalidTitle: 'The service account key is not valid',
    errorKeyInvalidText:
      'The key is not a usable Google service account key, or Google no longer accepts it: the key may have been deleted or disabled in Google Cloud, or its service account may have been. Create a new key in Google Cloud (Service accounts, the service account, Keys, Add key, Create new key, JSON) and paste the whole JSON file on the Settings tab. If the key is set in the site configuration instead, ask your developer to replace it there: a configured key cannot be changed in the backoffice. If you cannot create a key, ask whoever manages your Google Cloud project.',
    // args: api (analyticsData | searchConsole, the API that is switched off)
    errorApiDisabledTitle: ({ api }: { api?: string | null } = {}) => `${up(apiName(api))} is switched off`,
    // args: api
    errorApiDisabledText: ({ api }: { api?: string | null } = {}) =>
      `Google refused the request because ${apiName(api)} is not enabled in the Google Cloud project that the service account belongs to. Enable it at ${apiLibrary(api)}, with the project of the service account selected, wait a minute and test again. If you cannot do that yourself, ask whoever manages your Google Cloud project.`,
    // args: source (ga4 | searchConsole, the source that refused)
    errorNoAccessTitle: ({ source }: { source?: string | null } = {}) => `No access to ${sourceName(source)}`,
    // args: source, email (the service account's address; added by the client)
    errorNoAccessText: ({ source, email }: { source?: string | null; email?: string | null } = {}) =>
      `${sourceName(source)} did not let the service account in. ${grantAccess(source, email)}${checkPropertyId(source)} Wait a minute, then test again. If you cannot add users yourself, ask whoever administers the property.`,
    // args: source (ga4 | searchConsole, the source that does not know the property)
    errorPropertyNotFoundTitle: ({ source }: { source?: string | null } = {}) =>
      source === 'ga4' || source === 'searchConsole' ? `${sourceName(source)} property not found` : 'Property not found',
    // args: source, email (the service account's address; added by the client)
    errorPropertyNotFoundText: ({ source, email }: { source?: string | null; email?: string | null } = {}) =>
      source === 'ga4'
        ? `GA4 does not know the property ID you entered, or the service account cannot see it. Enter the number under Admin, then Property details, in Google Analytics, and make sure ${accountAddress(email)} has been added to that very property.`
        : source === 'searchConsole'
          ? `Search Console does not know the property you entered, or the service account cannot see it. Enter it exactly as Search Console lists it: sc-domain:example.com for a domain property, or the full address for a URL-prefix property, with http or https, www or not, and the trailing slash, as in https://www.example.com/. The wrong form never matches. Make sure ${accountAddress(email)} has been added as a user of that property.`
          : `Google does not know the property you entered, or the service account cannot see it. Check the GA4 property ID and the Search Console property on the Settings tab, and make sure ${accountAddress(email)} has been given access to both.`,
    // args: source (ga4 | searchConsole, the source that has nothing yet)
    errorNoDataTitle: ({ source }: { source?: string | null } = {}) =>
      source === 'ga4' || source === 'searchConsole' ? `${sourceName(source)} is connected, but has no data yet` : 'Connected, but no data yet',
    // args: source
    errorNoDataText: ({ source }: { source?: string | null } = {}) =>
      source === 'ga4'
        ? 'The connection works, but GA4 has no visits for yesterday. A new property needs a day or two of traffic, and its measurement code has to be on the site. There is nothing to fix here: the report fills in as data arrives. Test again tomorrow.'
        : source === 'searchConsole'
          ? 'The connection works, but Search Console has no data for this property yet. A newly added property takes a few days to show clicks and impressions. If it stays empty, check that it is the property of the right site and that its form matches: a domain property or a URL prefix.'
          : 'The connection works, but there is no data yet. Test again in a few days.',
    errorQuotaTitle: 'Google request limit reached',
    errorQuotaText:
      "Google limits how many requests the service account's project may make, and that limit is reached for now. Wait a few minutes (until tomorrow if the daily limit is used up) and try again. If it keeps happening, ask whoever manages the Google Cloud project to look at its quota.",
    errorUnknownTitle: 'Something went wrong',
    errorUnknownText:
      'The request failed for a reason the report cannot name. Try again in a moment. If it keeps failing, ask your developer to check the site log.',
    errorGa4PropertyInvalidTitle: 'The GA4 property ID is not valid',
    errorGa4PropertyInvalidText:
      'Enter the number from Google Analytics, under Admin, then Property details, for example 123456789. It is not the measurement ID that starts with G-.',
    errorSearchConsoleInvalidTitle: 'The Search Console property is not valid',
    errorSearchConsoleInvalidText:
      'Enter a domain property as example.com (or sc-domain:example.com), or a URL-prefix property as the full address with https:// and the trailing slash, for example https://www.example.com/. Spaces and other kinds of address are not accepted.',

    // ---- error: what the API itself refuses (ApiCodes: a request that is not valid, as opposed to a failure at Google). Same
    // keys as above, registered in ReportKeys from ApiCodes.All. None of them has arguments. -------------------------------------
    errorPeriodInvalidTitle: 'That is not a month',
    errorPeriodInvalidText:
      'A month has to be written as year and month, for example 2026-07. Pick a month from the list instead of typing one, then try again.',
    errorPeriodOutOfRangeTitle: 'That month cannot be fetched',
    errorPeriodOutOfRangeText:
      'A month can only be fetched once it has ended, and Search Console keeps only about sixteen months of data. Pick a month from the list.',
    errorDateInvalidTitle: 'That is not a date',
    errorDateInvalidText:
      'A date has to be written as year, month and day, for example 2026-07-05. Click a day in the chart instead of typing one.',
    errorDateOutOfRangeTitle: 'That day cannot be shown',
    errorDateOutOfRangeText:
      'The daily chart shows the last twelve months, up to today. Pick a month or a day inside that range.',
    errorAlreadyRunningTitle: 'A fetch is already running',
    errorAlreadyRunningText:
      'Another fetch is in progress, here or on another server. Wait until it has finished; the report updates by itself.',
    errorRequestInvalidTitle: 'The request was not complete',
    errorRequestInvalidText:
      'The page sent something the server could not use. Reload the page and try again. If it keeps happening, ask your developer to check the site log.',

    // ---- ui: the dashboard itself. The client only; the server never sends these, so ReportKeys does not register them. ------------
    uiSubtitleText: 'Traffic and search from Google Analytics and Search Console',
    uiLoadingLabel: 'Loading the report',
    uiTabsLabel: 'Report sections',
    uiTabAnalysisLabel: 'Analysis',
    uiTabTrafficLabel: 'Traffic',
    uiTabQueriesLabel: 'Queries',
    uiTabTopPagesLabel: 'Most clicked',
    uiTabToFixLabel: 'To fix',
    uiTabComparisonLabel: 'Analytics vs Search Console',
    uiTabSettingsLabel: 'Settings',

    uiMonthPickerLabel: 'Report month',
    uiRefreshLabel: 'Refresh',
    // args: period (the month being fetched, YYYY-MM)
    uiRefreshingText: ({ period }: { period?: string | null } = {}) =>
      `Fetching ${has(period) ? month(period!) : 'the month'} from Google Analytics and Search Console. This takes about fifteen seconds.`,
    uiFetchNowLabel: 'Fetch now',
    uiTryAgainLabel: 'Try again',
    uiDismissLabel: 'Dismiss',
    // args: when (the moment, already written for the reader: "3 hours ago" or a date)
    uiFetchedText: ({ when }: { when?: string | null } = {}) => (has(when) ? `Fetched ${when}` : 'Fetched'),
    // args: period (YYYY-MM)
    uiReportPeriodText: ({ period }: { period?: string | null } = {}) => (has(period) ? `Report period ${month(period!)}` : 'Report period'),
    // args: traceId (the server's id of the request, for the person who reads the log)
    uiProblemReferenceText: ({ traceId }: { traceId?: string | null } = {}) =>
      has(traceId) ? `Reference ${traceId}. Give it to your developer if the problem comes back.` : 'No reference was given.',

    uiProgressTitle: 'Fetching your data from Google',
    uiProgressStartingText: 'Starting the fetch...',
    // args: done (months finished), total (months to fetch), period (the month being fetched, YYYY-MM; optional)
    uiProgressText: ({ done, total, period }: { done?: number | null; total?: number | null; period?: string | null } = {}) =>
      has(done, total)
        ? `${num(done!)} of ${num(total!)} ${plural(total!, 'month', 'months')} fetched${has(period) ? `, now ${month(period!)}` : ''}.`
        : 'Fetching months from Google.',
    uiProgressHintText: 'You can leave this page: the fetch carries on in the background.',
    uiNotFetchedTitle: 'Nothing has been fetched yet',
    uiNotFetchedText:
      'The scheduled fetch fills the archive in the background, one month at a time. To start at once instead, press Fetch now. The first fetch takes a few minutes.',
    // args: period (the latest month that should be archived, YYYY-MM)
    uiArchiveBehindText: ({ period }: { period?: string | null } = {}) =>
      `${has(period) ? month(period!) : 'The latest month'} is not in the archive yet. The scheduled fetch normally adds a new month soon after it has ended. To get it now, press Fetch now.`,
    uiNothingToFetchText: 'There was nothing to fetch: the archive is up to date.',
    // args: done (months fetched)
    uiFetchedMonthsText: ({ done }: { done?: number | null } = {}) =>
      has(done) ? `Fetched ${num(done!)} ${plural(done!, 'month', 'months')}.` : 'Fetched the missing months.',
    uiFetchFailedTitle: 'The last fetch did not finish',
    uiFetchLostTitle: 'Lost contact with the server',
    uiFetchLostText: 'The fetch may still be running. Try again to keep following it.',

    uiEmptyMonthTitle: 'No data for this month',
    uiEmptyMonthText:
      'Neither Google Analytics nor Search Console returned anything for this month. A newly connected property needs a few days before data arrives. If it stays empty, ask whoever manages the connection to check it on the Settings tab.',
    // args: period (YYYY-MM)
    uiNoReportText: ({ period }: { period?: string | null } = {}) =>
      `There is no report for ${has(period) ? month(period!) : 'this month'} in the archive. Press Refresh to fetch it from Google.`,

    uiOverviewLabel: 'Overview of the report month',
    uiRingOfVisitsText: 'of visits',
    // args: label (the figure's name), value (the share, already written: 62.4%)
    uiRingAltText: ({ label, value }: { label?: string | null; value?: string | null } = {}) =>
      has(label, value) ? `${label}: ${value} of visits` : 'Share of visits',
    uiNoValueText: 'No data',
    uiKeyFiguresLabel: 'Key figures',
    uiTrendUpLabel: 'Up',
    uiTrendDownLabel: 'Down',
    uiTrendFlatLabel: 'Change',
    uiDataNotesLabel: 'Notes on the data',
    // args: source (the source's name), note (the note, already written)
    uiSourceNoteText: ({ source, note }: { source?: string | null; note?: string | null } = {}) =>
      has(source, note) ? `${source}: ${note}` : has(note) ? note! : 'No note.',

    uiMissingLabel: 'Still needed',
    uiMissingKeyLabel: 'Service account key',
    uiMissingGa4PropertyIdLabel: 'GA4 property ID',
    uiMissingSearchConsoleSiteLabel: 'Search Console property',
    uiAskAdministratorText:
      'Only people with access to the Settings section can connect Google. Ask an administrator to open Settings and finish the setup.',
    uiFindSettingsText: 'Open Settings to finish the setup. If you cannot see it, ask an administrator.',

    // ---- ui: reading the report (charts, their data tables, notes). The client only. ----------------------------------------------
    // args: from, to (the first and last point of the chart, already written: two months, two days or two years)
    uiChartRangeText: ({ from, to }: { from?: string | null; to?: string | null } = {}) =>
      !has(from) ? 'The period shown' : !has(to) || from === to ? from! : `${from} to ${to}`,
    // args: title (the chart's title)
    uiChartTableCaption: ({ title }: { title?: string | null } = {}) => (has(title) && title !== '' ? `Data table: ${title}` : 'Data table'),
    uiChartLegendLabel: 'Show or hide a line',
    uiChartFailedText: 'The chart could not be drawn. Reload the page to try again.',
    // The kind of a note under a chart, written beside its icon: never told by colour alone.
    uiNoteWarningLabel: 'Warning',
    uiNoteInfoLabel: 'Note',
    uiNoteInsightLabel: 'Insight',
    // ---- ui: the search tabs ---------------------------------------------------------------------------------------------------
    uiSearchTotalTitle: 'Search Console in total',
    uiBrandSplitOffText:
      'Brand and non-brand searches are not split, because no brand terms are set. Add them on the Settings tab to see the split in a table and two charts.',
    uiHelpSegmentsText:
      'Clicks are visits from the search result. Impressions are how many times the site appeared in the result list. Click-through rate is the share of impressions that became a click. Average position is where the site ranked on average, where 1 is the very top.',
    uiHelpQueriesText:
      'Clicks are visits the search brought. Impressions are how many times the site appeared for the search. Click-through rate is the share of impressions that became a click. Average position is where the site ranked for the search on average, where 1 is the very top.',
    uiHelpPagesText:
      'Clicks are visits that landed on the page. Impressions are how many times the page appeared in the result list. Click-through rate is the share of impressions that became a click. Average position is where the page ranked on average, where 1 is the very top.',
    uiTopQueriesTitle: 'Most clicked search queries',
    uiTopPagesTitle: 'Most clicked landing pages',
    uiTopListsNoteText:
      'The top lists do not add up to the total. Search Console leaves out rare search queries as soon as data is filtered by query or by page, and that difference cannot be divided up.',
    uiColumnQueryLabel: 'Search query',
    uiColumnPageLabel: 'Page',
    uiColumnSegmentLabel: 'Segment',
    uiColumnChannelLabel: 'Channel',
    uiColumnSourceLabel: 'Source',
    uiColumnVisitsLabel: 'Visits',
    uiColumnClicksLabel: 'Clicks',
    uiColumnImpressionsLabel: 'Impressions',
    uiColumnCtrLabel: 'Click-through rate',
    uiColumnPositionLabel: 'Average position',
    uiColumnMonthLabel: 'Month',
    uiColumnDayLabel: 'Day',
    uiColumnPeriodLabel: 'Period',

    // ---- ui: the daily traffic card (live from GA4, on the Traffic tab) -------------------------------------------------------------
    uiDailyTitle: 'Traffic by day',
    uiDailyPeriodLabel: 'Period',
    // args: windowDays (the length of the rolling window; it is only known once it has been read)
    uiDailyRollingLabel: ({ windowDays }: { windowDays?: number | null } = {}) => (has(windowDays) ? `Last ${windowDays} days` : 'Recent days'),
    uiDailyLoadingText: 'Fetching the daily figures from GA4...',
    uiDailyVisitsLabel: 'visits',
    uiDailyNoComparisonText: 'no comparison',
    uiDailyAgainstMonthText: 'vs the previous month',
    // args: windowDays (the length of the window, which the period before has too)
    uiDailyAgainstDaysText: ({ windowDays }: { windowDays?: number | null } = {}) =>
      has(windowDays) ? `vs the previous ${windowDays} days` : 'vs the period before',
    // args: day (the busiest day, already written with its weekday), sessions (its visits, already written)
    uiDailyPeakText: ({ day, sessions }: { day?: string | null; sessions?: string | null } = {}) =>
      has(day, sessions) ? `Peak day: ${day}, ${sessions} visits.` : 'No peak day.',
    uiDailyDayPickerLabel: "Where a day's visits came from",
    uiDailyPickDayLabel: 'Pick a day...',
    // args: day (written with its weekday), sessions (written)
    uiDailyDayOptionText: ({ day, sessions }: { day?: string | null; sessions?: string | null } = {}) =>
      has(day, sessions) ? `${day}: ${sessions}` : has(day) ? day! : 'A day',
    // args: day (written with its weekday)
    uiDailySourcesTitle: ({ day }: { day?: string | null } = {}) => (has(day) ? `Where the visits came from on ${day}` : 'Where the visits came from'),
    // args: day
    uiDailySourcesLoadingText: ({ day }: { day?: string | null } = {}) => (has(day) ? `Fetching the sources for ${day}...` : 'Fetching the sources...'),
    // args: day
    uiDailySourcesNoneText: ({ day }: { day?: string | null } = {}) => (has(day) ? `No visits were recorded on ${day}.` : 'No visits were recorded.'),

    // ---- ui: the Analysis tab (read from the archive; the month's note and the target queries are edited here) ------------------------
    uiAnalysisLoadingText: 'Reading the analysis...',
    uiAnalysisNoneText:
      'There is nothing to analyze yet. The analysis is built from the archived months, which fill in as they are fetched.',
    uiAnalysisUpdatingText: 'Updating the target queries...',

    uiSummaryTitle: 'The month in words',
    uiSummaryEmptyText:
      'There are too few figures to summarize the month yet. The summary only says what can be worked out from the data.',

    // The month's note: a person's words, kept apart from the statements above, which are worked out from the figures.
    uiMonthNoteTitle: 'Note for the month',
    uiMonthNoteLabel:
      'What happened this month that the figures do not show? A launch, an outage, a campaign, a change in the measurement.',
    uiMonthNoteHelpText:
      'The note is written by a person and is kept apart from the statements above, which are worked out from the figures. It belongs to this month only; saving it empty removes it.',
    // args: length (the note's length without the white space around it), max (the longest note)
    uiNoteCounterText: ({ length, max }: { length?: number | null; max?: number | null } = {}) =>
      has(length, max) ? `${num(length!)} of ${num(max!)} characters` : 'Characters used',
    // args: over (how many characters too many), max (the longest note)
    uiNoteTooLongText: ({ over, max }: { over?: number | null; max?: number | null } = {}) =>
      has(over, max)
        ? `The note is ${num(over!)} ${plural(over!, 'character', 'characters')} too long. The longest note is ${num(max!)} characters: shorten it, then save again.`
        : 'The note is too long. Shorten it, then save again.',
    uiSaveLabel: 'Save',
    uiSavedText: 'Saved',

    // The target queries: the searches the site wants to be found for, held up against the month.
    uiTargetsTitle: 'Target queries',
    // args: label (the name of the status "not visible", already written), rowsFetched (how many search queries the month holds)
    uiTargetsHelpText: ({ label, rowsFetched }: { label?: string | null; rowsFetched?: number | null } = {}) =>
      `The searches the site wants to be found for, held up against this month's search queries in Search Console. A target query is a topic: every search query that contains it counts. "${has(label) ? label : 'Not visible'}" means the search is not among the ${has(rowsFetched) ? `${num(rowsFetched!)} ` : ''}most shown search queries. Google anonymizes the long tail, so that is not the same as zero impressions.`,
    uiTargetsEditorLabel: 'Target queries, one per line',
    uiTargetsEditorHelpText:
      'A search query that contains the words counts, so "blue widget" also finds "buy blue widget online". Capitals, extra spaces, duplicates and blank lines are tidied away when you save, and the list is shown as it was kept.',
    // args: count (the target queries entered), max (the most the list holds)
    uiTermsCounterText: ({ count, max }: { count?: number | null; max?: number | null } = {}) =>
      has(count, max) ? `${num(count!)} of ${num(max!)} target queries` : 'Target queries entered',
    // args: max (the most the list holds)
    uiTermsTooManyText: ({ max }: { max?: number | null } = {}) =>
      has(max) ? `Only the first ${num(max!)} are kept.` : 'Only the first ones are kept.',
    // args: count (how many are too long), max (the longest a target query can be, in characters)
    uiTermsTooLongText: ({ count, max }: { count?: number | null; max?: number | null } = {}) =>
      has(count, max)
        ? `${num(count!)} target ${plural(count!, 'query is', 'queries are')} longer than ${num(max!)} characters and will be left out.`
        : 'Target queries that are too long will be left out.',
    uiTargetsEmptyText:
      'There are no target queries yet. Add one above, for example a service the site wants to be found for, and the table shows where the site stands and what to do about it.',
    uiColumnStatusLabel: 'Status',
    uiColumnMatchesLabel: 'Matching searches',
    uiColumnAdviceLabel: 'What to do',
    // args: example (the matching search with the most impressions)
    uiTargetExampleText: ({ example }: { example?: string | null } = {}) =>
      has(example) ? `Largest search: "${example}"` : 'No search matched',
    uiTargetUnknownLabel: 'Unknown',

    // The target queries' positions month by month.
    uiHistoryTitle: 'Target queries: position by month',
    uiHistoryHelpText:
      'Your own target queries over time. The list is fixed, so a change along a row is your words moving, not the basis changing. "No data" means the search was not among that month\'s most shown search queries.',

    // The pages that gained and lost the most impressions.
    uiMovementTitle: 'Biggest movement',
    uiMovementHelpText:
      'Every page is compared both with the previous month and with the same month a year earlier: they answer different questions and can point different ways. The measure is impressions, because visibility falls before the clicks do.',
    // args: period (the month the lists are sorted on, YYYY-MM)
    uiMovementRankedPreviousText: ({ period }: { period?: string | null } = {}) =>
      `The lists are sorted on the change against ${has(period) ? `${month(period!)}, the previous month` : 'the previous month'}. It is the same basis every month, so the order means the same whichever month you are on. The comparison with last year stays on each row as a check: July against June is holiday time, and the year figure shows it.`,
    // args: period (the month the lists are sorted on, YYYY-MM)
    uiMovementRankedYearAgoText: ({ period }: { period?: string | null } = {}) =>
      `The lists are sorted on the change against ${has(period) ? month(period!) : 'the same month a year earlier'}, because the previous month is not in the archive. That is only the case for the oldest month of the archive.`,
    uiMovementLossesTitle: 'Lost the most',
    uiMovementGainsTitle: 'Gained the most',
    uiMovementNoneText:
      'There is no month to compare with yet. The archive fills in backwards by itself: once two months have page lists, the comparison appears here.',
    uiMovementEmptyText: 'No page moved against the comparison months.',
    // args: period (the comparison month, YYYY-MM)
    uiMoveAgainstLabel: ({ period }: { period?: string | null } = {}) =>
      has(period) ? `Against ${month(period!)}` : 'Against an earlier month',
    // args: delta (the change in impressions, signed, already written), percent (the change in percent, signed, already written)
    uiMoveDeltaText: ({ delta, percent }: { delta?: string | null; percent?: string | null } = {}) =>
      has(delta, percent) ? `${delta} (${percent})` : has(delta) ? delta! : 'No change',
    // args: delta (the impressions gained, already written): a page that was not shown in the comparison month has no percentage
    uiMoveNewText: ({ delta }: { delta?: string | null } = {}) => (has(delta) ? `${delta} (new)` : 'New'),
    // args: before, now (the average position then and now, already written)
    uiMovePositionText: ({ before, now }: { before?: string | null; now?: string | null } = {}) =>
      has(before, now) ? `Position ${before} to ${now}` : 'Position changed',

    // The quick wins.
    uiOpportunitiesTitle: 'Quick wins',
    uiOpportunitiesHelpText: 'The cheapest fixes come first. Each one points at a search the site is already shown for.',
    uiEffortSmallLabel: 'Small effort',
    uiEffortMediumLabel: 'Medium effort',
    uiEffortLargeLabel: 'Large effort',

    // ---- ui: the To fix tab (the findings of the report) -------------------------------------------------------------------------
    uiToFixHelpText:
      'What the report found that is worth acting on, in the order the report lists it. Each finding says what was seen and why it matters.',
    uiToFixEmptyText: 'Nothing to fix: the report has no findings for this month.',

    // ---- ui: the Settings tab (users with the Settings section). The client only. -----------------------------------------------------
    uiSettingsLoadingText: 'Reading the settings...',
    uiSettingsIntroText:
      'Connect the report to Google Analytics 4 and Search Console with a Google service account. Test the connection before you save: saving settings that work starts fetching the missing months in the background.',
    uiSetInConfigurationLabel: 'Set in configuration',
    uiLockedFieldsText:
      'Fields marked "Set in configuration" come from the site configuration, which wins over what is saved here, so they cannot be changed on this tab. Ask your developer to change them.',

    // The connection: the two properties and the key.
    uiConnectionTitle: 'Connection',
    uiGa4PropertyLabel: 'Google Analytics 4 property ID',
    uiGa4PropertyHelpText:
      'The number Google Analytics shows under Admin, then Property details, for example 123456789 (properties/123456789 works too). It is not the measurement ID that starts with G-.',
    uiSearchConsoleLegendLabel: 'Search Console property',
    uiSearchConsoleDomainLabel: 'Domain property',
    uiSearchConsolePrefixLabel: 'URL-prefix property',
    uiSearchConsoleDomainInputLabel: 'Domain, for example example.com',
    uiSearchConsolePrefixInputLabel: 'Address, for example https://www.example.com/',
    uiSearchConsoleHelpText:
      'Choose the kind Search Console lists for your site, and write it the same way. A domain property covers every subdomain and both http and https, and is written without https:// and without a path. A URL-prefix property covers only the addresses that start with exactly what you enter: http or https, www or not, and the path. The trailing slash is added if you leave it out.',
    // args: value (the Search Console property as it will be sent: sc-domain:example.com, or an address)
    uiSearchConsoleSentText: ({ value }: { value?: string | null } = {}) =>
      has(value) && value !== '' ? `Will be saved as ${value}` : 'Nothing is saved for the property.',
    uiSearchConsoleLooksLikeUrlText:
      'This looks like a full address. A domain property is written without https:// and without a path; for an address, choose URL-prefix property.',
    uiSearchConsoleMissingSchemeText: 'A URL-prefix property is the full address, starting with https:// or http://.',
    uiSearchConsoleSavedAsDomainText: 'Without it, what you entered is saved as a domain property.',
    uiSearchConsoleRefusedText:
      'Search Console will not accept the property as it is written, so it cannot be saved. Write it the way Search Console lists it.',

    // The service account key. Write-only: once it is saved it is never shown again, only whose it is.
    uiKeyLegendLabel: 'Google service account key',
    uiKeyPasteLabel: 'Paste the contents of the JSON key file',
    uiKeyHelpText:
      'The key is stored encrypted and is never shown again, not even here. Paste the whole JSON file you downloaded from Google Cloud, or choose the file: it is read in your browser and sent only when you test or save.',
    uiKeyChooseFileLabel: 'Choose .json file',
    // args: email (the service account's address)
    uiKeySavedText: ({ email }: { email?: string | null } = {}) => (has(email) ? `Key saved · service account ${email}` : 'Key saved'),
    uiKeyReplaceLabel: 'Replace',
    uiKeyReplaceHelpText: 'The saved key stays in use until a new one is saved.',
    uiKeyRemoveLabel: 'Remove',
    uiCancelLabel: 'Cancel',
    uiKeyRemoveConfirmText:
      'Remove the saved key? The report stops fetching from Google until a new key is saved. The months already fetched stay.',
    uiKeyRemoveConfirmLabel: 'Remove the key',
    uiKeyRemovedText: 'Key removed',
    // args: email (the service account's address)
    uiKeyConfiguredText: ({ email }: { email?: string | null } = {}) =>
      has(email) ? `Set in configuration · service account ${email}` : 'Set in configuration',
    uiKeyConfiguredHelpText:
      'The key comes from the site configuration, which wins over a key saved here, so it cannot be changed on this tab. Ask your developer to change it.',
    uiKeyConfiguredBrokenTitle: 'The configured key cannot be used',
    uiKeyConfiguredBrokenText:
      'A key is set in the site configuration, but it cannot be read or is not a valid service account key. Pasting a key here does not help, because the configuration wins. Ask your developer to correct the key in the site configuration, or to remove it there so that a key can be saved on this tab.',
    // args: email (the service account the key in the box belongs to)
    uiKeyDraftText: ({ email }: { email?: string | null } = {}) =>
      has(email) ? `Ready to test or save · service account ${email}` : 'Ready to test or save',
    uiKeyNotJsonText: 'This is not JSON. Paste the whole contents of the key file, from the first { to the last }.',
    uiKeyNotServiceAccountText:
      'This JSON is not a service account key: its "type" is not "service_account". In Google Cloud, create a key for a service account (Keys, Add key, Create new key, JSON).',
    uiKeyNoClientEmailText: 'The key has no "client_email". Paste the whole file as Google Cloud downloaded it.',
    uiKeyNoPrivateKeyText: 'The key has no private key ("private_key"). Paste the whole file as Google Cloud downloaded it.',
    // args: max (the largest key, in KB)
    uiKeyTooLargeText: ({ max }: { max?: number | null } = {}) =>
      `This is larger than ${has(max) ? `${num(max!)} KB` : 'a key can be'}, so it is not a service account key file. A real key is about 2 KB.`,
    uiKeyCheckHintText: 'You can still test or save: the server checks the key and has the last word.',
    uiKeyFileUnreadableText: 'The file could not be read. Choose it again, or open it and paste its contents instead.',

    // The service account's address: what has to be added in Google Analytics and Search Console. Not a secret.
    uiServiceAccountLabel: 'Service account',
    uiServiceAccountHelpText: 'Add this address as a Viewer in Google Analytics and as a user in Search Console.',
    uiCopyAddressLabel: 'Copy the address',
    uiCopiedText: 'Copied',
    uiCopyFailedText: 'Could not copy. Select the address and copy it yourself.',

    // Test connection: three rows, each with an icon and a word for how it went.
    uiTestConnectionLabel: 'Test connection',
    uiTestHelpText:
      'Tests the values in the form before you save them: the key, then Google Analytics and Search Console. With the key box empty, the key in use is tested.',
    uiTestingText: 'Testing the connection with Google...',
    uiTestResultTitle: 'Connection test',
    uiTestDoneText: 'The connection test has finished. The results are below.',
    uiTestKeyLabel: 'Service account key',
    uiTestGa4Label: 'Google Analytics',
    uiTestSearchConsoleLabel: 'Search Console',
    uiTestPassedLabel: 'Works',
    uiTestNoDataLabel: 'Connected, no data yet',
    uiTestNotTestedLabel: 'Not tested',
    uiTestFailedLabel: 'Failed',
    uiTestNotTestedText: 'Not tested, because the key could not be used.',

    // The report.
    uiReportSettingsTitle: 'Report',
    uiBrandTermsLabel: 'Brand terms, one per line',
    uiBrandTermsHelpText:
      'A search query that contains one of these counts as a brand search, for example your company or product name; the rest are non-brand. Capitals do not matter.',
    // Names the "Refresh" button of the month by its label in the dashboard: keep the two the same.
    uiBrandTermsChangeText: 'Changing brand terms applies to months fetched from then on; use "Refresh" on a month to update it.',
    // args: count (the brand terms entered), max (the most the list holds), maxLength (the longest a brand term can be, in characters)
    uiBrandTermsCounterText: ({ count, max, maxLength }: { count?: number | null; max?: number | null; maxLength?: number | null } = {}) =>
      has(count, max, maxLength)
        ? `${num(count!)} of ${num(max!)} brand terms, up to ${num(maxLength!)} characters each`
        : 'Brand terms entered',
    // args: count (how many are too long), max (the longest a brand term can be, in characters)
    uiBrandTermsTooLongText: ({ count, max }: { count?: number | null; max?: number | null } = {}) =>
      has(count, max)
        ? `${num(count!)} brand ${plural(count!, 'term is', 'terms are')} longer than ${num(max!)} characters and will be left out.`
        : 'Brand terms that are too long will be left out.',
    uiDisplayNameLabel: 'Display name (optional)',
    uiDisplayNameHelpText: 'Shown under the title of the dashboard. Leave it empty for the general description.',
    uiFindingsGroupsLegendLabel: 'Who sees "To fix"',
    uiFindingsGroupsHelpText:
      'Only members of the chosen user groups see the To fix tab and the quick wins. With no group chosen, everyone who can open the report sees them.',
    uiGroupsLoadingText: 'Reading the user groups...',
    uiGroupsEmptyText: 'There are no user groups to choose from.',
    // args: alias (a saved alias that no user group has any more)
    uiGroupUnknownText: ({ alias }: { alias?: string | null } = {}) =>
      has(alias) ? `${alias} (no longer a user group)` : 'A user group that no longer exists',

    // Fetching.
    uiFetchingTitle: 'Fetching',
    uiAutoFetchLabel: 'Fetch new months automatically',
    uiAutoFetchHelpText:
      'A background job fetches each month once it has ended and fills in any of the latest 13 months that are missing. Turned off, months are fetched only when someone presses Fetch now or "Refresh". Older months stay in the archive.',
    uiFetchNowHelpText:
      'Fetch now fetches the months that are missing from the archive, in the background, with the saved settings. To fetch one month again, choose it in the report and use "Refresh".',
    uiFetchIdleText: 'No fetch is running.',

    // Saving.
    uiSaveSettingsLabel: 'Save settings',
    uiSaveHelpText:
      'Saving checks the connection with Google, which can take up to a minute. When it works, the missing months start to fetch in the background.',
    uiSavingText: 'Saving and checking the connection with Google. This can take up to a minute.',
    uiSettingsSavedText: 'Settings saved',
    uiUnsavedText: 'You have unsaved changes.',
    uiDiscardLabel: 'Discard changes',

    // The setup guide. The Google console steps were checked against Google's own help pages (see the Settings tab's links); Google renames
    // its menus now and then, so check them again when a step stops matching.
    uiGuideTitle: 'How to connect',
    uiGuideIntroText:
      'The report reads Google Analytics and Search Console through a Google service account: an account for software, with its own e-mail address and a key file. You need access to a Google Cloud project, and the right to add users to the Google Analytics property and to the Search Console property.',
    uiGuideStepProjectText:
      'In the Google Cloud console, create a project for the report, or use one you have: open Manage resources, click Create project, enter a name and click Create.',
    uiGuideStepApisText:
      'In that project, open APIs & services, then Library. Search for Google Analytics Data API and click Enable, then do the same for Google Search Console API.',
    uiGuideStepAccountText:
      'Create the service account: on the Service accounts page, click Create service account, enter a name and click Done (it needs no roles in Google Cloud). Then click its e-mail address, open the Keys tab, click Add key, then Create new key, choose JSON and click Create. The key file downloads; keep it safe. If Google Cloud says that an organization policy does not allow keys, ask your Google Cloud administrator.',
    // args: email (the service account's address, when the tab knows it)
    uiGuideStepAccessText: ({ email }: { email?: string | null } = {}) =>
      `Give the service account read access with its e-mail address, ${has(email) ? email : 'which the key file holds as client_email and this tab shows once the key is saved'}. In Google Analytics, open Admin and, under Property, Access management; click +, then Add users, enter the address, choose the Viewer role and click Add. In Search Console, open the property, then Settings, then Users and permissions; click Add user, enter the address and choose Restricted permission. Only an owner of the Search Console property can add users.`,
    uiGuideStepFormText:
      'On this tab, paste the key or choose the file, enter the GA4 property ID (in Google Analytics under Admin, then Property details) and choose the Search Console property exactly as Search Console lists it.',
    uiGuideStepTestText:
      'Click Test connection. When the key, Google Analytics and Search Console all work, click Save settings: the report starts fetching the last 13 months in the background, newest first.',
    uiGuideLinkProjectLabel: 'Create a Google Cloud project',
    uiGuideLinkApisLabel: 'Enable an API',
    uiGuideLinkAnalyticsApiLabel: 'Google Analytics Data API in the API Library',
    uiGuideLinkSearchConsoleApiLabel: 'Google Search Console API in the API Library',
    uiGuideLinkServiceAccountLabel: 'Create a service account',
    uiGuideLinkKeyLabel: 'Create a service account key',
    uiGuideLinkAnalyticsUsersLabel: 'Add users in Google Analytics',
    uiGuideLinkSearchConsoleUsersLabel: 'Add users in Search Console',
    uiGuideLinkPropertyIdLabel: 'Find the GA4 property ID',
    uiGuideLinkPropertyTypesLabel: 'Domain and URL-prefix properties',
    uiNewTabText: '(opens in a new tab)',
  },
};
