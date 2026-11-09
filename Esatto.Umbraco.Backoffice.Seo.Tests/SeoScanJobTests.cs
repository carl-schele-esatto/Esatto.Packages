using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Settings;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class SeoScanJobTests
{
    private static readonly DateTimeOffset ThreeAm = new(2026, 11, 9, 3, 10, 0, TimeSpan.Zero);
    private static SeoSettings On(int hour = 3) => new() { Schedule = new ScheduleSettings { Enabled = true, Hour = hour } };

    [Fact]
    public void Due_at_the_hour() => Assert.True(SeoScanJob.ScheduleDue(On(), ThreeAm, null));

    [Fact]
    public void Not_due_at_another_hour() => Assert.False(SeoScanJob.ScheduleDue(On(4), ThreeAm, null));

    [Fact]
    public void Not_due_twice_the_same_night()
        => Assert.False(SeoScanJob.ScheduleDue(On(), ThreeAm, ThreeAm.UtcDateTime.AddMinutes(-9)));

    [Fact]
    public void Due_again_the_next_night()
        => Assert.True(SeoScanJob.ScheduleDue(On(), ThreeAm, ThreeAm.UtcDateTime.AddDays(-1)));

    [Fact]
    public void Not_due_when_disabled()
        => Assert.False(SeoScanJob.ScheduleDue(new SeoSettings { Schedule = new ScheduleSettings { Enabled = false, Hour = 3 } }, ThreeAm, null));
}
