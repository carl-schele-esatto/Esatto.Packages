using NSubstitute;
using Umbraco.AI.Core.Analytics.Usage;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

/// <summary>A settable clock for services that take a <see cref="TimeProvider"/>.</summary>
public sealed class TestClock(DateTime utcNow) : TimeProvider
{
    private DateTimeOffset _now = new(utcNow);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Builders for Umbraco.AI usage statistics.</summary>
public static class Usage
{
    public static AIUsageTimeSeriesPoint Point(DateTime at, int requests, long input, long output, long cached = 0) => new()
    {
        Timestamp = at, RequestCount = requests, InputTokens = input, OutputTokens = output,
        CachedInputTokens = cached, TotalTokens = input + output, SuccessCount = requests, FailureCount = 0,
    };

    public static AIUsageSummary Summary(int requests, long input, long output, long cached = 0) => new()
    {
        TotalRequests = requests, InputTokens = input, OutputTokens = output, CachedInputTokens = cached,
        TotalTokens = input + output, SuccessCount = requests, FailureCount = 0, SuccessRate = 1, AverageDurationMs = 0,
    };

    public static AIUsageBreakdownItem Item(string dimension, string? name = null) =>
        new() { Dimension = dimension, DimensionName = name, RequestCount = 0, TotalTokens = 0, Percentage = 0 };

    /// <summary>An analytics substitute that answers empty for everything until set up otherwise.</summary>
    public static IAIUsageAnalyticsService EmptyAnalytics()
    {
        var a = Substitute.For<IAIUsageAnalyticsService>();
        a.GetTimeSeriesAsync(default, default, default, default, default).ReturnsForAnyArgs(Array.Empty<AIUsageTimeSeriesPoint>());
        a.GetSummaryAsync(default, default, default, default, default).ReturnsForAnyArgs(Summary(0, 0, 0));
        a.GetBreakdownByModelAsync(default, default, default, default).ReturnsForAnyArgs(Array.Empty<AIUsageBreakdownItem>());
        a.GetBreakdownByProfileAsync(default, default, default, default).ReturnsForAnyArgs(Array.Empty<AIUsageBreakdownItem>());
        a.GetBreakdownByUserAsync(default, default, default, default).ReturnsForAnyArgs(Array.Empty<AIUsageBreakdownItem>());
        a.GetBreakdownByProviderAsync(default, default, default, default).ReturnsForAnyArgs(Array.Empty<AIUsageBreakdownItem>());
        return a;
    }
}
