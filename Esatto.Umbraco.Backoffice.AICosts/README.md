# Esatto.Umbraco.Backoffice.AICosts

Estimated AI costs for **Umbraco.AI** in the Umbraco 18 backoffice.

Umbraco.AI records how many tokens every AI request uses, but not what it costs. This package adds a **Costs** tab next to Umbraco.AI's own **Usage** tab (AI section → Analytics) that turns that usage into money: tokens × each model's list price, in your currency.

![The Costs tab: totals, this month chart with budget, usage calendar, daily cost, tokens by type, reliability and cost by model](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.AICosts/docs/costs.png)

## What you see

- **Period and currency:** 30, 90 or 180 days, or 12 months; any currency the European Central Bank quotes. Your choices are remembered per browser.
- **This month:** cost so far, the projected cost by month end, and — with a monthly budget configured — a bar showing how much of it is used. It turns amber at 80 % or when the projection goes over the budget, and red once the budget is spent.
- **Totals:** estimated cost, tokens, requests and cost per request, each with the change against the previous period of the same length (e.g. "+12 % vs previous 30 days").
- **This month chart:** the cost so far as a line, a dashed projection to month end, and your budget as a reference line.
- **Usage calendar:** one square per day, for cost, tokens or requests.
- **Daily cost** (weekly for longer periods), in total or **stacked by model**, as a chart or a table.
- **Tokens by type:** input, cached input and output per day, and how much prompt caching saved.
- **Reliability:** succeeded and failed requests per day, the failure rate and the average response time.
- **Cost by model, profile, user or provider**, with each one's share of the total. Usage without a backoffice user (front end, API, scheduled agents) is shown as Umbraco.AI's "Anonymous" row.
- **Download CSV** of the daily figures and of the cost breakdown. Files open directly in Excel, also in locales with a decimal comma (Swedish, German…).

## Why "estimated"

Cost is tokens × the list prices **you** configure. It doesn't include discounts, batch pricing, taxes or provider surcharges. Cached input tokens are billed at the cache-read price when you set one, otherwise at the normal input price, so the estimate never comes out too low.

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.AICosts
```

Add your model prices (below), restart the site, and open **AI → Analytics → Costs**.

## Prices

The package ships **no prices**: they change often, and a stale built-in price would quietly show wrong costs. Add the models you use to `appsettings.json`, in US dollars per **1 million tokens**, keyed by the model ID exactly as the tab shows it:

```json
"Esatto": {
  "AICosts": {
    "PricesSource": "Anthropic API list prices checked 2026-10-06",
    "PricesPerMillionTokens": {
      "claude-opus-5-5": { "Input": 4.00, "Output": 20.00, "CachedInput": 0.20 },
      "claude-sonnet-5-5": { "Input": 2.00, "Output": 10.00, "CachedInput": 0.20 },
      "claude-haiku-5-5": { "Input": 0.10, "Output": 0.50 }
    }
  }
}
```

These values are an **example** — check them against your provider's current price list before relying on them. `PricesSource` is shown under the tab so everyone can see where the prices came from and how old they are. Models without a price still show their tokens, marked "No price".

## Settings

All optional, under `Esatto:AICosts`:

| Setting | Default | Meaning |
|---------|---------|---------|
| `PricesPerMillionTokens` | none | Per model ID: `Input`, `Output` and optional `CachedInput` (the cache-read price). |
| `BaseCurrency` | `USD` | The currency the prices are in. |
| `Currencies` | `SEK, EUR, USD, NOK, DKK, GBP` | The currency switcher. |
| `DefaultCurrency` | `SEK` | The currency the tab opens in. |
| `PricesSource` | none | Where the prices came from and when, shown in the footnote. |
| `MonthlyBudget` | none | A budget per calendar month, for the "This month" tile. |
| `BudgetCurrency` | `DefaultCurrency` | The currency of `MonthlyBudget`. |

## Good to know

- **Who sees it:** users with access to the AI section, the same as Umbraco.AI's own analytics.
- **Exchange rates** come from the European Central Bank's daily reference rates (no key needed), refreshed every 6 hours. If they can't be fetched, amounts are shown in the base currency and the tab says so.
- **Times are UTC.** Today and yesterday come from Umbraco.AI's hourly statistics, older days from its daily ones.
- **The comparison** is only shown while Umbraco.AI still keeps the whole previous period (daily statistics are kept 365 days by default, `Umbraco:AI:Analytics:UsageDailyRetentionDays`). With the default, 12 months has no comparison.
- **Cost by feature** isn't offered: Umbraco.AI can filter usage by feature but can't list the features used, so a breakdown would miss spend.

## Requirements

- Umbraco 18
- Umbraco.AI 18.3.0 or later (the first version that records cached input tokens)

## Compatibility

| Umbraco | Umbraco.AI | Status |
|---------|------------|--------|
| 18.x | 18.3+ | Supported |
| 18.2.1 | 18.5.2 | Verified |

## License

MIT. Source: [github.com/carl-schele-esatto/Esatto.Packages](https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.AICosts)
