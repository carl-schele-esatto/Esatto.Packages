using System;
using System.Collections.Generic;
using System.Linq;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerContentSeederTests
{
    /// <summary>Records every log call's level and rendered message; no assertion needs a mocking library.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class Harness
    {
        public IContentService ContentService { get; init; } = null!;
        public IContentTypeService ContentTypeService { get; init; } = null!;
        public IEntityService EntityService { get; init; } = null!;
        public IContent Policy { get; init; } = null!;
        public Func<BlockListValue?> Registry { get; init; } = null!;
        public RecordingLogger<CookieBannerContentSeeder> Logger { get; init; } = null!;
        public CookieBannerContentSeeder Seeder { get; init; } = null!;
    }

    private static Harness CreateSut(string cookieName, bool alreadySeeded = false, params IContent[] existingOfType)
    {
        var contentType = Substitute.For<IContentType>();
        contentType.Id.Returns(1234);
        var contentTypeService = Substitute.For<IContentTypeService>();
        contentTypeService.Get(CookiePolicyPageResolver.ContentTypeAlias).Returns(contentType);

        var root = Substitute.For<IContent>();
        root.Id.Returns(1000);
        root.Name.Returns("Home");

        var policy = Substitute.For<IContent>();

        var contentService = Substitute.For<IContentService>();
        long total;
        // IContentService.GetPagedOfType declares `filter` as non-nullable on the interface even
        // though null (via `default`) is the valid "no filter" value used throughout this suite.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        contentService
            .GetPagedOfType(default, default, default, out total, default, default)
            .ReturnsForAnyArgs(existingOfType);
#pragma warning restore CS8625
        contentService.GetRootContent().Returns(new[] { root });
        contentService
            .Create(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(policy);
        contentService
            .Publish(Arg.Any<IContent>(), Arg.Any<string[]>(), Arg.Any<int>())
            .Returns(new PublishResult(PublishResultType.SuccessPublish, new EventMessages(), policy));

        BlockListValue? registry = null;
        var jsonSerializer = Substitute.For<IJsonSerializer>();
        // A single fixed .Returns("[]") would make every Serialize() call - the block registry
        // AND every per-property Dropdown() call - come back identical, so the category and
        // storageType assertions below could never distinguish "necessary" from "Cookie" from
        // anything else. Route through a real serializer instead of hardcoding the expected
        // literal here, which would just restate the assertions rather than exercise them.
        jsonSerializer.Serialize(Arg.Any<object>()).Returns(callInfo =>
        {
            object? value = callInfo.Arg<object>();
            if (value is BlockListValue blockList)
            {
                registry ??= blockList;
                return "{}";
            }

            return System.Text.Json.JsonSerializer.Serialize(value);
        });

        var entityService = Substitute.For<IEntityService>();
        // Exists() drives the idempotency guard: false = 'not seeded yet, go ahead'.
        entityService
            .Exists(CookieBannerKeys.Nodes.CookiePolicy, UmbracoObjectTypes.Document)
            .Returns(alreadySeeded);

        var logger = new RecordingLogger<CookieBannerContentSeeder>();
        var seeder = new CookieBannerContentSeeder(
            contentService,
            contentTypeService,
            entityService,
            jsonSerializer,
            Options.Create(new CookieBannerOptions { CookieName = cookieName, CookieLifetimeDays = 365 }),
            logger);

        return new Harness
        {
            ContentService = contentService,
            ContentTypeService = contentTypeService,
            EntityService = entityService,
            Policy = policy,
            Registry = () => registry,
            Logger = logger,
            Seeder = seeder,
        };
    }

    private static string?[] CookieNames(BlockListValue registry)
        => registry.ContentData
            .SelectMany(block => block.Values)
            .Where(value => value.Alias == "cookieName")
            .Select(value => value.Value as string)
            .ToArray();

    [Fact]
    public void Declares_the_consent_cookie_under_its_configured_name()
    {
        // Pins that the seeded registry reads CookieName from options rather than hardcoding a
        // site's cookie: NDSTK's seeder wrote the literal "ndstk-consent", and a package that
        // ships a policy page naming the wrong cookie publishes a false legal declaration.
        Harness harness = CreateSut("site-consent");

        harness.Seeder.EnsurePolicyPage();

        BlockListValue? registry = harness.Registry();
        Assert.NotNull(registry);
        Assert.Equal(
            new[] { "site-consent", ".AspNetCore.Antiforgery.*", "UMB_MEMBER" },
            CookieNames(registry!));
    }

    [Fact]
    public void Declares_every_seeded_cookie_as_a_necessary_browser_cookie()
    {
        // Pins the category/storageType of the three generic declarations. These three are set
        // before any consent exists, so any category other than necessary would make the page
        // contradict what the banner actually does.
        Harness harness = CreateSut("site-consent");

        harness.Seeder.EnsurePolicyPage();

        BlockListValue? registry = harness.Registry();
        Assert.NotNull(registry);
        Assert.Equal(3, registry!.ContentData.Count);
        Assert.All(registry.ContentData, block =>
        {
            Assert.Equal(
                CookieBannerKeys.ElementTypes.CookieDefinition,
                block.ContentTypeKey);
            Assert.Contains(
                block.Values,
                value => value.Alias == "category" && (value.Value as string) == "[\"necessary\"]");
            Assert.Contains(
                block.Values,
                value => value.Alias == "storageType" && (value.Value as string) == "[\"Cookie\"]");
        });
    }

    [Fact]
    public void Does_not_add_a_second_policy_page_when_the_site_already_has_one()
    {
        // Pins idempotency across the by-type guard, which is what protects a consuming site that
        // seeds its OWN localised policy page (the NDSTK migration path) from getting a second,
        // English one bolted on at every boot.
        Harness harness = CreateSut("site-consent", alreadySeeded: false, Substitute.For<IContent>());

        harness.Seeder.EnsurePolicyPage();

        // Cast disambiguates the overload: IContentService.Create has both a (string, int, string,
        // int) and a (string, int, IContentType, int) overload, and DidNotReceiveWithAnyArgs
        // ignores the actual argument values regardless of which one is picked here.
        harness.ContentService.DidNotReceiveWithAnyArgs().Create(default!, default(int), default(string)!, default(int));
        harness.ContentService.DidNotReceiveWithAnyArgs().Publish(default!, default!, default);
    }

    [Fact]
    public void Does_nothing_when_the_seeded_node_already_exists()
    {
        // Pins the key-based idempotency guard, which is the one that makes a SECOND BOOT a no-op
        // (Task 17's manual check asserts exactly this). It also pins the API choice: the guard
        // must go through IEntityService.Exists, because neither GetById(Guid) overload exists on
        // both Umbraco.Cms.Core 17.0.0 and 18.1.1 - see the comment in CookieBannerContentSeeder.
        Harness harness = CreateSut("site-consent", alreadySeeded: true);

        harness.Seeder.EnsurePolicyPage();

        harness.EntityService
            .Received(1)
            .Exists(CookieBannerKeys.Nodes.CookiePolicy, UmbracoObjectTypes.Document);
        // Cast disambiguates the overload: IContentService.Create has both a (string, int, string,
        // int) and a (string, int, IContentType, int) overload, and DidNotReceiveWithAnyArgs
        // ignores the actual argument values regardless of which one is picked here.
        harness.ContentService.DidNotReceiveWithAnyArgs().Create(default!, default(int), default(string)!, default(int));
        harness.ContentService.DidNotReceiveWithAnyArgs().Save(default!, default(int?), default);
        harness.ContentService.DidNotReceiveWithAnyArgs().Publish(default!, default!, default);
    }

    [Fact]
    public void Logs_at_Error_when_the_created_page_cannot_be_published()
    {
        // Pins the fix: once Create/Save succeed, the node exists under the fixed key, so
        // entityService.Exists(...) makes every LATER boot return before Publish is ever retried -
        // a failed publish here otherwise strands the page as an invisible draft forever with only
        // a log line. That demands an operator's attention, so it must be Error, not Warning.
        Harness harness = CreateSut("site-consent");
        harness.ContentService
            .Publish(Arg.Any<IContent>(), Arg.Any<string[]>(), Arg.Any<int>())
            .Returns(new PublishResult(PublishResultType.FailedPublish, new EventMessages(), harness.Policy));

        harness.Seeder.EnsurePolicyPage();

        (LogLevel Level, string Message) entry = Assert.Single(harness.Logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("NOT be retried", entry.Message, StringComparison.Ordinal);
    }
}
