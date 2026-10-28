using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.Accessibility.Knowledge;

/// <summary>
/// The WCAG criteria and axe rule guidance shipped in the assembly (<c>Knowledge/*.en.json</c>).
/// Loaded once; register as a singleton.
/// </summary>
/// <remarks>
/// Files are named by culture so a translation can be added later without changing the format;
/// 1.0 ships English only.
/// </remarks>
public sealed class KnowledgeBase
{
    private const string ResourcePrefix = "Esatto.Umbraco.Backoffice.Accessibility.Knowledge.";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, Criterion> _byId;
    private readonly Dictionary<string, AxeRuleGuide> _rules;

    public KnowledgeBase()
    {
        All = Load<List<Criterion>>("criteria.en.json");
        _byId = All.ToDictionary(c => c.Id);
        _rules = Load<Dictionary<string, RuleText>>("axe-rules.en.json")
            .ToDictionary(kv => kv.Key, kv => new AxeRuleGuide(kv.Key, kv.Value.Owner, kv.Value.HowToFix, kv.Value.Caveats));
    }

    public IReadOnlyList<Criterion> All { get; }

    public IReadOnlyCollection<AxeRuleGuide> Rules => _rules.Values;

    public Criterion? Find(string id) => _byId.GetValueOrDefault(id);

    public IReadOnlyList<Criterion> For(WcagTarget target) => All.Where(target.Includes).ToList();

    public AxeRuleGuide? Rule(string ruleId) => _rules.GetValueOrDefault(ruleId);

    private static T Load<T>(string file)
    {
        using var stream = typeof(KnowledgeBase).Assembly.GetManifestResourceStream(ResourcePrefix + file)
            ?? throw new InvalidOperationException($"Embedded knowledge file '{file}' is missing.");
        return JsonSerializer.Deserialize<T>(stream, Json)
            ?? throw new InvalidOperationException($"Embedded knowledge file '{file}' is empty.");
    }

    private sealed record RuleText(Owner? Owner, string? HowToFix, string? Caveats);
}
