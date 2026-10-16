using Microsoft.Extensions.Options;
using Umbraco.AI.Core.Analytics.Usage;

namespace Esatto.Umbraco.Backoffice.AICosts;

/// <summary>
/// Cost by profile, user or provider. Cost depends on the model, so each row is priced from its usage
/// per model used in the period (rows × models summary queries; it only runs when an editor opens it).
/// </summary>
public sealed class AICostBreakdownService
{
    private readonly AICostQueries _queries;
    private readonly IOptionsMonitor<AICostOptions> _options;
    private readonly TimeProvider _timeProvider;

    public AICostBreakdownService(AICostQueries queries, IOptionsMonitor<AICostOptions> options, TimeProvider timeProvider)
    {
        _queries = queries;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<AICostBreakdown> GetBreakdownAsync(AICostDimension dimension, int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, AICostReportService.MaxDays);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var window = UsageWindow.Create(
            today.AddDays(-(days - 1)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), now);
        var prices = _options.CurrentValue.PricesPerMillionTokens;
        var modelIds = await _queries.GetModelIdsAsync(window, ct);

        var rows = new List<AICostBreakdownRow>();
        var identified = PricedUsage.Empty;
        AIUsageBreakdownItem? anonymous = null;
        foreach (var item in await _queries.GetBreakdownAsync(window, dimension, ct))
        {
            // Umbraco.AI reports usage without a backoffice user (frontend, API, scheduled agents, or all
            // of it when the user dimension is turned off) as one row, "Anonymous", which can't be
            // filtered on: it's priced below as the usage no user row claims.
            if (dimension == AICostDimension.User && !Guid.TryParse(item.Dimension, out _))
            {
                anonymous = item;
                continue;
            }

            if (Filter(dimension, item.Dimension, modelId: null) is null) continue;

            var priced = await _queries.PriceByModelAsync(window, modelIds, modelId => Filter(dimension, item.Dimension, modelId)!, prices, ct);
            identified = identified.Plus(priced);
            rows.Add(Row(item, priced));
        }

        if (anonymous is not null)
        {
            // Pricing is linear in tokens, so all usage minus the identified users' is exactly the rest.
            var all = await _queries.PriceByModelAsync(window, modelIds, modelId => new AIUsageFilter { ModelId = modelId }, prices, ct);
            rows.Add(Row(anonymous, all.Minus(identified)));
        }

        return new AICostBreakdown(
            dimension.ToString().ToLowerInvariant(),
            rows.OrderByDescending(r => r.Cost ?? -1).ThenByDescending(r => r.TotalTokens).ToList());
    }

    private static AICostBreakdownRow Row(AIUsageBreakdownItem item, PricedUsage priced)
    {
        var t = priced.Totals;
        return new AICostBreakdownRow(
            item.Dimension,
            string.IsNullOrWhiteSpace(item.DimensionName) ? item.Dimension : item.DimensionName,
            t.Requests, t.InputTokens, t.CachedInputTokens, t.OutputTokens, t.TotalTokens,
            priced.PricedTokens == 0 && priced.UnpricedTokens > 0 ? null : priced.Cost,
            priced.UnpricedTokens);
    }

    // A profile row's ID is the profile's GUID; one that isn't can't be filtered on, so it's left out
    // rather than failing the whole card (ProfileId is a required Guid in Umbraco.AI, so this is a guard).
    private static AIUsageFilter? Filter(AICostDimension dimension, string id, string? modelId) => dimension switch
    {
        AICostDimension.Profile => Guid.TryParse(id, out var profileId) ? new AIUsageFilter { ProfileId = profileId, ModelId = modelId } : null,
        AICostDimension.User => new AIUsageFilter { UserId = id, ModelId = modelId },
        AICostDimension.Provider => new AIUsageFilter { ProviderId = id, ModelId = modelId },
        _ => null,
    };
}
