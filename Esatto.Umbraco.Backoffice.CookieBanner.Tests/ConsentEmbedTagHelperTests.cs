using Microsoft.AspNetCore.Razor.TagHelpers;
using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentEmbedTagHelperTests
{
    /// <summary>Echoes the key back so a test can assert which key was asked for.</summary>
    private sealed class StubTextProvider : IConsentTextProvider
    {
        public string Get(string key) => $"[{key}]";
    }

    private static TagHelperContext Context() => new(
        new TagHelperAttributeList(),
        new Dictionary<object, object>(),
        Guid.NewGuid().ToString());

    private static TagHelperOutput Output() => new(
        "consent-embed",
        new TagHelperAttributeList(),
        (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

    private static ConsentEmbedTagHelper Helper(IConsentState consent) =>
        new(consent, new StubTextProvider())
        {
            Category = ConsentCategory.Marketing,
            Src = "https://www.youtube-nocookie.com/embed/abc",
            Title = "Team video",
        };

    // Pins that a granted category renders the real iframe with its src and title intact.
    [Fact]
    public void Renders_an_iframe_when_granted()
    {
        TagHelperOutput output = Output();

        Helper(new FakeConsentState(ConsentCategory.Marketing)).Process(Context(), output);

        var html = output.Content.GetContent();
        Assert.Equal("div", output.TagName);
        Assert.Contains("<iframe", html);
        Assert.Contains("https://www.youtube-nocookie.com/embed/abc", html);
        Assert.Contains("title=\"Team video\"", html);
    }

    // Pins that the ungranted case renders an invite, not a hidden iframe, and reads both text keys.
    [Fact]
    public void Renders_a_placeholder_with_no_iframe_when_not_granted()
    {
        TagHelperOutput output = Output();

        Helper(new FakeConsentState()).Process(Context(), output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<iframe", html);
        Assert.Contains("data-consent-open", html);
        Assert.Contains("[Cookies.Embed.Blocked.Body]", html);
        Assert.Contains("[Cookies.Embed.Blocked.Button]", html);
    }

    // SECURITY: pins that a blocked embed leaks the URL nowhere - not in a data attribute, not
    // hidden, not commented out. Leaking it is how "blocked" embeds end up firing requests anyway.
    [Fact]
    public void The_placeholder_never_leaks_the_embed_url()
    {
        TagHelperOutput output = Output();

        Helper(new FakeConsentState()).Process(Context(), output);

        Assert.DoesNotContain("youtube-nocookie.com", output.Content.GetContent());
    }

    // Pins XSS escaping: an editor-supplied title is HTML-encoded before it reaches the iframe.
    [Fact]
    public void Escapes_a_hostile_title()
    {
        TagHelperOutput output = Output();
        ConsentEmbedTagHelper helper = Helper(new FakeConsentState(ConsentCategory.Marketing));
        helper.Title = "\"><script>alert(1)</script>";

        helper.Process(Context(), output);

        Assert.DoesNotContain("<script>alert(1)</script>", output.Content.GetContent());
    }

    // Pins the packaging rule: the placeholder button styles itself and must not depend on a
    // host class such as .btn-primary, which only ever existed in NDSTK's site.css.
    [Fact]
    public void The_placeholder_button_uses_only_package_owned_classes()
    {
        TagHelperOutput output = Output();

        Helper(new FakeConsentState()).Process(Context(), output);

        var html = output.Content.GetContent();
        Assert.Contains("class=\"consent-btn consent-btn--primary\"", html);
        Assert.DoesNotContain("btn-primary", html);
        Assert.Equal("consent-embed consent-embed--blocked", output.Attributes["class"].Value);
        Assert.Equal("marketing", output.Attributes["data-consent-category"].Value);
    }
}
