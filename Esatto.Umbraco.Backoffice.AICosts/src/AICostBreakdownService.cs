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
        foreach (var item in await _queries.GetBreakdownAsync(window, dimension, ct))
        {
            if (Filter(dimension, item.Dimension, modelId: null) is null) continue;

            var priced = await _queries.PriceByModelAsync(window, modelIds, modelId => Filter(dimension, item.Dimension, modelId)!, prices, ct);
            var t = priced.Totals;
            rows.Add(new AICostBreakdownRow(
                item.Dimension,
                string.IsNullOrWhiteSpace(item.DimensionName) ? item.Dimension : item.DimensionName,
                t.Requests, t.InputTokens, t.CachedInputTokens, t.OutputTokens, t.TotalTokens,
                priced.PricedTokens == 0 && priced.UnpricedTokens > 0 ? null : priced.Cost,
                priced.UnpricedTokens));
        }

        return new AICostBreakdown(
            dimension.ToString().ToLowerInvariant(),
            rows.OrderByDescending(r => r.Cost ?? -1).ThenByDescending(r => r.TotalTokens).ToList());
    }

    // A profile row's ID is the profile's GUID; one that isn't (a deleted or legacy row) can't be
    // filtered on, so it's left out rather than failing the whole card.
    private static AIUsageFilter? Filter(AICostDimension dimension, string id, string? modelId) => dimension switch
    {
        AICostDimension.Profile => Guid.TryParse(id, out var profileId) ? new AIUsageFilter { ProfileId = profileId, ModelId = modelId } : null,
        AICostDimension.User => new AIUsageFilter { UserId = id, ModelId = modelId },
        AICostDimension.Provider => new AIUsageFilter { ProviderId = id, ModelId = modelId },
        _ => null,
    };
}
