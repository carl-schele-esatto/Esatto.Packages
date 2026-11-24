using System.Text.Json.Nodes;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>Reads the golden files copied next to the test assembly (see the csproj).</summary>
internal static class Fixtures
{
    public static string Text(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));

    public static JsonNode Json(string file) => JsonNode.Parse(Text(file))!;
}
