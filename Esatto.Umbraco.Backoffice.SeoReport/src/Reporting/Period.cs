using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// A calendar month, written <c>YYYY-MM</c> on the wire and in the archive. It is the only way a period gets into a key
/// or a query: <see cref="TryParse"/> accepts exactly that shape, so nothing else (whitespace, a path) travels onward. The JSON
/// form is the same string, wherever a period sits (<see cref="PeriodJsonConverter"/>).
/// </summary>
[JsonConverter(typeof(PeriodJsonConverter))]
public readonly record struct Period(int Year, int Month) : IComparable<Period>
{
    // Years 1-9999 are the DateOnly range and are exactly what four digits can print.
    public int Year { get; } = InRange(Year, 1, 9999);

    public int Month { get; } = InRange(Month, 1, 12);

    /// <summary>Parses exactly <c>YYYY-MM</c>: four digits, a hyphen, a month 01-12. Nothing is trimmed.</summary>
    public static bool TryParse(string? text, out Period period)
    {
        period = default;
        if (text is not { Length: 7 } || text[4] != '-') return false;

        // NumberStyles.None: ASCII digits only, so no sign, no whitespace, no full-width digits.
        if (!int.TryParse(text.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(text.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || year < 1 || month is < 1 or > 12)
        {
            return false;
        }

        period = new Period(year, month);
        return true;
    }

    public static Period Parse(string text)
        => TryParse(text, out var period)
            ? period
            : throw new ArgumentException($"'{text}' is not a YYYY-MM period.", nameof(text));

    public static Period FromDate(DateOnly date) => new(date.Year, date.Month);

    /// <summary>Moves by whole months, backwards when negative. Throws when the result leaves years 1-9999.</summary>
    public Period AddMonths(int months) => FromDate(new DateOnly(Year, Month, 1).AddMonths(months));

    public Period Previous() => AddMonths(-1);

    public int CompareTo(Period other)
        => Year != other.Year ? Year.CompareTo(other.Year) : Month.CompareTo(other.Month);

    public static bool operator <(Period left, Period right) => left.CompareTo(right) < 0;

    public static bool operator >(Period left, Period right) => left.CompareTo(right) > 0;

    public static bool operator <=(Period left, Period right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Period left, Period right) => left.CompareTo(right) >= 0;

    private static int InRange(int value, int min, int max, [CallerArgumentExpression(nameof(value))] string? name = null)
        => value >= min && value <= max
            ? value
            : throw new ArgumentOutOfRangeException(name, value, $"Must be between {min} and {max}.");

    /// <summary>The wire form, <c>YYYY-MM</c>.</summary>
    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
