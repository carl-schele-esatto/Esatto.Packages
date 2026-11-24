using Esatto.Umbraco.Backoffice.SeoReport.Google;
using NSubstitute;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// What the daily service answers in the controller's tests: the real curve arithmetic (<see cref="Ga4DailyAgent.WindowFor"/> and
/// <see cref="Ga4DailyAgent.Split"/>) over made-up sessions, read through the rig's clock, so the window, the totals and the peak are
/// the ones the real service would give for the same days.
/// </summary>
internal static class DailyCurves
{
    /// <summary>A made-up number of sessions for a day, with a spike on the ninth of every month.</summary>
    public static long Sessions(DateOnly day) => 100 + (day.Day * 3) + (day.Day == 9 ? 400 : 0);

    public static readonly IReadOnlyList<Ga4DailySource> Sources =
        [new Ga4DailySource("Organic Search", "google", 120), new Ga4DailySource("Email", "newsletter", 40)];

    public static DateOnly TodayOf(ApiRig rig) => DateOnly.FromDateTime(rig.Harness.Time.GetUtcNow().UtcDateTime);

    /// <summary>The curve for a window (null or empty month: the rolling one), with <paramref name="sessions"/> per day (<see cref="Sessions"/> when null).</summary>
    public static (Ga4DailySeries Series, Ga4DailyWindow Window) Curve(DateOnly today, string? month, Func<DateOnly, long>? sessions = null)
    {
        var window = Ga4DailyAgent.WindowFor(today, month);
        var points = new List<Ga4DailyPoint>();
        for (var day = window.PreviousStart; day <= window.End; day = day.AddDays(1)) points.Add(new Ga4DailyPoint(day, (sessions ?? Sessions)(day)));

        return (Ga4DailyAgent.Split(points, window), window);
    }

    /// <summary>Makes the rig's daily service answer with the curve for whatever month it is asked for, on the rig's clock, and with <see cref="Sources"/>.</summary>
    public static void Serve(ApiRig rig, Func<DateOnly, long>? sessions = null)
    {
        rig.Daily.GetSeriesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Curve(TodayOf(rig), call.Arg<string?>() is { Length: > 0 } month ? month : null, sessions)));
        rig.Daily.GetSourcesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Sources));
    }
}
