using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The server can only emit registered keys (ReportMapperTests), and every registered key must have English wording in
/// <c>Client/src/lang/en.ts</c>, or the dashboard shows a raw key. en.ts is read as text: an entry is a property at the start of
/// a line, <c>key: 'text'</c> or <c>key: ({ arg, ... }: {...}) =&gt; `...`</c>.
/// </summary>
public class EnglishTextsTests
{
    private static readonly Lazy<string> En = new(() => File.ReadAllText(Path.Combine(ClientFiles.ClientDirectory(), "src", "lang", "en.ts")));

    /// <summary>The line that defines <paramref name="key"/>: indentation, the name, a colon. Never part of another name or a text.</summary>
    private static Match Entry(string key) => Regex.Match(En.Value, $@"(?m)^[ \t]+{Regex.Escape(key)}[ \t]*:[ \t]*(?<value>.*)$");

    /// <summary>The argument names a function entry destructures, or none for a plain string.</summary>
    internal static HashSet<string> Parameters(string key)
    {
        var value = Entry(key).Groups["value"].Value;
        var destructured = Regex.Match(value, @"^\(\{(?<names>[^}]*)\}");
        if (!destructured.Success) return [];

        return [.. destructured.Groups["names"].Value.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0)];
    }

    [Fact]
    public void EveryRegisteredKeyHasEnglishText()
    {
        var missing = ReportKeys.All.Where(key => !Entry(key).Success).ToList();

        Assert.NotEmpty(ReportKeys.All);
        Assert.True(missing.Count == 0, "No English text in en.ts for: " + string.Join(", ", missing));
    }

    [Fact]
    public void The_entry_pattern_matches_a_whole_property_name_only()
    {
        Assert.True(Entry("dashboardTitle").Success);
        Assert.False(Entry("dashboardTitl").Success);
        Assert.False(Entry("ashboardTitle").Success);
    }

    // A function entry whose argument object has a default: ({ a }: {...} = {}) =>
    private static readonly Regex DefaultedArguments = new(@"^\(\{[^}]*\}(?:\s*:\s*\{[^}]*\})?\s*=\s*\{\s*\}\s*\)\s*=>");

    /// <summary>Whether a function entry gives its argument object a default, so it can be called with none.</summary>
    private static bool HasDefault(string key) => DefaultedArguments.IsMatch(Entry(key).Groups["value"].Value);

    private static bool IsFunction(string key) => Entry(key).Groups["value"].Value.StartsWith('(');

    [Fact]
    public void A_text_the_server_sends_without_arguments_is_a_string_or_a_function_that_needs_none()
    {
        // The server omits "args" when a text has nothing to send (the Search Console lag note without a date, say), so the
        // client calls the text with no object. A function that destructures one without a default throws there.
        var bare = ReportMapperTests.AllSamples()
            .SelectMany(ReportTexts.Walk)
            .Where(u => u.ArgNames.Count == 0)
            .Select(u => u.Key)
            .Distinct()
            .ToList();

        Assert.Contains("uiGscLagText", bare);
        Assert.All(bare, key => Assert.True(!IsFunction(key) || HasDefault(key), $"{key} is sent without arguments but needs them"));
    }

    [Fact]
    public void Every_function_entry_of_a_registered_key_defaults_its_arguments()
    {
        // The general rule behind the test above: no report text can throw for want of arguments, whichever ones the server
        // happens to send. (Client/src/lang/en.test.ts also calls each one with none and checks it still reads as English.)
        var functions = ReportKeys.All.Where(IsFunction).ToList();

        Assert.NotEmpty(functions);
        Assert.All(functions, key => Assert.True(HasDefault(key), $"{key} has no default for its argument object"));
    }

    [Fact]
    public void The_default_pattern_tells_a_default_from_none()
    {
        Assert.Matches(DefaultedArguments, "({ a, b }: { a?: number; b?: string } = {}) => `${a}`");
        Assert.DoesNotMatch(DefaultedArguments, "({ a, b }: { a?: number; b?: string }) => `${a}`");
    }

    [Fact]
    public void A_text_only_reads_arguments_the_server_sends_and_every_argument_sent_is_read()
    {
        // Each text reference (an area and base id) with the argument names it was ever sent with, across every sample report.
        var sent = ReportMapperTests.AllSamples()
            .SelectMany(ReportTexts.Walk)
            .GroupBy(u => (u.Area, u.BaseId))
            .ToList();

        Assert.NotEmpty(sent);
        foreach (var group in sent)
        {
            var names = group.SelectMany(u => u.ArgNames).ToHashSet(StringComparer.Ordinal);
            var read = new HashSet<string>(StringComparer.Ordinal);

            foreach (var key in group.Select(u => u.Key).Distinct())
            {
                var parameters = Parameters(key);
                Assert.True(parameters.IsSubsetOf(names), $"{key} reads {string.Join(", ", parameters.Except(names))}, which the server never sends");
                read.UnionWith(parameters);
            }

            Assert.True(names.IsSubsetOf(read), $"{group.Key.Area}/{group.Key.BaseId} is sent {string.Join(", ", names.Except(read))}, which no text reads");
        }
    }
}
