using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The Settings tab previews the Search Console property as the server will store it, with its own copy of
/// <see cref="SettingsNormalizer.NormalizeSearchConsole"/> (Client/src/search-console.ts). Both are tested against ONE list of inputs and what the
/// server makes of each (null: refused), Client/src/testing/search-console-vectors.json: here, so that every expected value is the server's own
/// answer, and in search-console.test.ts, so that the client gives the same one. A rule that changes on one side fails on the other.
/// </summary>
public class SearchConsoleVectorsTests
{
    private sealed record Vector(string Input, string? Expected);

    private static readonly Lazy<Vector[]> Vectors = new(() =>
        JsonSerializer.Deserialize<Vector[]>(
            File.ReadAllText(Path.Combine(ClientFiles.ClientDirectory(), "src", "testing", "search-console-vectors.json")),
            JsonSerializerOptions.Web)!);

    public static TheoryData<int> Rows()
    {
        var rows = new TheoryData<int>();
        for (var i = 0; i < Vectors.Value.Length; i++) rows.Add(i);
        return rows;
    }

    // By row number: some inputs are 2,000 characters long or hold characters that make poor test names.
    [Theory]
    [MemberData(nameof(Rows))]
    public void Every_shared_vector_is_what_the_server_stores(int row)
    {
        var vector = Vectors.Value[row];

        Assert.Equal(vector.Expected, SettingsNormalizer.NormalizeSearchConsole(vector.Input));
    }

    [Fact]
    public void The_shared_vectors_cover_both_kinds_and_refusals()
    {
        var vectors = Vectors.Value;

        Assert.True(vectors.Length >= 150, $"only {vectors.Length} vectors");
        Assert.Contains(vectors, v => v.Expected?.StartsWith("sc-domain:", StringComparison.Ordinal) == true);
        Assert.Contains(vectors, v => v.Expected?.StartsWith("https://", StringComparison.Ordinal) == true);
        Assert.Contains(vectors, v => v.Expected is null);
        Assert.Equal(vectors.Length, vectors.Select(v => v.Input).Distinct(StringComparer.Ordinal).Count());
    }
}
