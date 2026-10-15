using Esatto.Umbraco.Backoffice.AICosts;
using Xunit;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

public class UsageWindowTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 15, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Yesterday = Today.AddDays(-1);

    [Fact]
    public void Current_period_reads_daily_until_yesterday_and_hourly_after()
    {
        var w = UsageWindow.Create(Today.AddDays(-29), Today.AddDays(1), Now);
        Assert.Equal(Yesterday, w.HourlyFrom);
        Assert.True(w.HasDaily);
        Assert.True(w.HasHourly);
    }

    [Fact]
    public void A_range_starting_today_is_hourly_only()
    {
        var w = UsageWindow.Create(Today, Today.AddDays(1), Now);
        Assert.Equal(Today, w.HourlyFrom);
        Assert.False(w.HasDaily);
    }

    // Review Focus 1: anchored to now, not to the range end.
    [Fact]
    public void Previous_period_older_than_yesterday_is_read_from_daily()
    {
        var w = UsageWindow.Create(Today.AddDays(-59), Today.AddDays(-29), Now);
        Assert.True(w.HasDaily);
        Assert.False(w.HasHourly);
    }
}
