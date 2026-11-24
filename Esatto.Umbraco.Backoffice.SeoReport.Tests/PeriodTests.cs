using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

public class PeriodTests
{
    [Fact]
    public void TryParse_accepts_YYYY_MM_and_round_trips_through_ToString()
    {
        Assert.True(Period.TryParse("2026-07", out var period));
        Assert.Equal(new Period(2026, 7), period);
        Assert.Equal("2026-07", period.ToString());
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-7")]
    [InlineData("2026-00")]
    [InlineData("0000-07")]
    [InlineData("../2026")]
    [InlineData(" 2026-07")]
    [InlineData("2026-07 ")]
    [InlineData("2026-07-01")]
    [InlineData("+026-07")]
    [InlineData("2026/07")]
    [InlineData("２０２６-０７")] // full-width digits are not ASCII digits
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_rejects_anything_that_is_not_exactly_YYYY_MM(string? input)
    {
        Assert.False(Period.TryParse(input, out var period));
        Assert.Equal(default(Period), period);
    }

    [Fact]
    public void Parse_throws_ArgumentException_for_an_invalid_period()
    {
        Assert.Throws<ArgumentException>(() => Period.Parse("2026-7"));
        Assert.Throws<ArgumentException>(() => Period.Parse(null!));
    }

    [Fact]
    public void The_constructor_rejects_a_month_or_year_out_of_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Period(2026, 13));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Period(2026, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Period(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Period(10000, 1));
    }

    [Fact]
    public void FromDate_takes_the_year_and_month()
        => Assert.Equal("2026-02", Period.FromDate(new DateOnly(2026, 2, 28)).ToString());

    [Fact]
    public void Previous_steps_back_across_a_year_boundary()
        => Assert.Equal("2025-12", Period.Parse("2026-01").Previous().ToString());

    [Theory]
    [InlineData("2026-11", -13, "2025-10")]
    [InlineData("2026-11", 0, "2026-11")]
    [InlineData("2026-11", 2, "2027-01")]
    [InlineData("2026-01", -12, "2025-01")]
    public void AddMonths_moves_by_whole_months(string start, int months, string expected)
        => Assert.Equal(expected, Period.Parse(start).AddMonths(months).ToString());

    [Fact]
    public void AddMonths_throws_when_the_result_leaves_the_representable_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Period.Parse("9999-12").AddMonths(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Period.Parse("0001-01").Previous());
    }

    [Fact]
    public void Periods_order_chronologically()
    {
        var december = Period.Parse("2025-12");
        var january = Period.Parse("2026-01");

        Assert.True(december < january);
        Assert.True(january > december);
        Assert.True(december <= Period.Parse("2025-12"));
        Assert.True(january >= Period.Parse("2026-01"));
        Assert.True(december.CompareTo(january) < 0);
        Assert.Equal(0, january.CompareTo(Period.Parse("2026-01")));
        Assert.Equal([december, january], new[] { january, december }.Order());
    }
}
