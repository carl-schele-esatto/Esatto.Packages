using Esatto.Umbraco.Backoffice.Seo.Pages;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Settings;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class SettingsServiceTests
{
    private static SettingsService Create(string? storedJson = null, Dictionary<string, string?>? config = null)
    {
        var current = storedJson;
        var store = Substitute.For<ISeoStore>();
        store.GetSettingsJsonAsync().Returns(_ => current);
        store.When(s => s.SaveSettingsJsonAsync(Arg.Any<string>())).Do(call => current = call.Arg<string>());
        return new SettingsService(store, new ConfigurationBuilder().AddInMemoryCollection(config ?? new()).Build(), TimeProvider.System);
    }

    [Fact]
    public async Task Defaults()
    {
        var e = await Create().GetAsync();
        Assert.True(e.Settings.Schedule.Enabled);
        Assert.Equal(3, e.Settings.Schedule.Hour);
        Assert.Equal(2, e.Settings.Parallelism);
        Assert.Equal(0, e.Settings.DelayMs);
        Assert.Empty(e.Settings.ExcludePaths);
        Assert.Null(e.Settings.BaseUrl);
        Assert.Empty(e.Locked);
    }

    [Fact]
    public async Task Config_wins_and_is_locked()
    {
        var e = await Create("""{"parallelism":4}""", new() { ["Esatto:Seo:Schedule:Enabled"] = "false", ["Esatto:Seo:Parallelism"] = "3" }).GetAsync();
        Assert.False(e.Settings.Schedule.Enabled);
        Assert.Equal(3, e.Settings.Parallelism);
        Assert.Equal(["parallelism", "schedule.enabled"], e.Locked.Order());
    }

    [Fact]
    public async Task Config_only_keys_are_not_locked_settings()
    {
        var e = await Create(config: new() { ["Esatto:Seo:IntentionallyHidden"] = "true", ["Esatto:Seo:RequestHeaders:X-A"] = "1" }).GetAsync();
        Assert.Empty(e.Locked);
    }

    [Fact]
    public async Task Save_clamps_values_and_round_trips()
    {
        var service = Create();
        var saved = await service.SaveAsync(new SeoSettings
        {
            Parallelism = 50, DelayMs = -5, Schedule = new ScheduleSettings { Hour = 25 },
            ExcludePaths = [" /sok ", "", "/SOK"], BaseUrl = "  https://esatto.se  ",
        });
        Assert.Equal(8, saved.Settings.Parallelism);
        Assert.Equal(0, saved.Settings.DelayMs);
        Assert.Equal(23, saved.Settings.Schedule.Hour);
        Assert.Equal(["/sok"], saved.Settings.ExcludePaths);
        Assert.Equal("https://esatto.se", saved.Settings.BaseUrl);
        Assert.Equal(8, (await service.GetAsync()).Settings.Parallelism);
    }

    [Fact]
    public async Task Delay_is_clamped_to_5000() => Assert.Equal(5000, (await Create().SaveAsync(new SeoSettings { DelayMs = 9999 })).Settings.DelayMs);

    [Fact]
    public async Task Corrupt_json_falls_back_to_defaults() => Assert.Equal(2, (await Create("{nope").GetAsync()).Settings.Parallelism);
}

public class SeoEnvironmentTests
{
    private static SeoEnvironment Env(Dictionary<string, string?> config) => new(new ConfigurationBuilder().AddInMemoryCollection(config).Build());

    [Fact]
    public void Defaults_are_off_and_empty()
    {
        var env = Env([]);
        Assert.False(env.IntentionallyHidden);
        Assert.Empty(env.RequestHeaders);
    }

    [Fact]
    public void Intentionally_hidden_read_from_config() => Assert.True(Env(new() { ["Esatto:Seo:IntentionallyHidden"] = "true" }).IntentionallyHidden);

    [Fact]
    public void Request_headers_include_custom_and_basic_auth_shared_secret()
    {
        var env = Env(new()
        {
            ["Esatto:Seo:RequestHeaders:X-Bypass"] = "abc",
            ["Umbraco:CMS:BasicAuth:SharedSecret:Value"] = "s3cret",
        });
        Assert.Equal("abc", env.RequestHeaders["X-Bypass"]);
        Assert.Equal("s3cret", env.RequestHeaders["X-Authentication-Shared-Secret"]);
    }

    [Fact]
    public void Basic_auth_header_name_can_be_configured()
        => Assert.Equal("v", Env(new() { ["Umbraco:CMS:BasicAuth:SharedSecret:HeaderName"] = "X-My", ["Umbraco:CMS:BasicAuth:SharedSecret:Value"] = "v" }).RequestHeaders["X-My"]);
}

public class PageListBuilderTests
{
    private static PageCandidate Page(string? url, bool template = true, bool isProtected = false)
        => new(Guid.NewGuid(), "Page", "sv", url, isProtected, template, false, false, []);

    [Fact]
    public void No_template_dropped()
        => Assert.Equal(["https://a.se/y"], PageListBuilder.Build([Page("https://a.se/x", template: false), Page("https://a.se/y")], [], null).Select(p => p.Ref.Url));

    [Fact]
    public void Excluded_prefix_whole_segments_only()
    {
        var pages = PageListBuilder.Build([Page("https://a.se/Sok/x"), Page("https://a.se/sokresultat")], ["/sok/"], null);
        Assert.Equal(SkipReasons.Excluded, pages[0].SkipReason);
        Assert.Null(pages[1].SkipReason);
    }

    [Fact]
    public void Protected_skipped() => Assert.Equal(SkipReasons.Protected, PageListBuilder.Build([Page("https://a.se/x", isProtected: true)], [], null)[0].SkipReason);

    [Fact]
    public void Relative_url_made_absolute_with_base()
    {
        var page = PageListBuilder.Build([Page("/om/")], [], new Uri("https://esatto.se"))[0];
        Assert.Null(page.SkipReason);
        Assert.Equal("https://esatto.se/om/", page.Ref.Url);
    }

    [Theory]
    [InlineData("/om/")]
    [InlineData("#")]
    [InlineData(null)]
    public void No_usable_url_without_base(string? url) => Assert.Equal(SkipReasons.NoUrl, PageListBuilder.Build([Page(url)], [], null)[0].SkipReason);
}

public class BaseUrlResolverTests
{
    [Fact]
    public void Override_wins() => Assert.Equal("https://o.se/", BaseUrlResolver.Resolve("https://o.se", "https://app.se", new Uri("https://req.se"))!.ToString());

    [Fact]
    public void Application_url_next() => Assert.Equal("https://app.se/", BaseUrlResolver.Resolve(" ", "https://app.se", new Uri("https://req.se"))!.ToString());

    [Fact]
    public void Request_last() => Assert.Equal("https://req.se/", BaseUrlResolver.Resolve(null, "not a url", new Uri("https://req.se"))!.ToString());

    [Fact]
    public void Scheduled_with_no_base_returns_null() => Assert.Null(BaseUrlResolver.Resolve(null, null, null));
}

public class BoundDomainsTests
{
    // "/" and "/en" are Culture and Hostnames entries that bind a path only. Umbraco takes their host from the current
    // request, and a background scan has none, so absolute URLs for them come out as http://localhost/... (seen on Nova
    // local: 180 pages, every one at http://localhost). Such a site must be scanned like one without a domain.
    [Theory]
    [InlineData("/", false)]
    [InlineData("/en", false)]
    [InlineData(" /en", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("esatto.se", true)]
    [InlineData("esatto.se/en", true)]
    [InlineData("https://esatto.se/en", true)]
    [InlineData("localhost:44324", true)]
    public void Path_only_bindings_name_no_host(string? domainName, bool expected) => Assert.Equal(expected, BoundDomains.NamesHost(domainName));
}
