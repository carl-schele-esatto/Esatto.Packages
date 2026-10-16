# AICosts package — Design

**Date:** 2026-10-15
**Package:** **`Esatto.Umbraco.Backoffice.AICosts` 1.0.0** (new, public, nuget.org, `umbraco-marketplace` tag)
**Repo dir:** `Esatto.Umbraco.Backoffice.AICosts/` + `Esatto.Umbraco.Backoffice.AICosts.Tests/`
**Source:** `c:\src\Esatto.Web` (the AI section's Analytics → Costs tab, commit e17f5aa)
**Test site:** `c:\src\umbracokalaset26` (Umbraco 18.2.1, Umbraco.AI 18.5.2 + Anthropic)

## Summary

Umbraco.AI records how many tokens every AI request uses, but not what it costs. Esatto.Web has a
**Costs** tab next to Umbraco.AI's own **Usage** tab (AI section → Analytics) that turns that usage into
an estimated cost: tokens × each model's list price, converted to the editor's currency. This package
moves that feature out of Esatto.Web so any Umbraco 18 site with Umbraco.AI can install it.

Behaviour stays as it is in Esatto.Web. The server code moves with a light, behaviour-preserving tidy;
the backoffice view is rewritten from plain JavaScript into typed, tested TypeScript with the same look.

### Decisions (with Carl, 2026-10-15)

| Question | Decision |
|---|---|
| Name | `Esatto.Umbraco.Backoffice.AICosts` |
| Default prices | **None built in.** Prices come from config only; the README has a ready-to-copy block with its source and date. Prices change often, and a stale built-in price would quietly show wrong costs. |
| Frontend | Rewrite in TypeScript + Vite (like TreeDragAndDrop and BlockPreview), pure logic covered by vitest. |
| Versions | **Umbraco 18 only.** Umbraco.AI ≥ **18.3.0** (see below). |
| Config section | `Esatto:AICosts` (Esatto.Web's `AiCosts` moves there when it switches). |

### Why Umbraco.AI 18.3.0

Checked against the published assemblies:

- `IAIUsageAnalyticsService` (time series + breakdown by model, `AIUsagePeriod.Hourly/Daily`) and the
  `SectionAccessAI` authorization policy are identical in 18.0.0 and 18.3.0.
- `AIUsageTimeSeriesPoint.CachedInputTokens` — needed to price cached input at the cache-read price —
  first appears in **18.3.0** (absent in 18.0.0, 18.1.0 and 18.2.0).
- The `UmbracoAI.Workspace.AnalyticsRoot` workspace the tab attaches to exists in 18.3.0's static assets.

### New in the package (beyond Esatto.Web's tab)

Chosen by Carl when asked what to add (details under **Additions** below):

1. **Cost by profile / user / provider** next to cost by model.
2. **This month: budget + forecast** — month-to-date cost, projected month-end cost, optional budget.
3. **Export to CSV** — the period's daily figures and the current cost breakdown.
4. **Compare with the previous period** — change in the tiles vs the previous period of the same length.

**Not by feature:** Umbraco.AI tags requests with a feature type, but its analytics API can only *filter*
by one, not list them (no `GetBreakdownByFeatureAsync`), and only five inline feature types are known
constants — add-ons such as Prompt and Agent use their own. A feature breakdown would silently miss
spend, so it's left out.

### Out of scope (1.0)

- Umbraco 17 (Umbraco.AI 1.x / 17.x).
- Built-in or downloaded model prices.
- Budget alerts by email or notifications (the budget is shown in the tab only).
- Cost by feature (see above).
- Moving Esatto.Web onto the package (a separate step in that repo, after 1.0.0 is published).

## What the editor sees

AI section → Analytics → **Costs** (a workspace view next to Umbraco.AI's Usage, `icon-coins`),
visible to users with access to the AI section. Unchanged from Esatto.Web:

- **Filters:** period 30 / 90 / 180 days / 12 months; currency dropdown (disabled when exchange rates
  are unavailable). Period, currency and the heatmap metric are remembered per browser.
- **Tiles:** estimated cost (with a warning when some tokens come from models without a price, otherwise
  the date range), tokens (in / out), requests (per day on average), cost per request (priced models).
- **Usage calendar:** a heatmap, one cell per day, weeks as columns starting Monday, four intensity
  levels by quartile of the non-zero days plus "no usage"; metric switch Cost / Tokens / Requests;
  tooltips.
- **Cost chart:** columns per day (≤ 92 days) or per week (longer periods), rounded tops, round y-axis
  ticks, tooltips; "Show as table" switches to a table of the non-empty days/weeks.
- **Cost by model:** requests, input, cached input, output, cost (or a "No price" tag) and share of the
  total cost.
- **Footnote:** how the estimate is made, the configured prices source, the exchange rates' source and
  date (or a warning that amounts are in the base currency), "Times are UTC".
- Light and dark backoffice themes (colours validated for both).

## Additions

### 1. Cost by profile / user / provider

- The "Cost by model" card becomes **"Cost by"** with a switch: **Model** · **Profile** · **User** ·
  **Provider**. Same columns (requests, input, cached input, output, cost or "No price", share); rows
  show Umbraco.AI's display name (`DimensionName`, e.g. the profile or user name), falling back to the
  ID.
- Model stays in the main report. The other three load **on demand** from
  `GET …/esatto/ai-costs/breakdown?dimension=profile|user|provider&days=N` the first time they're picked
  (with a loader), and are cached per period in the tab.
- **Costing a row:** cost depends on the model, so for each row the service asks Umbraco.AI for a
  **summary per model** used in the period (`GetSummaryAsync` with the row's filter + `ModelId`; Daily up
  to yesterday + Hourly for yesterday and today, as for the report) and prices each with
  `AICostCalculator`. Pricing is linear in tokens, so pricing the per-model totals equals pricing every
  data point. A row's tokens from models without a price count as unpriced; its cost is `null` only
  when *none* of its tokens are priced.
- Cost: rows × models × 2 summary queries. Fine for typical sites (tens of users, a handful of models);
  it only runs when an editor opens that view.

### 2. This month: budget + forecast

- A **"This month"** tile, independent of the selected period (calendar month, UTC): cost so far,
  **projected month-end cost** (cost so far ÷ days elapsed including today × days in the month), and —
  when `MonthlyBudget` is set — a progress bar of cost so far against the budget plus the projection.
  The bar is neutral below 80 % of the budget, amber from 80 % or when the projection exceeds the budget,
  red once the cost so far exceeds it.
- The report gains `month`: `from`, `to`, `cost`, `projectedCost`, `unpricedTokens` (base currency), and
  `budget`: `amount` + `currency` (or `null`). The client converts the budget to the display currency
  with the report's rates; if that isn't possible (no rates and the budget isn't in the base currency),
  the tile shows the cost and projection without the budget, and says why.

### 3. Export to CSV

- **"Download CSV"** in the cost chart card (one row per day — or per week for long periods — as in the
  table: date, requests, input, cached input, output, total tokens, cost, unpriced tokens, currency) and
  in the "Cost by" card (the current breakdown).
- Generated in the browser from the data already loaded; amounts in the **displayed currency**.
- **Excel-friendly per locale:** where the browser's locale uses a decimal comma (sv-SE, de-DE…), the
  file uses `;` between fields and `,` decimals — what Excel expects there; otherwise `,` and `.`. UTF-8
  with BOM, RFC 4180 quoting. File name `ai-costs-<from>-<to>[-<dimension>].csv`.

### 4. Compare with the previous period

- The report gains `previous`: totals (requests, tokens, cost, unpriced tokens) for the period of the same
  length just before the selected one, priced the same way (per model).
- Tiles show the change, e.g. **"+12 % vs previous 30 days"** (cost, tokens, requests, cost per request);
  up isn't coloured as good or bad, just shown. Shown as "new" when the previous value is 0.
- **Only when the previous period is fully within Umbraco.AI's daily retention**
  (`AIAnalyticsOptions.UsageDailyRetentionDays`, read through `IOptions`, default 365 — present in
  18.3.0): older statistics may be partly deleted,
  and a comparison against half-deleted data would mislead. With the defaults, 12 months has no
  comparison; 30 / 90 / 180 days do. `previous` is `null` otherwise.

## Configuration

`appsettings.json`, section `Esatto:AICosts` — every setting optional:

| Setting | Default | Meaning |
|---|---|---|
| `PricesPerMillionTokens` | `{}` | Per model ID exactly as Umbraco.AI reports it: `Input`, `Output`, optional `CachedInput` (cache-read price; falls back to `Input`). Models without an entry show tokens but no cost. |
| `BaseCurrency` | `USD` | Currency of the prices. |
| `Currencies` | `SEK, EUR, USD, NOK, DKK, GBP` | Currency switcher (ISO 4217, quoted by the ECB). |
| `DefaultCurrency` | `SEK` | Currency the tab opens in (falls back to the first in `Currencies`). |
| `PricesSource` | – | Where the prices came from and when, shown in the footnote. |
| `MonthlyBudget` | – | Optional budget per calendar month, for the "This month" tile. |
| `BudgetCurrency` | `DefaultCurrency` | Currency of `MonthlyBudget`. |

`Currencies` keeps Esatto.Web's fix: the list isn't pre-filled on the options class (the configuration
binder would append to it), the default is applied when the bound list is empty.

## Architecture

### Server (`src/`)

| Type | Role |
|---|---|
| `AICostOptions`, `AIModelPrice` | Options bound from `Esatto:AICosts`. |
| `AICostCalculator` | `Cost(price, input, output, cachedInput)`: cached tokens are a subset of input (Microsoft.Extensions.AI convention), billed at `CachedInput ?? Input`; negative counts clamp to 0. |
| `AICostReportService` | Builds the report for N days (clamped 1–365), whole UTC days including today. Reads **Daily** statistics up to yesterday and **Hourly** for yesterday and today (Umbraco.AI only rolls completed days into daily statistics, up to an hour after midnight). Prices **per model per data point**, sums per day and per model; unpriced tokens are counted separately. Models sorted by cost, then tokens. |
| `IExchangeRateProvider`, `EcbExchangeRateProvider` | ECB daily reference rates (no key), rebased to `BaseCurrency`. Singleton, in-process cache: 6 h after a success, 15 min after a failure; keeps serving the last good rates; `null` when none. Named `HttpClient`, 10 s timeout. |
| `AICostBreakdownService` | Cost by profile / user / provider for N days: rows from Umbraco.AI's breakdown, each priced from per-model summaries (Additions §1). |
| `UsageWindow` (internal helper) | The shared "Daily up to yesterday + Hourly for yesterday and today" split, used by the report, the month, the previous period and the breakdown — one place for that rule. |
| `AICostsController` | `GET /umbraco/management/api/v1/esatto/ai-costs/report?days=N` (default 90) and `GET …/breakdown?dimension=…&days=N` (400 for an unknown dimension). Management API, bearer token, `[Authorize(Policy = AIAuthorizationPolicies.SectionAccessAI)]` — the same gate as Umbraco.AI's own analytics. |
| `AICostsComposer` + `AddAICosts()` | Registers options, `TimeProvider.System` (TryAdd), the HttpClient, the exchange-rate provider and the report service. Idempotent. |

Report shape (JSON, camelCase) — Esatto.Web's fields unchanged: `from`, `to`, `baseCurrency`,
`defaultCurrency`, `currencies`, `rates` (`date`, `source`, `perBaseUnit`), `pricesSource`, `days[]`,
`models[]`, `totals`; plus `month` (with `budget`) and `previous` (Additions §2 and §4). Breakdown shape:
`dimension`, `rows[]` (`id`, `name`, `requests`, `inputTokens`, `cachedInputTokens`, `outputTokens`,
`totalTokens`, `cost` or `null`, `unpricedTokens`).

**Dependencies:** `Umbraco.AI.Core` 18.3.0, `Umbraco.AI.Web` 18.3.0, `Umbraco.Cms.Api.Management`
18.0.0 (also the detectable Umbraco dependency the Marketplace needs), plus the repo's usual security
pins.

### Client (`Client/src`, built by Vite to `wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.AICosts/`)

- `ai-costs-view.element.ts` — the Lit element `esatto-ai-costs-view` (UmbElementMixin): loading,
  filters, tiles, heatmap, column chart/table, model table, footnote, tooltips, theme detection
  (luminance of `--uui-color-surface`), chart width via ResizeObserver.
- Pure modules, each with vitest tests:
  - `api.ts` — the report request (`umbHttpClient`, bearer security) and the response type; upper-cases
    rate keys (the serializer may camel-case dictionary keys).
  - `money.ts` — display currency (falls back to the base currency without rates), conversion,
    money/number formatting (small amounts get 4 decimals).
  - `dates.ts` — `parseDate` (DateOnly → UTC midnight).
  - `buckets.ts` — Monday-start weeks for long periods.
  - `scales.ts` — quartile thresholds, heatmap level, nice axis ticks, rounded column path.
  - `prefs.ts` — read/write the remembered period, currency and metric (storage errors ignored).
  - `budget.ts` — budget in the display currency (or why not), progress level (neutral/amber/red).
  - `compare.ts` — change vs the previous period (percentage, "new", none).
  - `csv.ts` — rows → CSV text: locale-dependent separator and decimals, RFC 4180 quoting, BOM.
- `umbraco-package.json` (static, in `Client/public`) — the `workspaceView` extension:
  alias `Esatto.Umbraco.Backoffice.AICosts.CostsView`, label "Costs", pathname `costs`, weight 900,
  condition `Umb.Condition.WorkspaceAlias` = `UmbracoAI.Workspace.AnalyticsRoot`.

## Error handling

- **Report request fails:** "Could not load AI cost data." in the tab; filters stay usable.
- **ECB unreachable:** amounts in the base currency, currency switcher disabled, footnote says so; the
  next attempt waits 15 minutes.
- **No usage / no prices:** each card shows a specific empty message ("No AI usage in this period." /
  "…the models used in this period have no configured price."); unpriced models get a "No price" tag
  whose tooltip names `Esatto:AICosts:PricesPerMillionTokens`.
- **Invalid `days`:** clamped to 1–365.

## Testing

- **xUnit** (`Esatto.Umbraco.Backoffice.AICosts.Tests`): Esatto.Web's 13 tests move over, renamed —
  calculator (input/output, cached subset, missing cache price, clamping), ECB parsing and rebasing,
  report (daily + hourly split, per-model pricing, yesterday/today from hourly, currencies binding
  without duplicates, defaults, days clamping). New: options defaults, `AddAICosts` registrations and
  idempotency, controller policy attribute and routes; breakdown (a row priced across two models, a row
  partly unpriced, a row fully unpriced → `null`, display name fallback, unknown dimension → 400);
  month (projection on the 1st and the last day, budget currency passed through); previous period
  (same length right before, `null` when beyond retention).
- **vitest:** the pure client modules above (conversion and fallback, formatting, weeks across month
  ends, quartiles and levels, ticks, prefs with broken storage, budget levels and conversion failure,
  comparison incl. zero → "new", CSV quoting/separators for sv-SE and en-US).
- **Live** on umbracokalaset26 (temporary ProjectReference): the tab appears under AI → Analytics, real
  Anthropic usage is priced, currency/period/metric switches, table toggle, dark theme, a user without
  AI section access gets 403.

## Docs

README: what it shows and why it's an estimate; install; a ready-to-copy price block with source and
date; settings table; requirements (Umbraco 18, Umbraco.AI ≥ 18.3.0, AI section access); a screenshot
generated with dummy data; source-code link. `icon.png` like the other packages.

## Risks

- **Umbraco.AI changes its analytics API or the workspace alias** in a later 18.x. Mitigation: the
  dependency floor is tested; the README states the verified versions.
- **The ECB feed changes or disappears.** Mitigation: parsing is tested on the real format; failures
  fall back to the base currency.
- **Wrong prices in a site's config** show wrong costs. Mitigation: the footnote shows `PricesSource`
  and the README stresses that prices are the site's responsibility.

## Charts (added 2026-10-15, approved by Carl)

Four charts, following the dataviz method (one y-axis per chart, fixed-order categorical colours from
the reference palette, legend + direct labels for ≥ 2 series, hover tooltips, dark mode stepped
separately, palette validated with `validate_palette.js`, a table view per chart).

1. **This month** (card under the tiles): solid cumulative-cost line 1st → today, dashed projection
   (same hue) to the projected month-end cost, the budget as a thin labelled horizontal line when set.
   Crosshair + tooltip per day. Server: `month.days[]` (`date`, `cost`), each day priced per model per
   data point like the report; the month's cost is the sum of those days.
2. **Daily cost by model** (existing cost card): a **Total · By model** switch; "By model" stacks each
   day/week by model, most expensive first, fixed categorical order; beyond 8 models the rest fold into
   "Other". Unpriced models aren't in the stack (noted under the chart). Table view and CSV get a
   column per model. Server: `modelDays[]` (`modelId`, `costs[]` aligned with `days`, priced models only).
   Stacking by profile/user is out of scope (per-row time series per model is too expensive).
3. **Tokens by type** (new card): stacked columns of uncached input, cached input and output tokens
   per day/week; "Caching saved X" = Σ cached tokens × (input price − cache price) for priced models.
   Server: `cachingSavings` (base currency).
4. **Reliability** (new card): requests per day stacked as succeeded + **failed** (status "critical"
   red with icon + label in the legend), above it the failure rate and the period's **average response
   time** (Umbraco.AI only has an average per summary, so it's a figure, not a series). Server:
   `days[].succeededRequests`, `days[].failedRequests`, `averageDurationMs` (request-weighted over the
   Daily + Hourly summaries).

Card order: tiles → This month → usage calendar → daily cost → tokens by type → reliability → cost by →
footnote. Each chart's geometry and data shaping live in a small pure module with vitest tests.
