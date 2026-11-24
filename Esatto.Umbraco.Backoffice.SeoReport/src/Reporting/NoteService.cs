using Esatto.Umbraco.Backoffice.SeoReport.Persistence;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// The note an editor writes about a month.
///
/// THE ONE INGREDIENT NO DATA SOURCE CAN PRODUCE. A cookie banner that went in, an outage, a core update at Google: those are the
/// lines that make a monthly report readable, and neither GA4 nor Search Console has them. Without them a drop reads as a drop
/// instead of as a gap in the measurement.
///
/// The note is not part of the report. The report is a measurement and does not change; the note is a person's interpretation
/// that can be rewritten at any time, so it has a row of its own, and writing it never touches the archive.
/// </summary>
public interface INoteService
{
    /// <summary>The longest note. Room for three or four sentences, not an essay: it is shown beside the summary and meant to be read at a glance.</summary>
    const int MaxLength = NoteEntity.MaxLength;

    /// <summary>The month's note, or null when there is none.</summary>
    Task<string?> GetAsync(Period period);

    /// <summary>
    /// Saves the note and returns what was saved: trimmed, and cut to <see cref="MaxLength"/> characters when longer (cut, not
    /// refused: the editor sees at once what was kept). Nothing or only white space deletes the note and returns null.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="period"/> is <c>default(Period)</c>, which is not a month.</exception>
    Task<string?> SaveAsync(Period period, string? text);
}

/// <inheritdoc />
public sealed class NoteService(ISeoReportStore store) : INoteService
{
    public async Task<string?> GetAsync(Period period)
    {
        PeriodGuard.Require(period);

        // Cleaned on the way out as well: white space that someone else wrote straight to the table reads as "no note".
        return Clean(await store.GetNoteAsync(period));
    }

    public async Task<string?> SaveAsync(Period period, string? text)
    {
        PeriodGuard.Require(period);

        var clean = Clean(text);
        await store.SaveNoteAsync(period, clean); // null deletes the row
        return clean;
    }

    /// <summary>
    /// A cleaned note. Empty text is null and not an empty string: null means "no note" and leaves the line out, while an empty
    /// string would draw an empty quotation.
    /// </summary>
    private static string? Clean(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length <= INoteService.MaxLength) return trimmed;

        // Cut at the limit, but never between the two halves of a surrogate pair (an emoji, say): a lone half is not valid text and
        // makes the JSON that carries the note fail.
        var length = char.IsHighSurrogate(trimmed[INoteService.MaxLength - 1]) ? INoteService.MaxLength - 1 : INoteService.MaxLength;
        return trimmed[..length];
    }
}
