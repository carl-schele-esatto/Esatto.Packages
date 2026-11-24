using System.Net;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Google;
using Google.Apis.Requests;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The problems the server names by a code (<see cref="ErrorCodes"/>) have a title and a text each in <c>en.ts</c>, looked up in
/// area <c>error</c> under the code. As for the report's texts (<see cref="EnglishTextsTests"/>, <see cref="AnalysisTextsTests"/>),
/// a text may read only arguments that are sent, and every argument that is sent must be read by some text. What is sent is what the
/// server really makes (the mapper, Test connection's no-data), plus what the CLIENT adds: the service account's e-mail, for the
/// problems whose cure is to add that address.
/// </summary>
public class ErrorTextsTests
{
    // What the client adds to a problem from the key status it already has; the server cannot know it (see GoogleErrorMapper).
    private static readonly Dictionary<string, string[]> AddedByTheClient = new()
    {
        [ErrorCodes.NoAccess] = ["email"],
        [ErrorCodes.PropertyNotFound] = ["email"],
    };

    private static GoogleApiException Failure(string service, HttpStatusCode status, string? reason = null)
        => new(service, "m")
        {
            HttpStatusCode = status,
            Error = new RequestError { Code = (int)status, Errors = reason is null ? null : [new SingleError { Reason = reason }] },
        };

    /// <summary>One problem for every way the server can send each code, as the mapper and Test connection make them.</summary>
    private static List<ProblemInfo> Sent() =>
    [
        .. new[] { ErrorCodes.NotConfigured, ErrorCodes.KeyUnreadable, ErrorCodes.KeyInvalid, ErrorCodes.Ga4PropertyInvalid, ErrorCodes.SearchConsoleInvalid }
            .Select(code => GoogleErrorMapper.Map(new ReportFetchException(code))),
        GoogleErrorMapper.Map(Failure("analyticsdata", HttpStatusCode.Forbidden, "accessNotConfigured")),
        GoogleErrorMapper.Map(Failure("searchconsole", HttpStatusCode.Forbidden, "accessNotConfigured")),
        GoogleErrorMapper.Map(Failure("analyticsdata", HttpStatusCode.Forbidden)),
        GoogleErrorMapper.Map(Failure("searchconsole", HttpStatusCode.Forbidden)),
        GoogleErrorMapper.Map(Failure("analyticsdata", HttpStatusCode.NotFound)),
        GoogleErrorMapper.Map(Failure("searchconsole", HttpStatusCode.NotFound)),
        GoogleErrorMapper.NoData(GoogleErrorMapper.Ga4),
        GoogleErrorMapper.NoData(GoogleErrorMapper.SearchConsole),
        GoogleErrorMapper.Map(Failure("analyticsdata", HttpStatusCode.TooManyRequests)),
        GoogleErrorMapper.Map(new InvalidOperationException("m")),
    ];

    private static IEnumerable<string> Keys(string code)
        => new[] { "Title", "Text" }.Select(part => ReportKeys.Lookup("error", ErrorCodes.BaseId(code), part));

    [Fact]
    public void The_samples_reach_every_code()
        => Assert.Equal(ErrorCodes.All.Order(), Sent().Select(problem => problem.Code).Distinct().Order());

    [Fact]
    public void A_text_only_reads_arguments_that_are_sent_and_every_argument_that_is_sent_is_read()
    {
        foreach (var code in ErrorCodes.All)
        {
            var sent = Sent()
                .Where(problem => problem.Code == code)
                .SelectMany(problem => problem.Args?.Keys ?? [])
                .Concat(AddedByTheClient.GetValueOrDefault(code, []))
                .ToHashSet(StringComparer.Ordinal);
            var read = new HashSet<string>(StringComparer.Ordinal);

            foreach (var key in Keys(code))
            {
                var parameters = EnglishTextsTests.Parameters(key);
                Assert.True(parameters.IsSubsetOf(sent), $"{key} reads {string.Join(", ", parameters.Except(sent))}, which is never sent");
                read.UnionWith(parameters);
            }

            Assert.True(sent.IsSubsetOf(read), $"{code} is sent {string.Join(", ", sent.Except(read))}, which no text reads");
        }
    }

    [Fact]
    public void The_arguments_are_the_three_the_texts_document()
        => Assert.Subset(
            new HashSet<string> { "api", "source", "email" },
            Sent().SelectMany(problem => problem.Args?.Keys ?? []).Concat(AddedByTheClient.Values.SelectMany(names => names)).ToHashSet());

    [Fact]
    public void Every_title_and_text_of_a_code_is_written()
        => Assert.All(ErrorCodes.All.SelectMany(Keys), key => Assert.Contains(key, ReportKeys.All));
}
