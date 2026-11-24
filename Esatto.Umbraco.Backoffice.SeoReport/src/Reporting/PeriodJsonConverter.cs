using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// Writes a <see cref="Period"/> as its <c>YYYY-MM</c> string, wherever it sits (a property, a list, a dictionary key), and reads
/// nothing else back. Without it a period is a struct and would reach the browser as <c>{ "year": 2026, "month": 7 }</c> under
/// Umbraco's options, which is not the form the client or any other part of this package's wire format uses: the type carries it, so
/// that no response record has to remember to send a string instead.
///
/// STRICT ON THE WAY IN. A value that is not exactly <c>YYYY-MM</c> (a number, <c>null</c>, an object, a string with a space in it)
/// is a <see cref="JsonException"/>, the same shape <see cref="Period.TryParse"/> accepts. The error never repeats the value, which
/// comes from a request. <c>default(Period)</c> is not a month and is refused on the way out as well: it would otherwise be written
/// as <c>0000-00</c>, a string no reader accepts.
/// </summary>
public sealed class PeriodJsonConverter : JsonConverter<Period>
{
    public override Period Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.String ? Parse(reader.GetString()) : throw NotAPeriod();

    public override void Write(Utf8JsonWriter writer, Period value, JsonSerializerOptions options)
        => writer.WriteStringValue(Text(value));

    public override Period ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString());

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Period value, JsonSerializerOptions options)
        => writer.WritePropertyName(Text(value));

    private static Period Parse(string? text) => Period.TryParse(text, out var period) ? period : throw NotAPeriod();

    private static string Text(Period value) => value == default ? throw new JsonException("The default period is not a month.") : value.ToString();

    private static JsonException NotAPeriod() => new("A period must be a string written YYYY-MM.");
}
