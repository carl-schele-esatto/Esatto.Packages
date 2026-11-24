const o = (...e) => e.every((t) => t != null), s = (e) => (/* @__PURE__ */ new Date(`${e}-01T00:00:00Z`)).toLocaleDateString("en", { month: "long", year: "numeric", timeZone: "UTC" }), A = (e) => (/* @__PURE__ */ new Date(`${e}T00:00:00Z`)).toLocaleDateString("en", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" }), i = (e) => e.toLocaleString("en"), c = (e) => e.toLocaleString("en", { minimumFractionDigits: 1, maximumFractionDigits: 1 }), v = (e) => e.toLocaleString("en", { maximumFractionDigits: 2 }), G = (e) => `${Math.floor(e / 60)}:${String(e % 60).padStart(2, "0")}`, l = (e, t, a) => e === 1 ? t : a, N = {
  visits: ["visit", "visits"],
  keyEvents: ["key event", "key events"],
  clicks: ["click", "clicks"]
}, d = (e, t) => {
  const a = t ? N[t] : void 0;
  return a ? `${i(e)} ${l(e, a[0], a[1])}` : i(e);
}, b = (e) => e ? ` Final data through ${A(e)}.` : "", k = (e) => `${i(e)} ${l(e, "impression", "impressions")}`, T = (e, t, a) => o(e, t, a) ? `${e === "/" ? "The home page" : e} ranks at position ${c(t)} for ${i(a)} ${l(a, "search query", "search queries")}` : "A page ranks for this search", w = (e) => {
  const t = Math.round(e * 10) / 10;
  return `${t >= 0 ? "+" : "-"}${c(Math.abs(t))}%`;
}, D = (e) => e === "/" ? "the home page" : e, I = (e) => e === !0 ? " Both sources point the same way, so the development is not a measurement artifact." : e === !1 ? " The sources point in different directions, and the difference is measurement rather than traffic: Search Console counts clicks at Google, GA4 counts visits in the browser, where blocked scripts and declined cookie consent are not measured. Search Console is the one to trust for the trend." : "", C = (e, t, a) => {
  const n = Math.abs(e);
  return `${i(n)} ${e < 0 ? "fewer" : "more"}${a ? ` ${l(n, "impression", "impressions")}` : ""} than ${s(t)}`;
}, P = (e, t, a, n, r) => {
  const h = [];
  return o(t, a) && h.push(C(a, t, h.length === 0)), o(n, r) && h.push(C(r, n, h.length === 0)), `The biggest drop is ${o(e) ? D(e) : "a page"}${h.length > 0 ? `: ${h.join(" and ")}` : ""}.`;
}, R = (e, t, a, n, r, h, u, g, m, y) => {
  const x = o(e, t, a) ? `${i(e)} of your ${i(t)} most shown search queries rank in the top 3 (${c(a)}%).` : "Some of your most shown search queries rank in the top 3.", $ = o(n, r, h) ? ` Against ${i(r)} (${c(h)}%) in ${s(n)}${u === !0 ? "." : ", the nearest earlier month in the archive, because the same month last year is not archived yet. Season can therefore explain part of the movement."}` : "", L = o(g, m, y) ? ` The best month in the archive is ${s(g)} with ${i(m)} (${c(y)}%).` : "";
  return x + $ + L;
}, M = (e) => o(e) && e > 0 ? ` ${e === 1 ? "The last day is" : `The last ${e} days are`} preliminary: GA4 finishes processing over a couple of days, so those figures can be revised afterwards.` : "", F = (e) => e.charAt(0).toUpperCase() + e.slice(1), f = (e) => e === "ga4" ? "GA4" : e === "searchConsole" ? "Search Console" : "Google", S = (e) => e === "analyticsData" ? "the Google Analytics Data API" : e === "searchConsole" ? "the Google Search Console API" : "a Google API", O = (e) => e === "analyticsData" ? "https://console.cloud.google.com/apis/library/analyticsdata.googleapis.com" : e === "searchConsole" ? "https://console.cloud.google.com/apis/library/searchconsole.googleapis.com" : "https://console.cloud.google.com/apis/library", K = (e) => e === "ga4" ? " Also check that the number you entered is the property ID shown under Admin, then Property details, and not an account or stream ID." : "", p = (e) => o(e) ? e : "the service account's e-mail address", q = (e, t) => e === "ga4" ? `In Google Analytics, open Admin, then Property access management, and add ${p(t)} with the Viewer role.` : e === "searchConsole" ? `In Search Console, open Settings, then Users and permissions, and add ${p(t)} as a user.` : `Add ${p(t)} as a user in Google Analytics and in Search Console.`, B = {
  esattoSeoReport: {
    dashboardTitle: "SEO Report",
    // ---- kpi: the traffic KPIs (label and description) -------------------------------------------------------------
    kpiOrganicSessionsLabel: "Visits from search engines",
    kpiOrganicSessionsDescription: "Through the result lists of Google and other search engines",
    kpiOrganicEngagementRateLabel: "Engaged visits",
    kpiOrganicEngagementRateDescription: "Share who stayed or clicked on",
    kpiOrganicSessionSecondsLabel: "Average visit length",
    kpiOrganicSessionSecondsDescription: "Minutes and seconds, as in GA4",
    kpiOrganicKeyEventsLabel: "Key events from search engines",
    kpiOrganicKeyEventsDescription: "Key events GA4 recorded on visits from search engines",
    kpiTotalSessionsLabel: "All visits",
    kpiTotalSessionsDescription: "All channels, not just search engines",
    kpiGscClicksLabel: "Clicks in Google",
    kpiGscClicksDescription: "Times someone clicked through from the result list",
    kpiClicksVsSessionsLabel: "Clicks minus visits",
    kpiClicksVsSessionsDescription: "Clicks are normally more; see the note below the table",
    // ---- kpi: a Search Console segment's measures (label) --------------------------------------------------------------
    kpiBrandClicksLabel: "Clicks",
    kpiBrandImpressionsLabel: "Impressions",
    kpiBrandCtrLabel: "Click-through rate",
    kpiBrandPositionLabel: "Average position",
    kpiNonBrandClicksLabel: "Clicks",
    kpiNonBrandImpressionsLabel: "Impressions",
    kpiNonBrandCtrLabel: "Click-through rate",
    kpiNonBrandPositionLabel: "Average position",
    kpiTotalClicksLabel: "Clicks",
    kpiTotalImpressionsLabel: "Impressions",
    kpiTotalCtrLabel: "Click-through rate",
    kpiTotalPositionLabel: "Average position",
    // ---- kpi: trend texts ----------------------------------------------------------------------------------------------
    kpiVsSameMonthLastYearText: "vs the same month last year",
    // args: period (the month a year back, YYYY-MM)
    kpiNoComparisonText: ({ period: e } = {}) => o(e) ? `no comparison: ${s(e)} has no data` : "no comparison: last year has no data",
    // args: period (YYYY-MM), value (that month's figure), unit (visits | keyEvents | clicks)
    kpiAnomalousBaselineText: ({ period: e, value: t, unit: a } = {}) => o(e, t) ? `vs ${s(e)} (${d(t, a)}), an anomalous baseline` : "vs an anomalous baseline",
    // args: percent (last year's engagement rate, one decimal)
    kpiEngagementVsLastYearText: ({ percent: e } = {}) => o(e) ? `vs ${c(e)}% last year` : "vs the same month last year",
    kpiEngagementShareText: "share of visits with engagement",
    // args: seconds (last year's average visit, whole seconds)
    kpiDurationVsLastYearText: ({ seconds: e } = {}) => o(e) ? `vs ${G(e)} last year` : "vs the same month last year",
    kpiDurationPerVisitText: "minutes:seconds per visit",
    kpiClicksMinusVisitsText: "Clicks in Google minus visits from Google in GA4",
    // ---- chart: title, subtitle, description -----------------------------------------------------------------------
    chartTrafficOrganicTitle: "Visits from search engines: historical trend",
    chartTrafficOrganicSubtitle: "Visits from the search engines' result lists, per month",
    chartTrafficOrganicDescription: "This chart shows visits from the result lists of Google and other search engines, with one line per year. A visit is one stay on the site: everything someone does during the same stay counts as ONE visit, and if they come back another day, that is a new one. So the same person can account for several visits.",
    chartTrafficTotalTitle: "All visits: every channel",
    chartTrafficTotalSubtitle: "All visits per month, wherever they came from",
    chartTrafficTotalDescription: "This chart shows all visits together, however they found the site: search engines, typed straight into the address bar, links from other sites and social media. It shows whether it is the search traffic that grows or everything.",
    chartTrafficSearchComparisonTitle: "Visits against clicks: GA4 compared with Search Console",
    chartTrafficSearchComparisonSubtitle: "Visits according to GA4, clicks according to Search Console",
    chartTrafficSearchComparisonDescription: "This chart shows visits (GA4) and clicks (Search Console) side by side, year by year. A click is a click in Google's result list; a visit is the stay that follows. Clicks are therefore normally MORE: if someone clicks, goes back to Google and clicks again within half an hour, that is two clicks but one visit. On top of that, GA4 only counts visitors who have accepted cookies, while Google counts the click anyway.",
    chartTrafficSearchClicksTitle: "Clicks: brand vs non-brand",
    chartTrafficSearchClicksSubtitle: "Clicks per month, split by the brand terms in the search query",
    chartTrafficSearchClicksDescription: "This chart shows clicks per month, split by whether the search contained the brand. Brand is people already looking for you; non-brand is new visitors who find the site through something else.",
    chartTrafficSearchImpressionsTitle: "Impressions: brand vs non-brand",
    chartTrafficSearchImpressionsSubtitle: "Impressions per month, same split",
    chartTrafficSearchImpressionsDescription: "This chart shows how often the site was shown in the search results, with the same split as the clicks chart. Many impressions without clicks mean the site is seen but does not draw visits.",
    chartTrafficSearchTitle: "Search queries: brand vs non-brand (Search Console)",
    chartTrafficSearchSubtitle: "Clicks, impressions, CTR and average position",
    // ---- chart: the daily traffic chart (ChartBlock.Id "daily_traffic"). Its texts are written with the arguments the daily
    // endpoint sends beside the chart (DailyChart.TextArgs): period is null in the rolling mode. -----------------------------
    // args: period (the calendar month, YYYY-MM), windowDays (the length of the rolling window)
    chartDailyTrafficTitle: ({ period: e, windowDays: t } = {}) => o(e) ? s(e) : o(t) ? `The last ${t} days` : "Visits per day",
    chartDailyTrafficSubtitle: "Live from GA4",
    // args: period, windowDays, provisionalDays (how many of the last days GA4 may still revise: the client passes the smaller of this and
    // windowDays, and null or 0 leaves the sentence about preliminary figures out)
    chartDailyTrafficDescription: ({ period: e, windowDays: t, provisionalDays: a } = {}) => `Visits per day, with the average of ${o(e) ? "the previous month" : o(t) ? `the previous ${t} days` : "the preceding period"} as a dashed comparison.${M(a)} The figures count only visits GA4 can measure (visitors who decline cookie consent or block scripts are not counted), so they are a floor, not the whole picture. Click a day in the chart to see where the visits came from.`,
    // ---- series: label -------------------------------------------------------------------------------------------------
    // args: year (from the series key's "_YYYY" suffix)
    seriesYearLabel: ({ year: e } = {}) => o(e) ? `${e}` : "Year",
    // args: year
    seriesSessionsLabel: ({ year: e } = {}) => o(e) ? `${e} visits` : "Visits",
    // args: year
    seriesClicksLabel: ({ year: e } = {}) => o(e) ? `${e} clicks` : "Clicks",
    seriesBrandLabel: "Brand",
    seriesNonBrandLabel: "Non-brand",
    seriesTotalLabel: "Total (unfiltered)",
    seriesDailyLabel: "Visits per day",
    seriesBaselineLabel: "Average of the preceding period",
    // ---- annotation: title and text ------------------------------------------------------------------------------------
    annotationNoKeyEventsTitle: "No key events in GA4",
    annotationNoKeyEventsText: "GA4 reports zero key events in every month. That means no event is marked as a key event, not that no enquiries came in. As long as that is the case, the report can only say which pages bring traffic, not which bring business.",
    annotationAnomalousBaselineTitle: "The comparison month is not representative",
    // args: period (the comparison month, YYYY-MM), value, median (of the months around it), unit (visits | keyEvents | clicks)
    annotationAnomalousBaselineText: ({ period: e, value: t, median: a, unit: n } = {}) => `${o(e, t, a) ? `${s(e)} has ${d(t, n)} against a median of ${i(a)} in the months around it.` : "The comparison month lies far below the months around it."} The year-on-year comparison is still calculated against that month, but it measures the measurement outage rather than the development.`,
    annotationAnonymizedQueriesTitle: "Anonymized search queries",
    // args: percent (brand + non-brand as a share of all clicks, one decimal), period (YYYY-MM)
    annotationAnonymizedQueriesText: ({ percent: e, period: t } = {}) => `${o(e, t) ? `Brand and non-brand add up to ${c(e)}% of the clicks in ${s(t)}.` : "Brand and non-brand add up to less than all the clicks."} Search Console leaves out rare queries as soon as data is filtered by query, and the difference cannot be split between brand and non-brand.`,
    annotationSegmentsExceedTotalTitle: "The segments exceed the total",
    // args: percent (brand + non-brand as a share of all clicks, one decimal), period (YYYY-MM)
    annotationSegmentsExceedTotalText: ({ percent: e, period: t } = {}) => `${o(e, t) ? `Brand and non-brand add up to ${c(e)}% of the clicks in ${s(t)}, so more than 100%.` : "Brand and non-brand add up to more than all the clicks."} Search Console rounds the three aggregates separately, so a click or two of difference is expected. A larger difference suggests that the brand terms or the property setting are wrong.`,
    annotationOutsideGscWindowTitle: "Outside Search Console's 16-month window",
    // args: count (months of the curve before the window), windowStart (the window's first month, YYYY-MM)
    annotationOutsideGscWindowText: ({ count: e, windowStart: t } = {}) => o(e, t) && e === 1 ? `1 month of the curve falls before ${s(t)} and is no longer kept in Search Console. It is drawn as a gap in the curve, not as zero.` : `${o(e, t) ? `${e} months of the curve fall before ${s(t)}` : "Some months of the curve fall before the window"} and are no longer kept in Search Console. They are drawn as gaps in the curve, not as zero.`,
    annotationMissingGscMonthsTitle: "Months without Search Console data",
    // args: from, to (the first and last month without data, YYYY-MM)
    annotationMissingGscMonthsText: ({ from: e, to: t } = {}) => `${o(e, t) ? e === t ? `${s(e)} has` : `${s(e)} to ${s(t)} have` : "Some months have"} no data in Search Console. Missing data is drawn as gaps in the curve.`,
    // args: count (pages found)
    annotationPagesNotClickedTitle: ({ count: e } = {}) => o(e) ? `${e} ${l(e, "page", "pages")} seen but not clicked` : "Pages seen but not clicked",
    // args: count, pages (up to three shortened paths, comma separated), position, impressions, ctrPercent (the thresholds)
    annotationPagesNotClickedText: ({ count: e, pages: t, position: a, impressions: n, ctrPercent: r } = {}) => `${o(e, t, a, n, r) ? `${t} ${l(e, "ranks", "rank")} at position ${c(a)} or better with at least ${i(n)} impressions, but under ${i(r)}% CTR.` : "These pages rank well and are seen often, but are hardly clicked."} So the ranking is already there; it is the title and meta description in the search result that make nobody click.`,
    // args: count (pages found)
    annotationPagesRankDeepTitle: ({ count: e } = {}) => o(e) ? `${e} ${l(e, "page", "pages")} shown but ranking deep` : "Pages shown but ranking deep",
    // args: count, pages (up to three shortened paths, comma separated), impressions, position (the thresholds)
    annotationPagesRankDeepText: ({ count: e, pages: t, impressions: a, position: n } = {}) => `${o(e, t, a, n) ? `${t} ${l(e, "has", "have")} at least ${i(a)} impressions but an average position worse than ${c(n)}:` : "These pages are seen often but rank on average"} page two or deeper, where almost nobody clicks. The demand is there; the ranking is not.`,
    annotationNonBrandNotClickedTitle: "Non-brand: visibility without clicks",
    // args: impressions, clicks, ctrPercent (up to two decimals), position (one decimal)
    annotationNonBrandNotClickedText: ({ impressions: e, clicks: t, ctrPercent: a, position: n } = {}) => `${o(e, t, a, n) ? `${i(e)} non-brand impressions gave ${i(t)} ${l(t, "click", "clicks")} (${v(a)}% CTR) at an average position of ${c(n)}.` : "Non-brand searches show the site often but give few clicks."} The site is shown for searches that do not contain the brand, but does not capture them.`,
    // ---- opportunity: title and text (a reference's arguments go to both) ------------------------------------------
    // args: query (the search query)
    opportunityNearTitle: ({ query: e } = {}) => o(e) ? `"${e}" is close to page one` : "A search query is close to page one",
    // args: position (one decimal), impressions
    opportunityNearText: ({ position: e, impressions: t } = {}) => `${o(e, t) ? `Position ${c(e)} with ${k(t)}.` : "It ranks on page two."} The page exists and is seen: it needs to get better at this very query, not be replaced.`,
    // args: query
    opportunityCannibalTitle: ({ query: e } = {}) => o(e) ? `Two pages compete for "${e}"` : "Two pages compete for the same query",
    // args: firstPath, firstPosition (one decimal), firstImpressions, secondPath, secondPosition, secondImpressions
    opportunityCannibalText: ({ firstPath: e, firstPosition: t, firstImpressions: a, secondPath: n, secondPosition: r, secondImpressions: h } = {}) => `${o(e, t, a, n, r, h) ? `${e} (position ${c(t)}, ${k(a)}) and ${n} (position ${c(r)}, ${k(h)}) are both shown for the query.` : "Two of the site's own pages are both shown for the query."} Choose which one should own it, and let the other link to it.`,
    // ---- target: a target query's status (label) and its advice (text) -------------------------------------------------
    targetMissingLabel: "Not visible",
    targetDeepLabel: "Ranking deep",
    targetNearLabel: "Close to page one",
    targetTitleLabel: "Not clicked",
    targetPerformingLabel: "Performing",
    // Names the "Refresh" button of the month by its label in the dashboard: keep the two the same.
    targetMissingNoQueriesText: `The month's search queries are missing from the report, so this search cannot be assessed. Use "Refresh" on the month to fetch them.`,
    // args: rowsFetched (how many search queries were fetched)
    targetMissingText: ({ rowsFetched: e } = {}) => `No content answers the topic. The search is not among the ${o(e) ? `${i(e)} ` : ""}most shown search queries this month. Google anonymizes the long tail, so this means "invisible in what we can see", not proven zero.`,
    targetDeepText: "Demand is there, the ranking is not. Work on the content of the page that should answer the search, and link to it internally from pages that already rank.",
    targetNearText: "One step from page one. Strengthen the page that already ranks: it does not need to be replaced, it needs to get better at this very search.",
    targetTitleText: "The ranking is won, the click is not. Rewrite the title and meta description so they answer the search: it is the cheapest fix there is.",
    targetPerformingText: "Nothing to do.",
    // args: path, position (one decimal), queries (how many of the term's search queries the page ranks for)
    targetPageDeepText: ({ path: e, position: t, queries: a } = {}) => `${T(e, t, a)}. Demand is there, the ranking is not: work on the content of that page and link to it internally from pages that already rank.`,
    // args: path, position, queries
    targetPageNearText: ({ path: e, position: t, queries: a } = {}) => `${T(e, t, a)}. One step from page one: strengthen that page, it does not need to be replaced.`,
    // args: path, position, queries, ctrPercent (up to two decimals)
    targetPageTitleText: ({ path: e, position: t, queries: a, ctrPercent: n } = {}) => `${T(e, t, a)}${o(n) ? ` with a click-through rate of ${v(n)}%` : ""}. The ranking is won, the click is not: rewrite the title and meta description of that very page.`,
    // args: path, position, queries
    targetPagePerformingText: ({ path: e, position: t, queries: a } = {}) => `${T(e, t, a)}. Nothing to do.`,
    // ---- narrative: the month in words (Statement.Text, part Text). A part a statement has nothing to say about arrives as null.
    // args: clicks, clicksChange (percent), sessions, sessionsChange (percent), agree (true when both sources move the same way)
    narrativeSourcesText: ({ clicks: e, clicksChange: t, sessions: a, sessionsChange: n, agree: r } = {}) => `${o(e, t, a, n) ? `Search Console shows ${d(e, "clicks")} (${w(t)}), GA4 shows ${d(a, "visits")} from search engines (${w(n)}).` : "Search Console clicks are set against GA4 visits from search engines."}${I(r)}`,
    // args: top3, total (the search queries examined), percent (one decimal); comparePeriod (YYYY-MM), compareTop3, comparePercent,
    // yearOnYear (true: the same month a year earlier, false: the nearest earlier month); bestPeriod, bestTop3, bestPercent
    narrativeVisibilityText: ({ top3: e, total: t, percent: a, comparePeriod: n, compareTop3: r, comparePercent: h, yearOnYear: u, bestPeriod: g, bestTop3: m, bestPercent: y } = {}) => R(e, t, a, n, r, h, u, g, m, y),
    // args: clicks (non-brand clicks), total (all clicks), percent (the share, one decimal)
    narrativeNonBrandText: ({ clicks: e, total: t, percent: a } = {}) => `${o(e, t, a) ? `Non-brand accounts for ${i(e)} of ${d(t, "clicks")} (${c(a)}%).` : "Non-brand accounts for part of the clicks."} Growth has to come from there: brand searches follow awareness of the brand, not the content work.`,
    // args: sessions (visits from search engines), events (key events from them)
    narrativeConversionText: ({ sessions: e, events: t } = {}) => o(e, t) ? `${d(e, "visits")} from search engines led to ${d(t, "keyEvents")}.` : "Visits from search engines led to key events.",
    // args: path ("/" is the home page), yearAgoPeriod (YYYY-MM), yearAgoDelta (impressions against that month, negative is fewer),
    // previousPeriod, previousDelta; position, positionBefore (one decimal)
    narrativeMovementText: ({ path: e, yearAgoPeriod: t, yearAgoDelta: a, previousPeriod: n, previousDelta: r, position: h, positionBefore: u } = {}) => `${P(e, t, a, n, r)}${o(h, u) ? ` The position went from ${c(u)} to ${c(h)}.` : ""}`,
    // args: period (the one month the movement is compared with, YYYY-MM)
    narrativeSeasonText: ({ period: e } = {}) => `The movement is only compared with ${o(e) ? s(e) : "one earlier month"}, because the same month last year is not in the archive. Part of the movement can therefore be season and not development.`,
    // ---- ui: sources, their notes, the report status ---------------------------------------------------------------
    uiGa4Label: "GA4",
    uiGscLabel: "Search Console",
    uiGa4IncompleteText: "The report period lacks complete GA4 data.",
    // args: dataThrough (optional, the last day with final data, YYYY-MM-DD)
    uiGscLagText: ({ dataThrough: e } = {}) => `2-3 days of data lag in Search Console.${b(e)}`,
    // args: period (the month the Search Console figures refer to, YYYY-MM), dataThrough (optional, YYYY-MM-DD)
    uiGscIncompleteText: ({ period: e, dataThrough: t } = {}) => `The report period is not complete in Search Console: all Search Console figures refer to ${o(e) ? s(e) : "an earlier month"}.${b(t)}`,
    // args: period (the report month, YYYY-MM), dataThrough (optional, YYYY-MM-DD)
    uiGscNoDataText: ({ period: e, dataThrough: t } = {}) => `Search Console has no data for ${o(e) ? s(e) : "this month"}.${b(t ?? void 0)}`,
    uiNoDataText: "No data yet.",
    // ---- error: what went wrong and what to do about it (a problem's code gives the key: apiDisabled -> errorApiDisabledTitle and
    // errorApiDisabledText, see ErrorCodes). The server sends the code and, for some, "api" or "source"; the CLIENT adds "email", the
    // service account's address from the key status, to the problems whose cure is "add this address". -----------------------------
    errorNotConfiguredTitle: "SEO Report is not set up yet",
    errorNotConfiguredText: 'There is no Google service account key yet. Open the Settings tab, paste the key, enter the GA4 property ID and the Search Console property, and run "Test connection". If you do not have a key, ask whoever manages your Google Cloud project to create one.',
    errorKeyUnreadableTitle: "The saved key can't be read",
    errorKeyUnreadableText: "A key is saved, but this environment cannot decrypt it. That happens after a database is restored into another environment or when the site's data protection keys change. Open the Settings tab and enter the key again, or ask your developer to supply it in the site configuration.",
    errorKeyInvalidTitle: "The service account key is not valid",
    errorKeyInvalidText: "The key is not a usable Google service account key, or Google no longer accepts it: the key may have been deleted or disabled in Google Cloud, or its service account may have been. Create a new key in Google Cloud (Service accounts, the service account, Keys, Add key, Create new key, JSON) and paste the whole JSON file on the Settings tab. If the key is set in the site configuration instead, ask your developer to replace it there: a configured key cannot be changed in the backoffice. If you cannot create a key, ask whoever manages your Google Cloud project.",
    // args: api (analyticsData | searchConsole, the API that is switched off)
    errorApiDisabledTitle: ({ api: e } = {}) => `${F(S(e))} is switched off`,
    // args: api
    errorApiDisabledText: ({ api: e } = {}) => `Google refused the request because ${S(e)} is not enabled in the Google Cloud project that the service account belongs to. Enable it at ${O(e)}, with the project of the service account selected, wait a minute and test again. If you cannot do that yourself, ask whoever manages your Google Cloud project.`,
    // args: source (ga4 | searchConsole, the source that refused)
    errorNoAccessTitle: ({ source: e } = {}) => `No access to ${f(e)}`,
    // args: source, email (the service account's address; added by the client)
    errorNoAccessText: ({ source: e, email: t } = {}) => `${f(e)} did not let the service account in. ${q(e, t)}${K(e)} Wait a minute, then test again. If you cannot add users yourself, ask whoever administers the property.`,
    // args: source (ga4 | searchConsole, the source that does not know the property)
    errorPropertyNotFoundTitle: ({ source: e } = {}) => e === "ga4" || e === "searchConsole" ? `${f(e)} property not found` : "Property not found",
    // args: source, email (the service account's address; added by the client)
    errorPropertyNotFoundText: ({ source: e, email: t } = {}) => e === "ga4" ? `GA4 does not know the property ID you entered, or the service account cannot see it. Enter the number under Admin, then Property details, in Google Analytics, and make sure ${p(t)} has been added to that very property.` : e === "searchConsole" ? `Search Console does not know the property you entered, or the service account cannot see it. Enter it exactly as Search Console lists it: sc-domain:example.com for a domain property, or the full address for a URL-prefix property, with http or https, www or not, and the trailing slash, as in https://www.example.com/. The wrong form never matches. Make sure ${p(t)} has been added as a user of that property.` : `Google does not know the property you entered, or the service account cannot see it. Check the GA4 property ID and the Search Console property on the Settings tab, and make sure ${p(t)} has been given access to both.`,
    // args: source (ga4 | searchConsole, the source that has nothing yet)
    errorNoDataTitle: ({ source: e } = {}) => e === "ga4" || e === "searchConsole" ? `${f(e)} is connected, but has no data yet` : "Connected, but no data yet",
    // args: source
    errorNoDataText: ({ source: e } = {}) => e === "ga4" ? "The connection works, but GA4 has no visits for yesterday. A new property needs a day or two of traffic, and its measurement code has to be on the site. There is nothing to fix here: the report fills in as data arrives. Test again tomorrow." : e === "searchConsole" ? "The connection works, but Search Console has no data for this property yet. A newly added property takes a few days to show clicks and impressions. If it stays empty, check that it is the property of the right site and that its form matches: a domain property or a URL prefix." : "The connection works, but there is no data yet. Test again in a few days.",
    errorQuotaTitle: "Google request limit reached",
    errorQuotaText: "Google limits how many requests the service account's project may make, and that limit is reached for now. Wait a few minutes (until tomorrow if the daily limit is used up) and try again. If it keeps happening, ask whoever manages the Google Cloud project to look at its quota.",
    errorUnknownTitle: "Something went wrong",
    errorUnknownText: "The request failed for a reason the report cannot name. Try again in a moment. If it keeps failing, ask your developer to check the site log.",
    errorGa4PropertyInvalidTitle: "The GA4 property ID is not valid",
    errorGa4PropertyInvalidText: "Enter the number from Google Analytics, under Admin, then Property details, for example 123456789. It is not the measurement ID that starts with G-.",
    errorSearchConsoleInvalidTitle: "The Search Console property is not valid",
    errorSearchConsoleInvalidText: "Enter a domain property as example.com (or sc-domain:example.com), or a URL-prefix property as the full address with https:// and the trailing slash, for example https://www.example.com/. Spaces and other kinds of address are not accepted.",
    // ---- error: what the API itself refuses (ApiCodes: a request that is not valid, as opposed to a failure at Google). Same
    // keys as above, registered in ReportKeys from ApiCodes.All. None of them has arguments. -------------------------------------
    errorPeriodInvalidTitle: "That is not a month",
    errorPeriodInvalidText: "A month has to be written as year and month, for example 2026-07. Pick a month from the list instead of typing one, then try again.",
    errorPeriodOutOfRangeTitle: "That month cannot be fetched",
    errorPeriodOutOfRangeText: "A month can only be fetched once it has ended, and Search Console keeps only about sixteen months of data. Pick a month from the list.",
    errorDateInvalidTitle: "That is not a date",
    errorDateInvalidText: "A date has to be written as year, month and day, for example 2026-07-05. Click a day in the chart instead of typing one.",
    errorDateOutOfRangeTitle: "That day cannot be shown",
    errorDateOutOfRangeText: "The daily chart shows the last twelve months, up to today. Pick a month or a day inside that range.",
    errorAlreadyRunningTitle: "A fetch is already running",
    errorAlreadyRunningText: "Another fetch is in progress, here or on another server. Wait until it has finished; the report updates by itself.",
    errorRequestInvalidTitle: "The request was not complete",
    errorRequestInvalidText: "The page sent something the server could not use. Reload the page and try again. If it keeps happening, ask your developer to check the site log.",
    // ---- ui: the dashboard itself. The client only; the server never sends these, so ReportKeys does not register them. ------------
    uiSubtitleText: "Traffic and search from Google Analytics and Search Console",
    uiLoadingLabel: "Loading the report",
    uiTabsLabel: "Report sections",
    uiTabAnalysisLabel: "Analysis",
    uiTabTrafficLabel: "Traffic",
    uiTabQueriesLabel: "Queries",
    uiTabTopPagesLabel: "Most clicked",
    uiTabToFixLabel: "To fix",
    uiTabComparisonLabel: "Analytics vs Search Console",
    uiTabSettingsLabel: "Settings",
    uiMonthPickerLabel: "Report month",
    uiRefreshLabel: "Refresh",
    // args: period (the month being fetched, YYYY-MM)
    uiRefreshingText: ({ period: e } = {}) => `Fetching ${o(e) ? s(e) : "the month"} from Google Analytics and Search Console. This takes about fifteen seconds.`,
    uiFetchNowLabel: "Fetch now",
    uiTryAgainLabel: "Try again",
    uiDismissLabel: "Dismiss",
    // args: when (the moment, already written for the reader: "3 hours ago" or a date)
    uiFetchedText: ({ when: e } = {}) => o(e) ? `Fetched ${e}` : "Fetched",
    // args: period (YYYY-MM)
    uiReportPeriodText: ({ period: e } = {}) => o(e) ? `Report period ${s(e)}` : "Report period",
    // args: traceId (the server's id of the request, for the person who reads the log)
    uiProblemReferenceText: ({ traceId: e } = {}) => o(e) ? `Reference ${e}. Give it to your developer if the problem comes back.` : "No reference was given.",
    uiProgressTitle: "Fetching your data from Google",
    uiProgressStartingText: "Starting the fetch...",
    // args: done (months finished), total (months to fetch), period (the month being fetched, YYYY-MM; optional)
    uiProgressText: ({ done: e, total: t, period: a } = {}) => o(e, t) ? `${i(e)} of ${i(t)} ${l(t, "month", "months")} fetched${o(a) ? `, now ${s(a)}` : ""}.` : "Fetching months from Google.",
    uiProgressHintText: "You can leave this page: the fetch carries on in the background.",
    uiNotFetchedTitle: "Nothing has been fetched yet",
    uiNotFetchedText: "The scheduled fetch fills the archive in the background, one month at a time. To start at once instead, press Fetch now. The first fetch takes a few minutes.",
    // args: period (the latest month that should be archived, YYYY-MM)
    uiArchiveBehindText: ({ period: e } = {}) => `${o(e) ? s(e) : "The latest month"} is not in the archive yet. The scheduled fetch normally adds a new month soon after it has ended. To get it now, press Fetch now.`,
    uiNothingToFetchText: "There was nothing to fetch: the archive is up to date.",
    // args: done (months fetched)
    uiFetchedMonthsText: ({ done: e } = {}) => o(e) ? `Fetched ${i(e)} ${l(e, "month", "months")}.` : "Fetched the missing months.",
    uiFetchFailedTitle: "The last fetch did not finish",
    uiFetchLostTitle: "Lost contact with the server",
    uiFetchLostText: "The fetch may still be running. Try again to keep following it.",
    uiEmptyMonthTitle: "No data for this month",
    uiEmptyMonthText: "Neither Google Analytics nor Search Console returned anything for this month. A newly connected property needs a few days before data arrives. If it stays empty, ask whoever manages the connection to check it on the Settings tab.",
    // args: period (YYYY-MM)
    uiNoReportText: ({ period: e } = {}) => `There is no report for ${o(e) ? s(e) : "this month"} in the archive. Press Refresh to fetch it from Google.`,
    uiOverviewLabel: "Overview of the report month",
    uiRingOfVisitsText: "of visits",
    // args: label (the figure's name), value (the share, already written: 62.4%)
    uiRingAltText: ({ label: e, value: t } = {}) => o(e, t) ? `${e}: ${t} of visits` : "Share of visits",
    uiNoValueText: "No data",
    uiKeyFiguresLabel: "Key figures",
    uiTrendUpLabel: "Up",
    uiTrendDownLabel: "Down",
    uiTrendFlatLabel: "Change",
    uiDataNotesLabel: "Notes on the data",
    // args: source (the source's name), note (the note, already written)
    uiSourceNoteText: ({ source: e, note: t } = {}) => o(e, t) ? `${e}: ${t}` : o(t) ? t : "No note.",
    uiMissingLabel: "Still needed",
    uiMissingKeyLabel: "Service account key",
    uiMissingGa4PropertyIdLabel: "GA4 property ID",
    uiMissingSearchConsoleSiteLabel: "Search Console property",
    uiAskAdministratorText: "Only people with access to the Settings section can connect Google. Ask an administrator to open Settings and finish the setup.",
    uiFindSettingsText: "Open Settings to finish the setup. If you cannot see it, ask an administrator.",
    // ---- ui: reading the report (charts, their data tables, notes). The client only. ----------------------------------------------
    // args: from, to (the first and last point of the chart, already written: two months, two days or two years)
    uiChartRangeText: ({ from: e, to: t } = {}) => o(e) ? !o(t) || e === t ? e : `${e} to ${t}` : "The period shown",
    // args: title (the chart's title)
    uiChartTableCaption: ({ title: e } = {}) => o(e) && e !== "" ? `Data table: ${e}` : "Data table",
    uiChartLegendLabel: "Show or hide a line",
    uiChartFailedText: "The chart could not be drawn. Reload the page to try again.",
    // The kind of a note under a chart, written beside its icon: never told by colour alone.
    uiNoteWarningLabel: "Warning",
    uiNoteInfoLabel: "Note",
    uiNoteInsightLabel: "Insight",
    // ---- ui: the search tabs ---------------------------------------------------------------------------------------------------
    uiSearchTotalTitle: "Search Console in total",
    uiBrandSplitOffText: "Brand and non-brand searches are not split, because no brand terms are set. Add them on the Settings tab to see the split in a table and two charts.",
    uiHelpSegmentsText: "Clicks are visits from the search result. Impressions are how many times the site appeared in the result list. Click-through rate is the share of impressions that became a click. Average position is where the site ranked on average, where 1 is the very top.",
    uiHelpQueriesText: "Clicks are visits the search brought. Impressions are how many times the site appeared for the search. Click-through rate is the share of impressions that became a click. Average position is where the site ranked for the search on average, where 1 is the very top.",
    uiHelpPagesText: "Clicks are visits that landed on the page. Impressions are how many times the page appeared in the result list. Click-through rate is the share of impressions that became a click. Average position is where the page ranked on average, where 1 is the very top.",
    uiTopQueriesTitle: "Most clicked search queries",
    uiTopPagesTitle: "Most clicked landing pages",
    uiTopListsNoteText: "The top lists do not add up to the total. Search Console leaves out rare search queries as soon as data is filtered by query or by page, and that difference cannot be divided up.",
    uiColumnQueryLabel: "Search query",
    uiColumnPageLabel: "Page",
    uiColumnSegmentLabel: "Segment",
    uiColumnChannelLabel: "Channel",
    uiColumnSourceLabel: "Source",
    uiColumnVisitsLabel: "Visits",
    uiColumnClicksLabel: "Clicks",
    uiColumnImpressionsLabel: "Impressions",
    uiColumnCtrLabel: "Click-through rate",
    uiColumnPositionLabel: "Average position",
    uiColumnMonthLabel: "Month",
    uiColumnDayLabel: "Day",
    uiColumnPeriodLabel: "Period",
    // ---- ui: the daily traffic card (live from GA4, on the Traffic tab) -------------------------------------------------------------
    uiDailyTitle: "Traffic by day",
    uiDailyPeriodLabel: "Period",
    // args: windowDays (the length of the rolling window; it is only known once it has been read)
    uiDailyRollingLabel: ({ windowDays: e } = {}) => o(e) ? `Last ${e} days` : "Recent days",
    uiDailyLoadingText: "Fetching the daily figures from GA4...",
    uiDailyVisitsLabel: "visits",
    uiDailyNoComparisonText: "no comparison",
    uiDailyAgainstMonthText: "vs the previous month",
    // args: windowDays (the length of the window, which the period before has too)
    uiDailyAgainstDaysText: ({ windowDays: e } = {}) => o(e) ? `vs the previous ${e} days` : "vs the period before",
    // args: day (the busiest day, already written with its weekday), sessions (its visits, already written)
    uiDailyPeakText: ({ day: e, sessions: t } = {}) => o(e, t) ? `Peak day: ${e}, ${t} visits.` : "No peak day.",
    uiDailyDayPickerLabel: "Where a day's visits came from",
    uiDailyPickDayLabel: "Pick a day...",
    // args: day (written with its weekday), sessions (written)
    uiDailyDayOptionText: ({ day: e, sessions: t } = {}) => o(e, t) ? `${e}: ${t}` : o(e) ? e : "A day",
    // args: day (written with its weekday)
    uiDailySourcesTitle: ({ day: e } = {}) => o(e) ? `Where the visits came from on ${e}` : "Where the visits came from",
    // args: day
    uiDailySourcesLoadingText: ({ day: e } = {}) => o(e) ? `Fetching the sources for ${e}...` : "Fetching the sources...",
    // args: day
    uiDailySourcesNoneText: ({ day: e } = {}) => o(e) ? `No visits were recorded on ${e}.` : "No visits were recorded.",
    // ---- ui: the Analysis tab (read from the archive; the month's note and the target queries are edited here) ------------------------
    uiAnalysisLoadingText: "Reading the analysis...",
    uiAnalysisNoneText: "There is nothing to analyze yet. The analysis is built from the archived months, which fill in as they are fetched.",
    uiAnalysisUpdatingText: "Updating the target queries...",
    uiSummaryTitle: "The month in words",
    uiSummaryEmptyText: "There are too few figures to summarize the month yet. The summary only says what can be worked out from the data.",
    // The month's note: a person's words, kept apart from the statements above, which are worked out from the figures.
    uiMonthNoteTitle: "Note for the month",
    uiMonthNoteLabel: "What happened this month that the figures do not show? A launch, an outage, a campaign, a change in the measurement.",
    uiMonthNoteHelpText: "The note is written by a person and is kept apart from the statements above, which are worked out from the figures. It belongs to this month only; saving it empty removes it.",
    // args: length (the note's length without the white space around it), max (the longest note)
    uiNoteCounterText: ({ length: e, max: t } = {}) => o(e, t) ? `${i(e)} of ${i(t)} characters` : "Characters used",
    // args: over (how many characters too many), max (the longest note)
    uiNoteTooLongText: ({ over: e, max: t } = {}) => o(e, t) ? `The note is ${i(e)} ${l(e, "character", "characters")} too long. The longest note is ${i(t)} characters: shorten it, then save again.` : "The note is too long. Shorten it, then save again.",
    uiSaveLabel: "Save",
    uiSavedText: "Saved",
    // The target queries: the searches the site wants to be found for, held up against the month.
    uiTargetsTitle: "Target queries",
    // args: label (the name of the status "not visible", already written), rowsFetched (how many search queries the month holds)
    uiTargetsHelpText: ({ label: e, rowsFetched: t } = {}) => `The searches the site wants to be found for, held up against this month's search queries in Search Console. A target query is a topic: every search query that contains it counts. "${o(e) ? e : "Not visible"}" means the search is not among the ${o(t) ? `${i(t)} ` : ""}most shown search queries. Google anonymizes the long tail, so that is not the same as zero impressions.`,
    uiTargetsEditorLabel: "Target queries, one per line",
    uiTargetsEditorHelpText: 'A search query that contains the words counts, so "blue widget" also finds "buy blue widget online". Capitals, extra spaces, duplicates and blank lines are tidied away when you save, and the list is shown as it was kept.',
    // args: count (the target queries entered), max (the most the list holds)
    uiTermsCounterText: ({ count: e, max: t } = {}) => o(e, t) ? `${i(e)} of ${i(t)} target queries` : "Target queries entered",
    // args: max (the most the list holds)
    uiTermsTooManyText: ({ max: e } = {}) => o(e) ? `Only the first ${i(e)} are kept.` : "Only the first ones are kept.",
    // args: count (how many are too long), max (the longest a target query can be, in characters)
    uiTermsTooLongText: ({ count: e, max: t } = {}) => o(e, t) ? `${i(e)} target ${l(e, "query is", "queries are")} longer than ${i(t)} characters and will be left out.` : "Target queries that are too long will be left out.",
    uiTargetsEmptyText: "There are no target queries yet. Add one above, for example a service the site wants to be found for, and the table shows where the site stands and what to do about it.",
    uiColumnStatusLabel: "Status",
    uiColumnMatchesLabel: "Matching searches",
    uiColumnAdviceLabel: "What to do",
    // args: example (the matching search with the most impressions)
    uiTargetExampleText: ({ example: e } = {}) => o(e) ? `Largest search: "${e}"` : "No search matched",
    uiTargetUnknownLabel: "Unknown",
    // The target queries' positions month by month.
    uiHistoryTitle: "Target queries: position by month",
    uiHistoryHelpText: `Your own target queries over time. The list is fixed, so a change along a row is your words moving, not the basis changing. "No data" means the search was not among that month's most shown search queries.`,
    // The pages that gained and lost the most impressions.
    uiMovementTitle: "Biggest movement",
    uiMovementHelpText: "Every page is compared both with the previous month and with the same month a year earlier: they answer different questions and can point different ways. The measure is impressions, because visibility falls before the clicks do.",
    // args: period (the month the lists are sorted on, YYYY-MM)
    uiMovementRankedPreviousText: ({ period: e } = {}) => `The lists are sorted on the change against ${o(e) ? `${s(e)}, the previous month` : "the previous month"}. It is the same basis every month, so the order means the same whichever month you are on. The comparison with last year stays on each row as a check: July against June is holiday time, and the year figure shows it.`,
    // args: period (the month the lists are sorted on, YYYY-MM)
    uiMovementRankedYearAgoText: ({ period: e } = {}) => `The lists are sorted on the change against ${o(e) ? s(e) : "the same month a year earlier"}, because the previous month is not in the archive. That is only the case for the oldest month of the archive.`,
    uiMovementLossesTitle: "Lost the most",
    uiMovementGainsTitle: "Gained the most",
    uiMovementNoneText: "There is no month to compare with yet. The archive fills in backwards by itself: once two months have page lists, the comparison appears here.",
    uiMovementEmptyText: "No page moved against the comparison months.",
    // args: period (the comparison month, YYYY-MM)
    uiMoveAgainstLabel: ({ period: e } = {}) => o(e) ? `Against ${s(e)}` : "Against an earlier month",
    // args: delta (the change in impressions, signed, already written), percent (the change in percent, signed, already written)
    uiMoveDeltaText: ({ delta: e, percent: t } = {}) => o(e, t) ? `${e} (${t})` : o(e) ? e : "No change",
    // args: delta (the impressions gained, already written): a page that was not shown in the comparison month has no percentage
    uiMoveNewText: ({ delta: e } = {}) => o(e) ? `${e} (new)` : "New",
    // args: before, now (the average position then and now, already written)
    uiMovePositionText: ({ before: e, now: t } = {}) => o(e, t) ? `Position ${e} to ${t}` : "Position changed",
    // The quick wins.
    uiOpportunitiesTitle: "Quick wins",
    uiOpportunitiesHelpText: "The cheapest fixes come first. Each one points at a search the site is already shown for.",
    uiEffortSmallLabel: "Small effort",
    uiEffortMediumLabel: "Medium effort",
    uiEffortLargeLabel: "Large effort",
    // ---- ui: the To fix tab (the findings of the report) -------------------------------------------------------------------------
    uiToFixHelpText: "What the report found that is worth acting on, in the order the report lists it. Each finding says what was seen and why it matters.",
    uiToFixEmptyText: "Nothing to fix: the report has no findings for this month.",
    // ---- ui: the Settings tab (users with the Settings section). The client only. -----------------------------------------------------
    uiSettingsLoadingText: "Reading the settings...",
    uiSettingsIntroText: "Connect the report to Google Analytics 4 and Search Console with a Google service account. Test the connection before you save: saving settings that work starts fetching the missing months in the background.",
    uiSetInConfigurationLabel: "Set in configuration",
    uiLockedFieldsText: 'Fields marked "Set in configuration" come from the site configuration, which wins over what is saved here, so they cannot be changed on this tab. Ask your developer to change them.',
    // The connection: the two properties and the key.
    uiConnectionTitle: "Connection",
    uiGa4PropertyLabel: "Google Analytics 4 property ID",
    uiGa4PropertyHelpText: "The number Google Analytics shows under Admin, then Property details, for example 123456789 (properties/123456789 works too). It is not the measurement ID that starts with G-.",
    uiSearchConsoleLegendLabel: "Search Console property",
    uiSearchConsoleDomainLabel: "Domain property",
    uiSearchConsolePrefixLabel: "URL-prefix property",
    uiSearchConsoleDomainInputLabel: "Domain, for example example.com",
    uiSearchConsolePrefixInputLabel: "Address, for example https://www.example.com/",
    uiSearchConsoleHelpText: "Choose the kind Search Console lists for your site, and write it the same way. A domain property covers every subdomain and both http and https, and is written without https:// and without a path. A URL-prefix property covers only the addresses that start with exactly what you enter: http or https, www or not, and the path. The trailing slash is added if you leave it out.",
    // args: value (the Search Console property as it will be sent: sc-domain:example.com, or an address)
    uiSearchConsoleSentText: ({ value: e } = {}) => o(e) && e !== "" ? `Will be saved as ${e}` : "Nothing is saved for the property.",
    uiSearchConsoleLooksLikeUrlText: "This looks like a full address. A domain property is written without https:// and without a path; for an address, choose URL-prefix property.",
    uiSearchConsoleMissingSchemeText: "A URL-prefix property is the full address, starting with https:// or http://.",
    uiSearchConsoleSavedAsDomainText: "Without it, what you entered is saved as a domain property.",
    uiSearchConsoleRefusedText: "Search Console will not accept the property as it is written, so it cannot be saved. Write it the way Search Console lists it.",
    // The service account key. Write-only: once it is saved it is never shown again, only whose it is.
    uiKeyLegendLabel: "Google service account key",
    uiKeyPasteLabel: "Paste the contents of the JSON key file",
    uiKeyHelpText: "The key is stored encrypted and is never shown again, not even here. Paste the whole JSON file you downloaded from Google Cloud, or choose the file: it is read in your browser and sent only when you test or save.",
    uiKeyChooseFileLabel: "Choose .json file",
    // args: email (the service account's address)
    uiKeySavedText: ({ email: e } = {}) => o(e) ? `Key saved · service account ${e}` : "Key saved",
    uiKeyReplaceLabel: "Replace",
    uiKeyReplaceHelpText: "The saved key stays in use until a new one is saved.",
    uiKeyRemoveLabel: "Remove",
    uiCancelLabel: "Cancel",
    uiKeyRemoveConfirmText: "Remove the saved key? The report stops fetching from Google until a new key is saved. The months already fetched stay.",
    uiKeyRemoveConfirmLabel: "Remove the key",
    uiKeyRemovedText: "Key removed",
    // args: email (the service account's address)
    uiKeyConfiguredText: ({ email: e } = {}) => o(e) ? `Set in configuration · service account ${e}` : "Set in configuration",
    uiKeyConfiguredHelpText: "The key comes from the site configuration, which wins over a key saved here, so it cannot be changed on this tab. Ask your developer to change it.",
    uiKeyConfiguredBrokenTitle: "The configured key cannot be used",
    uiKeyConfiguredBrokenText: "A key is set in the site configuration, but it cannot be read or is not a valid service account key. Pasting a key here does not help, because the configuration wins. Ask your developer to correct the key in the site configuration, or to remove it there so that a key can be saved on this tab.",
    // args: email (the service account the key in the box belongs to)
    uiKeyDraftText: ({ email: e } = {}) => o(e) ? `Ready to test or save · service account ${e}` : "Ready to test or save",
    uiKeyNotJsonText: "This is not JSON. Paste the whole contents of the key file, from the first { to the last }.",
    uiKeyNotServiceAccountText: 'This JSON is not a service account key: its "type" is not "service_account". In Google Cloud, create a key for a service account (Keys, Add key, Create new key, JSON).',
    uiKeyNoClientEmailText: 'The key has no "client_email". Paste the whole file as Google Cloud downloaded it.',
    uiKeyNoPrivateKeyText: 'The key has no private key ("private_key"). Paste the whole file as Google Cloud downloaded it.',
    // args: max (the largest key, in KB)
    uiKeyTooLargeText: ({ max: e } = {}) => `This is larger than ${o(e) ? `${i(e)} KB` : "a key can be"}, so it is not a service account key file. A real key is about 2 KB.`,
    uiKeyCheckHintText: "You can still test or save: the server checks the key and has the last word.",
    uiKeyFileUnreadableText: "The file could not be read. Choose it again, or open it and paste its contents instead.",
    // The service account's address: what has to be added in Google Analytics and Search Console. Not a secret.
    uiServiceAccountLabel: "Service account",
    uiServiceAccountHelpText: "Add this address as a Viewer in Google Analytics and as a user in Search Console.",
    uiCopyAddressLabel: "Copy the address",
    uiCopiedText: "Copied",
    uiCopyFailedText: "Could not copy. Select the address and copy it yourself.",
    // Test connection: three rows, each with an icon and a word for how it went.
    uiTestConnectionLabel: "Test connection",
    uiTestHelpText: "Tests the values in the form before you save them: the key, then Google Analytics and Search Console. With the key box empty, the key in use is tested.",
    uiTestingText: "Testing the connection with Google...",
    uiTestResultTitle: "Connection test",
    uiTestDoneText: "The connection test has finished. The results are below.",
    uiTestKeyLabel: "Service account key",
    uiTestGa4Label: "Google Analytics",
    uiTestSearchConsoleLabel: "Search Console",
    uiTestPassedLabel: "Works",
    uiTestNoDataLabel: "Connected, no data yet",
    uiTestNotTestedLabel: "Not tested",
    uiTestFailedLabel: "Failed",
    uiTestNotTestedText: "Not tested, because the key could not be used.",
    // The report.
    uiReportSettingsTitle: "Report",
    uiBrandTermsLabel: "Brand terms, one per line",
    uiBrandTermsHelpText: "A search query that contains one of these counts as a brand search, for example your company or product name; the rest are non-brand. Capitals do not matter.",
    // Names the "Refresh" button of the month by its label in the dashboard: keep the two the same.
    uiBrandTermsChangeText: 'Changing brand terms applies to months fetched from then on; use "Refresh" on a month to update it.',
    // args: count (the brand terms entered), max (the most the list holds), maxLength (the longest a brand term can be, in characters)
    uiBrandTermsCounterText: ({ count: e, max: t, maxLength: a } = {}) => o(e, t, a) ? `${i(e)} of ${i(t)} brand terms, up to ${i(a)} characters each` : "Brand terms entered",
    // args: count (how many are too long), max (the longest a brand term can be, in characters)
    uiBrandTermsTooLongText: ({ count: e, max: t } = {}) => o(e, t) ? `${i(e)} brand ${l(e, "term is", "terms are")} longer than ${i(t)} characters and will be left out.` : "Brand terms that are too long will be left out.",
    uiDisplayNameLabel: "Display name (optional)",
    uiDisplayNameHelpText: "Shown under the title of the dashboard. Leave it empty for the general description.",
    uiFindingsGroupsLegendLabel: 'Who sees "To fix"',
    uiFindingsGroupsHelpText: "Only members of the chosen user groups see the To fix tab and the quick wins. With no group chosen, everyone who can open the report sees them.",
    uiGroupsLoadingText: "Reading the user groups...",
    uiGroupsEmptyText: "There are no user groups to choose from.",
    // args: alias (a saved alias that no user group has any more)
    uiGroupUnknownText: ({ alias: e } = {}) => o(e) ? `${e} (no longer a user group)` : "A user group that no longer exists",
    // Fetching.
    uiFetchingTitle: "Fetching",
    uiAutoFetchLabel: "Fetch new months automatically",
    uiAutoFetchHelpText: 'A background job fetches each month once it has ended and fills in any of the latest 13 months that are missing. Turned off, months are fetched only when someone presses Fetch now or "Refresh". Older months stay in the archive.',
    uiFetchNowHelpText: 'Fetch now fetches the months that are missing from the archive, in the background, with the saved settings. To fetch one month again, choose it in the report and use "Refresh".',
    uiFetchIdleText: "No fetch is running.",
    // Saving.
    uiSaveSettingsLabel: "Save settings",
    uiSaveHelpText: "Saving checks the connection with Google, which can take up to a minute. When it works, the missing months start to fetch in the background.",
    uiSavingText: "Saving and checking the connection with Google. This can take up to a minute.",
    uiSettingsSavedText: "Settings saved",
    uiUnsavedText: "You have unsaved changes.",
    uiDiscardLabel: "Discard changes",
    // The setup guide. The Google console steps were checked against Google's own help pages (see the Settings tab's links); Google renames
    // its menus now and then, so check them again when a step stops matching.
    uiGuideTitle: "How to connect",
    uiGuideIntroText: "The report reads Google Analytics and Search Console through a Google service account: an account for software, with its own e-mail address and a key file. You need access to a Google Cloud project, and the right to add users to the Google Analytics property and to the Search Console property.",
    uiGuideStepProjectText: "In the Google Cloud console, create a project for the report, or use one you have: open Manage resources, click Create project, enter a name and click Create.",
    uiGuideStepApisText: "In that project, open APIs & services, then Library. Search for Google Analytics Data API and click Enable, then do the same for Google Search Console API.",
    uiGuideStepAccountText: "Create the service account: on the Service accounts page, click Create service account, enter a name and click Done (it needs no roles in Google Cloud). Then click its e-mail address, open the Keys tab, click Add key, then Create new key, choose JSON and click Create. The key file downloads; keep it safe. If Google Cloud says that an organization policy does not allow keys, ask your Google Cloud administrator.",
    // args: email (the service account's address, when the tab knows it)
    uiGuideStepAccessText: ({ email: e } = {}) => `Give the service account read access with its e-mail address, ${o(e) ? e : "which the key file holds as client_email and this tab shows once the key is saved"}. In Google Analytics, open Admin and, under Property, Access management; click +, then Add users, enter the address, choose the Viewer role and click Add. In Search Console, open the property, then Settings, then Users and permissions; click Add user, enter the address and choose Restricted permission. Only an owner of the Search Console property can add users.`,
    uiGuideStepFormText: "On this tab, paste the key or choose the file, enter the GA4 property ID (in Google Analytics under Admin, then Property details) and choose the Search Console property exactly as Search Console lists it.",
    uiGuideStepTestText: "Click Test connection. When the key, Google Analytics and Search Console all work, click Save settings: the report starts fetching the last 13 months in the background, newest first.",
    uiGuideLinkProjectLabel: "Create a Google Cloud project",
    uiGuideLinkApisLabel: "Enable an API",
    uiGuideLinkAnalyticsApiLabel: "Google Analytics Data API in the API Library",
    uiGuideLinkSearchConsoleApiLabel: "Google Search Console API in the API Library",
    uiGuideLinkServiceAccountLabel: "Create a service account",
    uiGuideLinkKeyLabel: "Create a service account key",
    uiGuideLinkAnalyticsUsersLabel: "Add users in Google Analytics",
    uiGuideLinkSearchConsoleUsersLabel: "Add users in Search Console",
    uiGuideLinkPropertyIdLabel: "Find the GA4 property ID",
    uiGuideLinkPropertyTypesLabel: "Domain and URL-prefix properties",
    uiNewTabText: "(opens in a new tab)"
  }
};
export {
  B as default
};
//# sourceMappingURL=en.js.map
