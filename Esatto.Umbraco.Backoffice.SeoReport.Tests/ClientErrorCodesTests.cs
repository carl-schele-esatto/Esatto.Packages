using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The client keeps a copy of the two code lists (<c>Client/src/error-codes.ts</c>): the words it has a title and a text for. A code the
/// server learns and the client does not would be shown as "unknown", so the copy has to be the same list. Only the names are compared;
/// the wording is held to the registry by <see cref="EnglishTextsTests"/>.
/// </summary>
public class ClientErrorCodesTests
{
    private static readonly Lazy<string> Source = new(() => File.ReadAllText(Path.Combine(ClientFiles.ClientDirectory(), "src", "error-codes.ts")));

    /// <summary>The string literals of <c>export const NAME = [ ... ] as const;</c>.</summary>
    private static List<string> ClientList(string name)
    {
        var list = Regex.Match(Source.Value, $@"export const {name} = \[(?<items>[^\]]*)\]");
        Assert.True(list.Success, $"{name} is not in error-codes.ts");

        return [.. Regex.Matches(list.Groups["items"].Value, @"'(?<code>[A-Za-z0-9]+)'").Select(m => m.Groups["code"].Value)];
    }

    [Fact]
    public void The_client_knows_every_code_Google_and_the_key_can_produce_and_no_other()
        => Assert.Equal(ErrorCodes.All.Order(StringComparer.Ordinal), ClientList("GOOGLE_ERROR_CODES").Order(StringComparer.Ordinal));

    [Fact]
    public void The_client_knows_every_code_the_API_itself_answers_with_and_no_other()
        => Assert.Equal(ApiCodes.All.Order(StringComparer.Ordinal), ClientList("API_ERROR_CODES").Order(StringComparer.Ordinal));

    [Fact]
    public void The_API_codes_are_every_ApiCodes_constant()
    {
        // ApiCodes.All is written by hand; a constant added without it would have no text and no key.
        var constants = typeof(ApiCodes).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!);

        Assert.Equal(constants.Order(StringComparer.Ordinal), ApiCodes.All.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_API_code_is_registered_as_a_title_and_a_text_under_its_snake_case_id()
    {
        foreach (var code in ApiCodes.All)
        {
            Assert.Contains(ReportKeys.Lookup("error", ErrorCodes.BaseId(code), "Title"), ReportKeys.All);
            Assert.Contains(ReportKeys.Lookup("error", ErrorCodes.BaseId(code), "Text"), ReportKeys.All);
        }

        Assert.Equal("errorPeriodOutOfRangeTitle", ReportKeys.Lookup("error", ErrorCodes.BaseId(ApiCodes.PeriodOutOfRange), "Title"));
    }
}
