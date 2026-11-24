using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>Every member of a JSON document, wherever it sits, with the path to it: for the tests that say "nowhere in the response".</summary>
internal static class JsonWalk
{
    public sealed record Member(string Path, string Name, JsonElement Value);

    /// <summary>The members of every object under <paramref name="root"/> (the root's own included), depth first, in document order.</summary>
    public static IEnumerable<Member> Members(JsonElement root) => Members(root, "$");

    private static IEnumerable<Member> Members(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var here = $"{path}.{property.Name}";
                    yield return new Member(here, property.Name, property.Value);

                    foreach (var inner in Members(property.Value, here)) yield return inner;
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var inner in Members(item, $"{path}[{index++}]")) yield return inner;
                }

                break;
        }
    }
}
