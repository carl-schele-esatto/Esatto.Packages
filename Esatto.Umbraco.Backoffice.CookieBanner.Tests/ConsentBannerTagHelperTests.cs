using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentBannerTagHelperTests
{
    private const string DialogMarkup = """<dialog id="esatto-consent-dialog"></dialog>""";

    private static TagHelperContext Context() => new(
        new TagHelperAttributeList(),
        new Dictionary<object, object>(),
        Guid.NewGuid().ToString());

    private static TagHelperOutput Output() => new(
        "consent-banner",
        new TagHelperAttributeList(),
        (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

    [Fact]
    public async Task Renders_the_ConsentBanner_view_component_in_place_of_the_element()
    {
        // <consent-banner /> must collapse to the dialog markup itself: a leftover <consent-banner>
        // element would be an unknown inline element wrapping a modal dialog.
        var helper = new FakeViewComponentHelper(DialogMarkup);
        var tagHelper = new ConsentBannerTagHelper(helper) { ViewContext = new ViewContext() };
        TagHelperOutput output = Output();

        await tagHelper.ProcessAsync(Context(), output);

        Assert.Equal("ConsentBanner", helper.InvokedName);
        Assert.Null(output.TagName);
        Assert.Equal(DialogMarkup, output.Content.GetContent());
    }

    [Fact]
    public async Task Contextualizes_the_view_component_helper_before_invoking_it()
    {
        // IViewComponentHelper is injected without a ViewContext; invoking it uncontextualized throws
        // InvalidOperationException at request time, which is exactly the bug this pins.
        var helper = new FakeViewComponentHelper(DialogMarkup);
        var tagHelper = new ConsentBannerTagHelper(helper) { ViewContext = new ViewContext() };

        await tagHelper.ProcessAsync(Context(), Output());

        Assert.True(helper.ContextualizedBeforeInvoke);
    }

    private sealed class FakeViewComponentHelper(string html) : IViewComponentHelper, IViewContextAware
    {
        private bool _contextualized;

        public string? InvokedName { get; private set; }

        public bool ContextualizedBeforeInvoke { get; private set; }

        public void Contextualize(ViewContext viewContext) => _contextualized = true;

        public Task<IHtmlContent> InvokeAsync(string name, object? arguments)
        {
            InvokedName = name;
            ContextualizedBeforeInvoke = _contextualized;
            return Task.FromResult<IHtmlContent>(new HtmlString(html));
        }

        public Task<IHtmlContent> InvokeAsync(Type componentType, object? arguments)
            => throw new NotSupportedException("The tag helper must invoke by name.");
    }
}
