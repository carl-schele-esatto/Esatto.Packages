using System.Globalization;
using Esatto.Umbraco.Backoffice.Accessibility.Public;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class PublicTagHelperTests
{
    private static SettingsService Settings(string json)
    {
        var store = new TestStore { SettingsJson = json };
        return TestStore.Settings(store);
    }

    /// <summary>Stands in for asp-append-version's file hashing: appends ?v=1.</summary>
    private static IFileVersionProvider Versions()
    {
        var versions = Substitute.For<IFileVersionProvider>();
        versions.AddFileVersionToPath(Arg.Any<PathString>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(1) + "?v=1");
        return versions;
    }

    private static ViewContext RequestContext() => new() { HttpContext = new DefaultHttpContext() };

    private static (TagHelperContext, TagHelperOutput) Tag(string name)
    {
        var context = new TagHelperContext(name, [], new Dictionary<object, object>(), "id");
        var output = new TagHelperOutput(name, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        return (context, output);
    }

    private static async Task<string> RenderHead(string json)
    {
        var (context, output) = Tag("a11y-head");
        await new AccessibilityHeadTagHelper(Settings(json), Versions()) { ViewContext = RequestContext() }.ProcessAsync(context, output);
        using var writer = new StringWriter();
        output.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task Head_emits_nothing_when_all_visitor_features_are_off()
        => Assert.Equal(string.Empty, await RenderHead("""{"visitor":{"followOsMotion":false,"followOsContrast":false,"panel":{"enabled":false}}}"""));

    [Fact]
    public async Task Head_emits_css_and_boot_script_with_follow_flags()
    {
        var html = await RenderHead("""{"visitor":{"followOsMotion":true,"followOsContrast":false,"panel":{"enabled":false}}}""");

        Assert.Contains("""<link rel="stylesheet" href="/esatto-accessibility/a11y.css?v=1" />""", html);
        Assert.Contains("""<script src="/esatto-accessibility/a11y-boot.js?v=1" data-follow-motion="true" data-follow-contrast="false" data-options=""></script>""", html);
    }

    [Fact]
    public async Task Head_lists_enabled_panel_options_only_when_the_panel_is_on()
    {
        var html = await RenderHead("""{"visitor":{"followOsMotion":false,"panel":{"enabled":true,"readableFont":false,"readingGuide":true}}}""");
        Assert.Contains("data-options=\"text,spacing,contrast,motion,links,guide\"", html);
    }

    [Fact]
    public async Task Panel_emits_nothing_when_disabled()
    {
        var helper = Substitute.For<IViewComponentHelper, IViewContextAware>();
        var (context, output) = Tag("a11y-panel");

        await new AccessibilityPanelTagHelper(helper, Settings("""{"visitor":{"panel":{"enabled":false}}}""")) { ViewContext = new ViewContext() }.ProcessAsync(context, output);

        Assert.True(output.IsContentModified is false || output.Content.GetContent() == string.Empty);
        Assert.Null(output.TagName);
        await helper.DidNotReceiveWithAnyArgs().InvokeAsync(default(string)!, default);
    }

    [Fact]
    public async Task Panel_invokes_the_view_component_when_enabled()
    {
        var helper = Substitute.For<IViewComponentHelper, IViewContextAware>();
        helper.InvokeAsync(AccessibilityPanelTagHelper.ViewComponentName, Arg.Any<object?>()).Returns(new HtmlString("<div>panel</div>"));
        var (context, output) = Tag("a11y-panel");

        await new AccessibilityPanelTagHelper(helper, Settings("""{"visitor":{"panel":{"enabled":true}}}""")) { ViewContext = new ViewContext() }.ProcessAsync(context, output);

        Assert.Equal("<div>panel</div>", output.Content.GetContent());
    }

    [Fact]
    public void Panel_renders_only_enabled_options()
    {
        var panel = new PanelSettings { Enabled = true, ReadingGuide = false, ReadableFont = false };
        var model = AccessibilityPanelViewComponent.BuildModel(panel, CultureInfo.GetCultureInfo("en"));

        Assert.Equal(["text", "spacing", "contrast", "motion", "links"], model.Options.Select(o => o.Pref));
        Assert.Equal("Larger text", model.Options[0].Label);
        Assert.Equal("bottom-right", model.Position);
    }

    [Fact]
    public async Task Panel_script_url_carries_the_file_version_so_upgrades_reach_browsers()
    {
        var component = new AccessibilityPanelViewComponent(Settings("""{"visitor":{"panel":{"enabled":true}}}"""), Versions())
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new ViewContext
                {
                    HttpContext = new DefaultHttpContext(),
                    ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
                },
            },
        };

        var result = Assert.IsType<ViewViewComponentResult>(await component.InvokeAsync());

        Assert.Equal("/esatto-accessibility/a11y-panel.js?v=1", Assert.IsType<PanelViewModel>(result.ViewData!.Model).ScriptUrl);
    }
}

public class PanelTextProviderTests
{
    [Fact]
    public void Text_override_wins_then_swedish_resx_then_english()
    {
        var panel = new PanelSettings();
        panel.Texts["sv"] = new Dictionary<string, string> { ["ButtonLabel"] = "Tillgänglighet & läsbarhet" };
        var sv = CultureInfo.GetCultureInfo("sv-SE");

        Assert.Equal("Tillgänglighet & läsbarhet", PanelTextProvider.Get(panel, "ButtonLabel", sv));
        Assert.Equal("Större knappar och mer luft", PanelTextProvider.Get(panel, "Spacing", sv));
        Assert.Equal("Bigger buttons and more spacing", PanelTextProvider.Get(panel, "Spacing", CultureInfo.GetCultureInfo("de")));
    }

    [Fact]
    public void Unknown_key_returns_the_key() => Assert.Equal("Nope", PanelTextProvider.Get(new PanelSettings(), "Nope", CultureInfo.InvariantCulture));
}
