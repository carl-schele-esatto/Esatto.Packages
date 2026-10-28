using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class SettingsServiceTests
{
    private static (SettingsService Service, IAccessibilityStore Store) Create(string? storedJson = null, Dictionary<string, string?>? config = null)
    {
        // Behaves like the real table: a save is what the next read returns.
        var current = storedJson;
        var store = Substitute.For<IAccessibilityStore>();
        store.GetSettingsJsonAsync().Returns(_ => current);
        store.When(s => s.SaveSettingsJsonAsync(Arg.Any<string>())).Do(call => current = call.Arg<string>());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config ?? new()).Build();
        return (new SettingsService(store, configuration, TimeProvider.System), store);
    }

    [Fact]
    public async Task Defaults_when_nothing_stored_or_configured()
    {
        var effective = await Create().Service.GetAsync();
        var s = effective.Settings;

        Assert.Equal(WcagVersion.V22, s.Version);
        Assert.Equal(WcagLevel.AA, s.Level);
        Assert.Equal(new WcagTarget(WcagVersion.V22, WcagLevel.AA), effective.Target);
        Assert.True(s.Visitor.FollowOsMotion);
        Assert.False(s.Visitor.FollowOsContrast);
        Assert.False(s.Visitor.Panel.Enabled);
        Assert.Equal("bottom-right", s.Visitor.Panel.Position);
        Assert.True(s.Visitor.Panel.LargerText && s.Visitor.Panel.Spacing && s.Visitor.Panel.HighContrast
            && s.Visitor.Panel.ReduceMotion && s.Visitor.Panel.ReadableFont && s.Visitor.Panel.HighlightLinks);
        Assert.False(s.Visitor.Panel.ReadingGuide);
        Assert.Empty(s.Scan.ExcludePaths);
        Assert.False(s.Scan.MobileWidth);
        Assert.False(s.Scan.BestPractice);
        Assert.Equal(6, s.Scan.RecheckMonths);
        Assert.Empty(effective.Locked);
    }

    [Fact]
    public async Task Stored_settings_are_used()
    {
        var effective = await Create("""{"version":"V21","level":"AAA"}""").Service.GetAsync();
        Assert.Equal(new WcagTarget(WcagVersion.V21, WcagLevel.AAA), effective.Target);
    }

    [Fact]
    public async Task Config_value_wins_and_is_locked()
    {
        var effective = await Create(
            """{"visitor":{"panel":{"enabled":false}}}""",
            new() { ["Esatto:Accessibility:Visitor:Panel:Enabled"] = "true" }).Service.GetAsync();

        Assert.True(effective.Settings.Visitor.Panel.Enabled);
        Assert.Contains("visitor.panel.enabled", effective.Locked);
    }

    [Fact]
    public async Task Save_cannot_override_locked_value()
    {
        var (service, _) = Create(config: new() { ["Esatto:Accessibility:Visitor:Panel:Enabled"] = "true" });
        var incoming = new AccessibilitySettings();
        incoming.Visitor.Panel.Enabled = false;

        var effective = await service.SaveAsync(incoming);

        Assert.True(effective.Settings.Visitor.Panel.Enabled);
    }

    [Fact]
    public async Task Save_stores_and_returns_unlocked_values()
    {
        var (service, store) = Create();
        var incoming = new AccessibilitySettings { Level = WcagLevel.AAA };

        var effective = await service.SaveAsync(incoming);

        Assert.Equal(WcagLevel.AAA, effective.Settings.Level);
        await store.Received(1).SaveSettingsJsonAsync(Arg.Is<string>(json => json.Contains("\"AAA\"")));
    }

    [Fact]
    public async Task Config_array_replaces_database_array()
    {
        var effective = await Create(
            """{"scan":{"excludePaths":["/a"]}}""",
            new() { ["Esatto:Accessibility:Scan:ExcludePaths:0"] = "/b" }).Service.GetAsync();

        Assert.Equal(new[] { "/b" }, effective.Settings.Scan.ExcludePaths);
        Assert.Contains("scan.excludePaths", effective.Locked);
    }

    [Fact]
    public async Task Unknown_or_corrupt_json_falls_back_to_defaults()
    {
        var effective = await Create("{not json").Service.GetAsync();
        Assert.Equal(WcagTarget.Default, effective.Target);
    }

    [Fact]
    public async Task Recheck_months_is_clamped_on_save()
    {
        var (service, _) = Create();
        var incoming = new AccessibilitySettings();
        incoming.Scan.RecheckMonths = 99;

        Assert.Equal(24, (await service.SaveAsync(incoming)).Settings.Scan.RecheckMonths);
    }
}
