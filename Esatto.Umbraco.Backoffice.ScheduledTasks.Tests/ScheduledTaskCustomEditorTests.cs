using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public enum FeedFormat
{
    Xml,
    Json,
}

public sealed class FeedSource
{
    public string Url { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 30;

    public FeedFormat Format { get; set; } = FeedFormat.Xml;
}

public sealed class CustomEditorProperties
{
    [ScheduledTaskProperty("Feed", EditorUiAlias = "My.PropertyEditorUi.FeedSource", ConfigJson = """{ "allowHttp": false, "formats": ["xml", "json"] }""")]
    public FeedSource? Feed { get; set; } = new() { Url = "https://feeds.example.com/products.xml" };

    [ScheduledTaskProperty(EditorUiAlias = "Umb.PropertyEditorUi.Tags", ConfigJson = """{ "group": "jobs" }""")]
    public List<string> Tags { get; set; } = [];

    [ScheduledTaskProperty(EditorUiAlias = "My.PropertyEditorUi.Slider")]
    public int Priority { get; set; } = 5;

    [ScheduledTaskProperty(EditorUiAlias = "My.PropertyEditorUi.Raw")]
    public JsonNode? Raw { get; set; }

    [ScheduledTaskProperty(EditorUiAlias = "My.PropertyEditorUi.Broken", ConfigJson = "[1, 2]")]
    public string? BrokenConfig { get; set; }

    [ScheduledTaskProperty(Editor = ScheduledTaskPropertyEditor.Custom)]
    public string? NoAlias { get; set; }
}

public class ScheduledTaskCustomEditorTests
{
    private readonly ScheduledTaskPropertyValues _values = new(NullLogger<ScheduledTaskPropertyValues>.Instance);

    private static ScheduledTaskPropertyDefinition Definition(string alias)
        => ScheduledTaskPropertySchema.For(typeof(CustomEditorProperties)).Single(d => d.Alias == alias);

    [Fact]
    public void An_editor_ui_alias_picks_that_editor_with_its_config()
    {
        var feed = Definition("feed");

        Assert.Equal(ScheduledTaskPropertyEditor.Custom, feed.Editor);
        Assert.Equal("My.PropertyEditorUi.FeedSource", feed.PropertyEditorUiAlias);
        Assert.Null(feed.Error);
        Assert.Equal(["allowHttp", "formats"], feed.Config.Select(c => c.Alias));
        Assert.False(feed.Config[0].Value!.GetValue<bool>());
        Assert.Equal(2, feed.Config[1].Value!.AsArray().Count);
    }

    [Fact]
    public void Defaults_are_shown_as_the_json_of_the_propertys_type()
    {
        var form = _values.Form(typeof(CustomEditorProperties), null);

        var feed = form.Values["feed"]!.AsObject();
        Assert.Equal("https://feeds.example.com/products.xml", feed["url"]!.GetValue<string>());
        Assert.Equal(30, feed["timeoutSeconds"]!.GetValue<int>());
        Assert.Equal("Xml", feed["format"]!.GetValue<string>());
        Assert.Empty(form.Values["tags"]!.AsArray());
        Assert.Equal(5, form.Values["priority"]!.GetValue<int>());
    }

    [Fact]
    public void An_untouched_form_stores_nothing()
    {
        var form = _values.Form(typeof(CustomEditorProperties), null);

        var (json, errors) = _values.Prepare(typeof(CustomEditorProperties), form.Values);

        Assert.Null(errors);
        Assert.Null(json);
    }

    [Fact]
    public void The_editors_value_is_read_into_your_own_class()
    {
        var values = new Dictionary<string, JsonNode?>
        {
            ["feed"] = JsonNode.Parse("""{ "url": "https://example.com/feed.json", "timeoutSeconds": 90, "format": "json", "extra": true }"""),
            ["tags"] = JsonNode.Parse("""["nightly", "products"]"""),
            ["priority"] = 8,
            ["raw"] = JsonNode.Parse("""{ "anything": [1, 2, 3] }"""),
        };

        var (json, errors) = _values.Prepare(typeof(CustomEditorProperties), values);
        Assert.Null(errors);

        var bound = _values.Bind<CustomEditorProperties>("recurring:X", json);
        Assert.Equal("https://example.com/feed.json", bound.Feed!.Url);
        Assert.Equal(90, bound.Feed.TimeoutSeconds);
        Assert.Equal(FeedFormat.Json, bound.Feed.Format);
        Assert.Equal(["nightly", "products"], bound.Tags);
        Assert.Equal(8, bound.Priority);
        Assert.Equal(3, bound.Raw!["anything"]!.AsArray().Count);
    }

    [Fact]
    public void Only_changed_values_are_stored_and_the_saved_json_comes_back_into_the_form()
    {
        var form = _values.Form(typeof(CustomEditorProperties), null);
        var values = new Dictionary<string, JsonNode?>(form.Values)
        {
            ["feed"] = JsonNode.Parse("""{ "url": "https://example.com/other.xml", "timeoutSeconds": 30, "format": "Xml" }"""),
        };

        var (json, _) = _values.Prepare(typeof(CustomEditorProperties), values);

        var stored = JsonNode.Parse(json!)!.AsObject();
        Assert.Equal(["feed"], stored.Select(p => p.Key));
        Assert.Equal("https://example.com/other.xml", _values.Form(typeof(CustomEditorProperties), json).Values["feed"]!["url"]!.GetValue<string>());
    }

    [Fact]
    public void A_value_that_doesnt_fit_the_type_is_rejected_by_alias()
    {
        var values = new Dictionary<string, JsonNode?>
        {
            ["feed"] = JsonNode.Parse("""{ "timeoutSeconds": "soon" }"""),
            ["priority"] = "high",
        };

        var (json, errors) = _values.Prepare(typeof(CustomEditorProperties), values);

        Assert.Null(json);
        Assert.Contains("FeedSource", errors!["feed"].Single());
        Assert.True(errors.ContainsKey("priority"));
    }

    [Fact]
    public void A_non_nullable_value_type_is_required()
    {
        Assert.True(Definition("priority").Mandatory);
        Assert.False(Definition("feed").Mandatory);

        var (_, errors) = _values.Prepare(typeof(CustomEditorProperties), new Dictionary<string, JsonNode?> { ["priority"] = null });

        Assert.True(errors!.ContainsKey("priority"));
    }

    [Fact]
    public void Clearing_a_reference_type_stores_null()
    {
        var (json, errors) = _values.Prepare(typeof(CustomEditorProperties), new Dictionary<string, JsonNode?> { ["feed"] = null });

        Assert.Null(errors);
        Assert.Null(_values.Bind<CustomEditorProperties>("recurring:X", json).Feed);
    }

    [Fact]
    public void Bad_config_or_a_missing_alias_is_explained_and_the_rest_of_the_form_works()
    {
        Assert.Contains("JSON object", Definition("brokenConfig").Error);
        Assert.Null(Definition("brokenConfig").Converter);
        Assert.Contains("EditorUiAlias", Definition("noAlias").Error);
        Assert.Null(Definition("feed").Error);
    }

    [Fact]
    public void Invalid_config_json_says_so()
    {
        ScheduledTaskPropertyEditors.BindCustom("My.Ui", "{ not json", typeof(string), out var error);

        Assert.Contains("isn't valid JSON", error);
    }
}
