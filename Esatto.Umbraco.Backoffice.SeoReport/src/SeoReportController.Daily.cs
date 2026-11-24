using System.Globalization;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Esatto.Umbraco.Backoffice.SeoReport;

// The daily curve: the one part of the analysis that is live, asking GA4 through a service that keeps the answer for half an hour.
public sealed partial class SeoReportController
{
    // The name of IDailyTrafficService.GetSeriesAsync's parameter, which its check puts on the exception it throws for a bad month.
    private const string MonthParameter = "month";

    /// <summary>
    /// The visits per day for the rolling last thirty days or a calendar month, against the period before it: the curve as a chart
    /// block, the arguments its texts are written with, what the picker offers and what the summary line and the day picker need.
    ///
    /// LIVE, UNLIKE THE ARCHIVE. This asks GA4 (one call, kept by the service for thirty minutes), which is why it is an endpoint of its
    /// own: a report is a finished month that never changes, this is rolling data that does. It never starts the backfill and never
    /// reads the archive.
    ///
    /// ONLY WHAT THE PICKER OFFERS. A month must be <c>YYYY-MM</c> and one of the months <see cref="DailyChart.MonthOptions"/> lists (the
    /// running one and the eleven before it): the service would accept any month, and each distinct one is a Google call and a cache
    /// entry. None, and the empty string, are the rolling window.
    /// </summary>
    /// <param name="month"><c>YYYY-MM</c>, exactly; none or empty for the rolling window.</param>
    [HttpGet("daily")]
    [ProducesResponseType(typeof(DailyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetDaily([FromQuery] string? month, CancellationToken ct)
    {
        var today = Today();

        // The empty string and none are the same question, asked the same way all the way down (and so one entry in the service's cache).
        var asked = string.IsNullOrEmpty(month) ? null : month;

        if (asked is not null)
        {
            if (!Period.TryParse(asked, out _)) return PeriodInvalid();
            if (!DailyChart.MonthOptions(today).Contains(asked)) return ProblemResult(StatusCodes.Status400BadRequest, ApiCodes.DateOutOfRange);
        }

        try
        {
            var (series, window) = await _daily.GetSeriesAsync(asked, ct);
            return Ok(DailyResponseFor(series, window, today));
        }
        catch (ArgumentException ex) when (ex.ParamName == MonthParameter)
        {
            // The service's own check of the month, which the checks above should already have made: whatever it refuses by that name is
            // the caller's month. An ArgumentException about anything else is not, and falls to the general case.
            return PeriodInvalid();
        }
        catch (Exception ex) when (!CallerLeft(ex, ct))
        {
            return Failure(ex);
        }
    }

    /// <summary>
    /// Where one day's visits came from, as GA4 names the channel and the source: what tells "the newsletter brought four hundred" from
    /// "it just spiked". Asks GA4 (kept by the service for thirty minutes), never starts the backfill.
    ///
    /// ONLY A DAY THE CHART CAN SHOW: from the first day of the oldest month the picker offers up to today. The service would take any
    /// day, and each distinct one is a Google call and a cache entry.
    /// </summary>
    /// <param name="date"><c>YYYY-MM-DD</c>, exactly.</param>
    [HttpGet("daily/sources")]
    [ProducesResponseType(typeof(DailySourcesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetDailySources([FromQuery] string? date, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return ProblemResult(StatusCodes.Status400BadRequest, ApiCodes.DateInvalid);
        }

        var today = Today();
        if (day < DailyChart.EarliestDay(today) || day > today) return ProblemResult(StatusCodes.Status400BadRequest, ApiCodes.DateOutOfRange);

        try
        {
            var sources = await _daily.GetSourcesAsync(day, ct);
            return Ok(new DailySourcesResponse(
                DailyChart.IsoDate(day),
                [.. sources.Select(x => new DailySourceRow(x.Channel, x.Source, x.Sessions))]));
        }
        catch (Exception ex) when (!CallerLeft(ex, ct))
        {
            return Failure(ex);
        }
    }

    /// <summary>Today by the injected clock (UTC), as the service reads it: the daily window and the bounds are counted from the same day.</summary>
    private DateOnly Today() => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

    private static DailyResponse DailyResponseFor(Ga4DailySeries series, Ga4DailyWindow window, DateOnly today) => new(
        DailyChart.Build(series),
        DailyChart.TextArgs(window, today),
        [.. DailyChart.MonthOptions(today)],
        new DailyWindowResponse(DailyChart.IsoDate(window.Start), DailyChart.IsoDate(window.End), window.Month),
        series.Total,
        series.ChangePercent is { } change && double.IsFinite(change) ? Math.Round(change, 1) : null,
        series.Peak is { } peak ? new DailyPeakResponse(DailyChart.IsoDate(peak.Date), peak.Sessions) : null,
        [.. series.Points.Select(x => new DailyDayResponse(DailyChart.IsoDate(x.Date), x.Sessions))],
        DailyChart.DaysIn(window));
}
