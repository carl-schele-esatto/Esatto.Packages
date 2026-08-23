using System;
using System.Collections.Generic;
using System.Linq;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookiePolicyPageResolverTests
{
    private static IContent Document(Guid key)
    {
        var document = Substitute.For<IContent>();
        document.Key.Returns(key);
        return document;
    }

    private static CookiePolicyPageResolver CreateSut(
        IPublishedContentCache cache,
        IContentTypeService contentTypeService,
        IContentService contentService,
        Guid? policyPageKey)
        => new(
            cache,
            contentTypeService,
            contentService,
            Options.Create(new CookieBannerOptions { PolicyPageKey = policyPageKey }),
            NullLogger<CookiePolicyPageResolver>.Instance);

    [Fact]
    public void Honours_the_explicit_policy_page_key_without_querying_the_document_type()
    {
        // Pins the PolicyPageKey override: a site with several cookiePolicy nodes must be able to
        // name the one the banner and footer point at, and the override must short-circuit the
        // by-type scan entirely rather than merely re-ordering it.
        var key = Guid.NewGuid();
        var expected = Substitute.For<IPublishedContent>();

        var cache = Substitute.For<IPublishedContentCache>();
        cache.GetById(key).Returns(expected);
        var contentTypeService = Substitute.For<IContentTypeService>();
        var contentService = Substitute.For<IContentService>();

        ICookiePolicyPageResolver resolver = CreateSut(cache, contentTypeService, contentService, key);

        Assert.Same(expected, resolver.Resolve());
        contentTypeService.DidNotReceiveWithAnyArgs().Get(default(string)!);
    }

    [Fact]
    public void Falls_back_to_the_first_published_node_of_the_cookie_policy_type()
    {
        // Pins the replacement for NDSTK's cookiePolicyPage Content Picker on the SITE's settings
        // doctype - a cross-model schema write a package may not make. Resolution is by document
        // type, and an unpublished candidate must be skipped: the published cache returns null for
        // it, so "first of the type" is not the same as "first PUBLISHED of the type".
        var draftKey = Guid.NewGuid();
        var publishedKey = Guid.NewGuid();
        var expected = Substitute.For<IPublishedContent>();

        var cache = Substitute.For<IPublishedContentCache>();
        cache.GetById(draftKey).Returns((IPublishedContent?)null);
        cache.GetById(publishedKey).Returns(expected);

        var contentType = Substitute.For<IContentType>();
        contentType.Id.Returns(1234);
        var contentTypeService = Substitute.For<IContentTypeService>();
        contentTypeService.Get(CookiePolicyPageResolver.ContentTypeAlias).Returns(contentType);

        var contentService = Substitute.For<IContentService>();
        // The candidate array must be built BEFORE the GetPagedOfType call is configured: each
        // Document() call configures its own substitute's .Key via .Returns(), and NSubstitute
        // tracks only one pending "last call" at a time. Building the array inline as an argument
        // to ReturnsForAnyArgs would let those nested .Returns() calls clear the pending
        // GetPagedOfType call before ReturnsForAnyArgs can configure it, throwing
        // CouldNotSetReturnDueToNoLastCallException - confirmed by isolated repro.
        var candidates = new[] { Document(draftKey), Document(publishedKey) };
        long total;
        // IContentService.GetPagedOfType declares `filter` as non-nullable on the interface even
        // though null (via `default`) is the valid "no filter" value used throughout this suite.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        contentService
            .GetPagedOfType(default, default, default, out total, default, default)
            .ReturnsForAnyArgs(candidates);
#pragma warning restore CS8625

        ICookiePolicyPageResolver resolver = CreateSut(cache, contentTypeService, contentService, null);

        Assert.Same(expected, resolver.Resolve());
    }

    [Fact]
    public void Returns_null_when_no_published_cookie_policy_page_exists()
    {
        // Pins that a site with the document type installed but nothing published (or with the
        // type missing entirely, on a boot before the schema installer ran) resolves to null
        // instead of throwing - the banner renders without a policy link, it does not 500.
        var cache = Substitute.For<IPublishedContentCache>();
        var contentTypeService = Substitute.For<IContentTypeService>();
        contentTypeService.Get(CookiePolicyPageResolver.ContentTypeAlias).Returns((IContentType?)null);
        var contentService = Substitute.For<IContentService>();

        ICookiePolicyPageResolver resolver = CreateSut(cache, contentTypeService, contentService, null);

        Assert.Null(resolver.Resolve());
    }
}
