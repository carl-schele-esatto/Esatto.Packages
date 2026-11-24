using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The settings: database values with configuration on top, and the service-account key, which is stored encrypted, read
/// from configuration when it is set there, and never returned, logged or put in a message.
/// </summary>
public class SettingsServiceTests
{
    private const string Marker = FakeServiceAccount.LeakMarker;

    /// <summary>A stretch of the key's base64 body, which would only appear in a text that printed the key.</summary>
    private static string PemFragment => FakeServiceAccount.PrivateKeyPem.Split('\n')[1][..30];

    /// <summary>A key whose <c>private_key_id</c> and <c>project_id</c> carry the leak marker (the email is public by design).</summary>
    private static string KeyWithMarker(string email = FakeServiceAccount.DefaultClientEmail)
    {
        var key = FakeServiceAccount.Object(email);
        key["private_key_id"] = Marker;
        key["project_id"] = Marker;
        return key.ToJsonString();
    }

    private static string Key(string email) => FakeServiceAccount.Json(email);

    private static void ExpireCache(SettingsHarness h) => h.Time.Advance(TimeSpan.FromMinutes(2));

    // ---- Database values and defaults ------------------------------------------------------------------------

    [Fact]
    public async Task Without_anything_stored_the_defaults_apply()
    {
        var h = new SettingsHarness();

        var effective = await h.Settings.GetAsync();

        Assert.Null(effective.Settings.Ga4PropertyId);
        Assert.Null(effective.Settings.SearchConsoleSite);
        Assert.Empty(effective.Settings.BrandTerms);
        Assert.Null(effective.Settings.DisplayName);
        Assert.Empty(effective.Settings.FindingsUserGroups);
        Assert.True(effective.Settings.AutoFetch);
        Assert.Empty(effective.Locked);
        Assert.Equal(new KeyStatus(false, KeySourceKind.None, null, false), effective.Key);
    }

    [Fact]
    public async Task Database_values_are_used_when_there_is_no_configuration()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new
        {
            ga4PropertyId = "properties/1",
            searchConsoleSite = "sc-domain:example.com",
            brandTerms = new[] { "acme", "acme ab" },
            displayName = "Acme",
            findingsUserGroups = new[] { "editors" },
            autoFetch = false,
        });

        var effective = await h.Settings.GetAsync();

        Assert.Equal("properties/1", effective.Settings.Ga4PropertyId);
        Assert.Equal("sc-domain:example.com", effective.Settings.SearchConsoleSite);
        Assert.Equal(["acme", "acme ab"], effective.Settings.BrandTerms);
        Assert.Equal("Acme", effective.Settings.DisplayName);
        Assert.Equal(["editors"], effective.Settings.FindingsUserGroups);
        Assert.False(effective.Settings.AutoFetch);
        Assert.Empty(effective.Locked);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    [InlineData("{\"autoFetch\":\"lots\"}")]
    [InlineData("")]
    public async Task A_corrupt_settings_document_gives_the_defaults_and_does_not_throw(string json)
    {
        var h = new SettingsHarness();
        await h.Store.SaveDocumentAsync(DocumentKeys.Settings, json);

        var effective = await h.Settings.GetAsync();

        Assert.True(effective.Settings.AutoFetch);
        Assert.Null(effective.Settings.Ga4PropertyId);
        Assert.Empty(effective.Settings.BrandTerms);
    }

    [Fact]
    public async Task Hand_edited_values_in_the_database_are_cleaned_when_read()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new
        {
            ga4PropertyId = "garbage",
            searchConsoleSite = "has spaces.com",
            brandTerms = new[] { " a ", "", "A", "b" },
            displayName = "   ",
            findingsUserGroups = new[] { "Editors", "editors", " " },
        });

        var s = (await h.Settings.GetAsync()).Settings;

        Assert.Null(s.Ga4PropertyId);
        Assert.Null(s.SearchConsoleSite);
        Assert.Equal(["a", "b"], s.BrandTerms);
        Assert.Null(s.DisplayName);
        Assert.Equal(["Editors"], s.FindingsUserGroups);
    }

    [Fact]
    public async Task Brand_terms_groups_and_the_display_name_are_capped_when_saved_silently_and_in_order()
    {
        var h = new SettingsHarness();
        var terms = Enumerable.Range(0, 60).Select(i => $"brand {i:D2}").Prepend(new string('x', 101)).ToArray();
        var groups = Enumerable.Range(0, 60).Select(i => $"group{i:D2}").ToArray();

        var saved = (await h.Settings.SaveAsync(
            new SeoReportSettings { BrandTerms = terms, FindingsUserGroups = groups, DisplayName = new string('d', 250) }, null, false)).Settings;

        Assert.Equal(terms[1..51], saved.BrandTerms);
        Assert.Equal(groups[..50], saved.FindingsUserGroups);
        Assert.Equal(new string('d', 200), saved.DisplayName);
        var stored = (await h.StoredSettingsAsync())!;
        Assert.Equal(terms[1..51], stored.BrandTerms);
        Assert.Equal(groups[..50], stored.FindingsUserGroups);
        Assert.Equal(new string('d', 200), stored.DisplayName);
    }

    [Fact]
    public async Task A_group_alias_longer_than_a_brand_term_may_be_is_kept_when_saved_and_when_configured()
    {
        var alias = new string('g', 180);

        var saved = (await new SettingsHarness().Settings.SaveAsync(new SeoReportSettings { FindingsUserGroups = [alias] }, null, false)).Settings;

        var configured = new SettingsHarness();
        configured.Configure("FindingsUserGroups:0", alias);
        var effective = (await configured.Settings.GetAsync()).Settings;

        Assert.Equal([alias], saved.FindingsUserGroups);
        Assert.Equal([alias], effective.FindingsUserGroups);
    }

    [Fact]
    public async Task Configured_lists_and_a_configured_display_name_are_capped_too()
    {
        var h = new SettingsHarness();
        for (var i = 0; i < 60; i++) h.Configure($"BrandTerms:{i}", $"brand {i:D2}");
        h.Configure("DisplayName", new string('d', 250));

        var effective = (await h.Settings.GetAsync()).Settings;

        Assert.Equal(50, effective.BrandTerms.Length);
        Assert.Equal("brand 49", effective.BrandTerms[^1]);
        Assert.Equal(new string('d', 200), effective.DisplayName);
    }

    [Fact]
    public async Task A_stored_Search_Console_property_always_reads_back_because_one_that_would_not_is_refused_when_saved()
    {
        var h = new SettingsHarness();

        var refused = await Assert.ThrowsAsync<SettingsValidationException>(
            () => h.Settings.SaveAsync(new SeoReportSettings { SearchConsoleSite = "https://example.com/" + new string('a', 2028) }, null, false));

        Assert.Equal("searchConsoleInvalid", refused.Code);
    }

    [Fact]
    public void The_settings_type_carries_the_six_fields_and_no_secret()
    {
        var properties = typeof(SeoReportSettings).GetProperties().Select(p => p.Name).Order().ToArray();

        Assert.Equal(["AutoFetch", "BrandTerms", "DisplayName", "FindingsUserGroups", "Ga4PropertyId", "SearchConsoleSite"], properties);
        Assert.Equal("Esatto:SeoReport", SeoReportSettings.SectionName);
    }

    [Fact]
    public void The_key_status_carries_nothing_but_the_four_documented_values()
    {
        // KeyStatus is a record, so its generated ToString prints every property: a property holding the key would leak it.
        var properties = typeof(KeyStatus).GetProperties().Select(p => p.Name).Order().ToArray();

        Assert.Equal(["HasKey", "ServiceAccountEmail", "Source", "Unreadable"], properties);
    }

    // ---- Configuration wins and locks ------------------------------------------------------------------------

    [Fact]
    public async Task A_configuration_value_wins_over_the_database_and_is_listed_as_locked()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new { ga4PropertyId = "properties/1", displayName = "From database" });
        h.Configure("Ga4PropertyId", "222");

        var effective = await h.Settings.GetAsync();

        Assert.Equal("properties/222", effective.Settings.Ga4PropertyId);
        Assert.Equal("From database", effective.Settings.DisplayName);
        Assert.Equal(["ga4PropertyId"], effective.Locked);
    }

    [Fact]
    public async Task Every_field_can_be_locked_and_is_listed_by_its_camelCase_name_in_the_documented_order()
    {
        var h = new SettingsHarness();
        h.Configure("Ga4PropertyId", "123");
        h.Configure("SearchConsoleSite", "Example.com");
        h.Configure("BrandTerms:0", "acme");
        h.Configure("BrandTerms:1", "acme ab");
        h.Configure("DisplayName", "Acme");
        h.Configure("FindingsUserGroups:0", "editors");
        h.Configure("AutoFetch", "false");

        var effective = await h.Settings.GetAsync();

        Assert.Equal(["ga4PropertyId", "searchConsoleSite", "brandTerms", "displayName", "findingsUserGroups", "autoFetch"], effective.Locked);
        Assert.Equal("properties/123", effective.Settings.Ga4PropertyId);
        Assert.Equal("sc-domain:example.com", effective.Settings.SearchConsoleSite);
        Assert.Equal(["acme", "acme ab"], effective.Settings.BrandTerms);
        Assert.Equal("Acme", effective.Settings.DisplayName);
        Assert.Equal(["editors"], effective.Settings.FindingsUserGroups);
        Assert.False(effective.Settings.AutoFetch);
    }

    [Fact]
    public async Task A_configured_list_replaces_the_stored_list_instead_of_adding_to_it_and_keeps_its_order()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new { brandTerms = new[] { "stored1", "stored2", "stored3" } });
        var configured = Enumerable.Range(0, 12).Select(i => $"term{i:00}").ToArray();
        for (var i = 0; i < configured.Length; i++) h.Configure($"BrandTerms:{i}", configured[i]);

        var effective = await h.Settings.GetAsync();

        Assert.Equal(configured, effective.Settings.BrandTerms);
        Assert.Contains("brandTerms", effective.Locked);
    }

    [Fact]
    public async Task A_blank_configuration_value_leaves_the_field_alone_and_unlocked()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new { displayName = "From database", ga4PropertyId = "properties/9" });
        h.Configure("DisplayName", "   ");
        h.Configure("Ga4PropertyId", "");
        h.Configure("AutoFetch", "");

        var effective = await h.Settings.GetAsync();

        Assert.Equal("From database", effective.Settings.DisplayName);
        Assert.Equal("properties/9", effective.Settings.Ga4PropertyId);
        Assert.True(effective.Settings.AutoFetch);
        Assert.Empty(effective.Locked);
    }

    [Fact]
    public async Task A_bad_configuration_value_is_ignored_and_the_valid_ones_still_apply()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new { autoFetch = false });
        h.Configure("AutoFetch", "lots");
        h.Configure("DisplayName", "Acme");

        var effective = await h.Settings.GetAsync();

        Assert.False(effective.Settings.AutoFetch);   // the database value stands
        Assert.Equal("Acme", effective.Settings.DisplayName);
        Assert.Equal(["displayName"], effective.Locked);
        Assert.Contains(h.Log.Entries, e => e.Level == LogLevel.Warning && e.Text.Contains("AutoFetch"));
    }

    [Fact]
    public async Task A_bad_configuration_value_is_logged_by_name_and_never_by_value()
    {
        var h = new SettingsHarness();
        h.Configure("AutoFetch", Marker);
        h.Configure("Ga4PropertyId", Marker);

        await h.Settings.GetAsync();

        Assert.NotEmpty(h.Log.Entries);
        Assert.DoesNotContain(Marker, h.Log.AllText);
    }

    [Theory]
    [InlineData("Ga4PropertyId", "abc", "ga4PropertyId")]
    [InlineData("SearchConsoleSite", "has spaces.com", "searchConsoleSite")]
    public async Task A_configured_id_that_does_not_normalize_is_ignored_and_does_not_lock_the_field(string name, string value, string lockedName)
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new { ga4PropertyId = "properties/9", searchConsoleSite = "sc-domain:stored.com" });
        h.Configure(name, value);

        var effective = await h.Settings.GetAsync();

        Assert.Equal("properties/9", effective.Settings.Ga4PropertyId);
        Assert.Equal("sc-domain:stored.com", effective.Settings.SearchConsoleSite);
        Assert.DoesNotContain(lockedName, effective.Locked);
        Assert.Contains(h.Log.Entries, e => e.Level == LogLevel.Warning && e.Text.Contains(name));
    }

    [Fact]
    public async Task A_scalar_configured_for_a_list_is_ignored_and_does_not_lock_it()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new { brandTerms = new[] { "stored" } });
        h.Configure("BrandTerms", "acme");

        var effective = await h.Settings.GetAsync();

        Assert.Equal(["stored"], effective.Settings.BrandTerms);
        Assert.DoesNotContain("brandTerms", effective.Locked);
    }

    [Fact]
    public async Task The_key_settings_and_unknown_keys_are_never_listed_as_locked_and_never_become_settings()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountJson", KeyWithMarker("cfg@proj.iam.gserviceaccount.com"));
        h.Configure("ServiceAccountKeyFile", "C:/nowhere/key.json");
        h.Configure("SomethingElse", "x");

        var effective = await h.Settings.GetAsync();

        Assert.True(effective.Key.HasKey);   // the configured key (which carries the marker) really is in force
        Assert.Empty(effective.Locked);
        Assert.DoesNotContain(Marker, JsonSerializer.Serialize(effective.Settings));
        Assert.DoesNotContain(Marker, JsonSerializer.Serialize(effective));
    }

    // ---- Cache and generation --------------------------------------------------------------------------------

    [Fact]
    public async Task The_result_is_cached_for_a_minute_and_read_again_after_it()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new { displayName = "One" });
        var first = await h.Settings.GetAsync();

        await h.SeedSettingsAsync(new { displayName = "Two" });   // behind the service's back
        h.Time.Advance(TimeSpan.FromSeconds(59));
        var cached = await h.Settings.GetAsync();

        h.Time.Advance(TimeSpan.FromSeconds(2));
        var fresh = await h.Settings.GetAsync();

        Assert.Same(first, cached);
        Assert.Equal("One", cached.Settings.DisplayName);
        Assert.Equal("Two", fresh.Settings.DisplayName);
    }

    [Fact]
    public async Task A_configuration_change_is_picked_up_when_the_cache_expires()
    {
        var h = new SettingsHarness();
        h.Configure("DisplayName", "Before");
        Assert.Equal("Before", (await h.Settings.GetAsync()).Settings.DisplayName);

        h.Configure("DisplayName", "After");
        Assert.Equal("Before", (await h.Settings.GetAsync()).Settings.DisplayName);

        ExpireCache(h);

        Assert.Equal("After", (await h.Settings.GetAsync()).Settings.DisplayName);
    }

    [Fact]
    public async Task Saving_makes_the_new_values_visible_at_once()
    {
        var h = new SettingsHarness();
        Assert.Null((await h.Settings.GetAsync()).Settings.DisplayName);

        var saved = await h.Settings.SaveAsync(new SeoReportSettings { DisplayName = "Acme" }, null, false);

        Assert.Equal("Acme", saved.Settings.DisplayName);
        Assert.Equal("Acme", (await h.Settings.GetAsync()).Settings.DisplayName);
    }

    // ---- SaveAsync: normalization, validation, locks ---------------------------------------------------------

    [Fact]
    public async Task Saving_normalizes_the_values_stores_them_and_leaves_the_incoming_object_alone()
    {
        var h = new SettingsHarness();
        var incoming = new SeoReportSettings
        {
            Ga4PropertyId = " 123 ",
            SearchConsoleSite = "Example.com/",
            BrandTerms = ["acme", " acme ", "", "Other"],
            DisplayName = "  ",
            FindingsUserGroups = ["Editors", "editors", " ", "Admins"],
            AutoFetch = false,
        };

        var saved = await h.Settings.SaveAsync(incoming, null, false);
        var stored = await h.StoredSettingsAsync();

        foreach (var s in new[] { saved.Settings, stored! })
        {
            Assert.Equal("properties/123", s.Ga4PropertyId);
            Assert.Equal("sc-domain:example.com", s.SearchConsoleSite);
            Assert.Equal(["acme", "Other"], s.BrandTerms);
            Assert.Null(s.DisplayName);
            Assert.Equal(["Editors", "Admins"], s.FindingsUserGroups);
            Assert.False(s.AutoFetch);
        }

        Assert.Equal(" 123 ", incoming.Ga4PropertyId);
        Assert.Equal(4, incoming.BrandTerms.Length);
    }

    [Fact]
    public async Task Saving_a_blank_id_clears_it()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new { ga4PropertyId = "properties/1", searchConsoleSite = "sc-domain:example.com" });

        var saved = await h.Settings.SaveAsync(new SeoReportSettings { Ga4PropertyId = "  ", SearchConsoleSite = null }, null, false);

        Assert.Null(saved.Settings.Ga4PropertyId);
        Assert.Null(saved.Settings.SearchConsoleSite);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("properties/")]
    [InlineData("properties/12a")]
    [InlineData("properties/properties/1")]
    public async Task An_invalid_GA4_id_is_refused_with_its_code_and_nothing_is_saved(string id)
    {
        var h = new SettingsHarness();

        var ex = await Assert.ThrowsAsync<SettingsValidationException>(
            () => h.Settings.SaveAsync(new SeoReportSettings { Ga4PropertyId = id }, Key("svc@proj.iam.gserviceaccount.com"), false));

        Assert.Equal("ga4PropertyInvalid", ex.Code);
        Assert.Equal("ga4PropertyInvalid", ex.Message);
        Assert.Empty(h.Store.Documents);
    }

    [Theory]
    [InlineData("has spaces.com")]
    [InlineData("ftp://x")]
    [InlineData("sc-domain:")]
    [InlineData("example.com/path")]
    public async Task An_invalid_Search_Console_property_is_refused_with_its_code_and_nothing_is_saved(string site)
    {
        var h = new SettingsHarness();

        var ex = await Assert.ThrowsAsync<SettingsValidationException>(
            () => h.Settings.SaveAsync(new SeoReportSettings { SearchConsoleSite = site }, Key("svc@proj.iam.gserviceaccount.com"), false));

        Assert.Equal("searchConsoleInvalid", ex.Code);
        Assert.Empty(h.Store.Documents);
    }

    [Fact]
    public async Task The_ids_are_checked_before_the_key_and_the_GA4_id_before_the_Search_Console_property()
    {
        var h = new SettingsHarness();

        var both = await Assert.ThrowsAsync<SettingsValidationException>(
            () => h.Settings.SaveAsync(new SeoReportSettings { Ga4PropertyId = "x", SearchConsoleSite = "y y" }, "not a key", false));
        var siteAndKey = await Assert.ThrowsAsync<SettingsValidationException>(
            () => h.Settings.SaveAsync(new SeoReportSettings { SearchConsoleSite = "y y" }, "not a key", false));

        Assert.Equal("ga4PropertyInvalid", both.Code);
        Assert.Equal("searchConsoleInvalid", siteAndKey.Code);
    }

    [Fact]
    public async Task A_locked_field_is_ignored_when_saving_and_the_others_are_still_saved()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new { ga4PropertyId = "properties/1", displayName = "Old" });
        h.Configure("Ga4PropertyId", "222");

        var saved = await h.Settings.SaveAsync(
            new SeoReportSettings { Ga4PropertyId = "999", DisplayName = "New", BrandTerms = ["acme"] }, null, false);
        var stored = await h.StoredSettingsAsync();

        Assert.Equal("properties/1", stored!.Ga4PropertyId);   // the locked value was not persisted: the old one is still there
        Assert.Equal("New", stored.DisplayName);
        Assert.Equal(["acme"], stored.BrandTerms);
        Assert.Equal("properties/222", saved.Settings.Ga4PropertyId);
        Assert.Equal("New", saved.Settings.DisplayName);
        Assert.Equal(["ga4PropertyId"], saved.Locked);
    }

    [Fact]
    public async Task An_invalid_value_for_a_locked_field_is_ignored_not_refused()
    {
        var h = new SettingsHarness();
        h.Configure("Ga4PropertyId", "222");

        var saved = await h.Settings.SaveAsync(new SeoReportSettings { Ga4PropertyId = "abc" }, null, false);

        Assert.Equal("properties/222", saved.Settings.Ga4PropertyId);
    }

    [Fact]
    public async Task Once_the_configuration_is_removed_the_stored_value_of_a_locked_field_is_back()
    {
        var h = new SettingsHarness();
        await h.SeedSettingsAsync(new { displayName = "Stored" });
        h.Configure("DisplayName", "Configured");
        await h.Settings.SaveAsync(new SeoReportSettings { DisplayName = "Typed" }, null, false);

        h.Configure("DisplayName", null);
        ExpireCache(h);

        var effective = await h.Settings.GetAsync();
        Assert.Equal("Stored", effective.Settings.DisplayName);
        Assert.Empty(effective.Locked);
    }

    // ---- The key: storage ------------------------------------------------------------------------------------

    [Fact]
    public async Task Saving_a_key_stores_only_ciphertext()
    {
        var h = new SettingsHarness();

        var saved = await h.SaveKeyAsync(KeyWithMarker());

        Assert.Equal(new KeyStatus(true, KeySourceKind.Database, FakeServiceAccount.DefaultClientEmail, false), saved.Key);
        Assert.Contains(DocumentKeys.ProtectedKey, h.Store.Documents.Keys);
        foreach (var (name, value) in h.Store.Documents)
        {
            Assert.DoesNotContain(Marker, value);
            Assert.DoesNotContain("PRIVATE KEY", value);
            Assert.DoesNotContain(PemFragment, value);
            Assert.DoesNotContain(FakeServiceAccount.DefaultClientEmail, value);
            Assert.NotEmpty(name);
        }
    }

    /// <summary>
    /// Pastes that need repairing, by name (so the per-process random key stays out of the test ids). The stored key must be the
    /// normalized one, never what was pasted.
    /// </summary>
    public static TheoryData<string> Pastes_that_need_normalizing() => new()
    {
        "a BOM, CRLF line endings and indentation",
        "the private key's newlines pasted as backslash-n",
    };

    private static string MarkedPaste(string shape)
    {
        if (shape.StartsWith("a BOM", StringComparison.Ordinal))
        {
            var key = FakeServiceAccount.Object();
            key["private_key_id"] = Marker;
            var indented = key.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\r\n");
            return new string((char)0xFEFF, 1) + "\r\n" + indented + "\r\n";
        }

        var escaped = FakeServiceAccount.Object(privateKey: FakeServiceAccount.PrivateKeyPem.Replace("\n", "\\n"));
        escaped["private_key_id"] = Marker;
        return escaped.ToJsonString();
    }

    [Theory]
    [MemberData(nameof(Pastes_that_need_normalizing))]
    public async Task The_stored_ciphertext_decrypts_to_the_normalized_key_not_to_what_was_pasted(string shape)
    {
        var input = MarkedPaste(shape);
        Assert.True(ServiceAccountKey.TryNormalize(input, out var normalized));
        Assert.NotEqual(input, normalized);   // the paste really needs normalizing, or this test could not tell the two apart

        var h = new SettingsHarness();
        await h.SaveKeyAsync(input);

        using var document = JsonDocument.Parse(h.Store.Documents[DocumentKeys.ProtectedKey]);
        var cipher = document.RootElement.GetProperty("cipher").GetString();
        Assert.True(h.Protector.TryUnprotect(cipher, out var plain));

        Assert.Equal(normalized, plain);
        Assert.NotEqual(input, plain);
        Assert.Contains(Marker, plain);
    }

    [Fact]
    public async Task Nothing_the_service_returns_or_logs_carries_the_key()
    {
        var h = new SettingsHarness();

        var saved = await h.SaveKeyAsync(KeyWithMarker());
        var read = await h.Settings.GetAsync();
        var state = await h.Settings.GetSetupStateAsync();
        var missing = await h.Settings.GetMissingAsync();

        foreach (var text in new[]
                 {
                     saved.ToString(), saved.Key.ToString(), JsonSerializer.Serialize(saved), JsonSerializer.Serialize(read),
                     state.ToString(), string.Join(",", missing), h.Log.AllText,
                 })
        {
            Assert.DoesNotContain(Marker, text);
            Assert.DoesNotContain("PRIVATE KEY", text);
            Assert.DoesNotContain(PemFragment, text);
        }
    }

    [Fact]
    public async Task Saving_settings_without_a_key_leaves_the_stored_key_alone()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("svc@proj.iam.gserviceaccount.com"));

        var saved = await h.Settings.SaveAsync(new SeoReportSettings { DisplayName = "Acme" }, null, false);
        var blank = await h.Settings.SaveAsync(new SeoReportSettings { DisplayName = "Acme" }, "   ", false);

        Assert.True(saved.Key.HasKey);
        Assert.True(blank.Key.HasKey);
        Assert.Equal("svc@proj.iam.gserviceaccount.com", blank.Key.ServiceAccountEmail);
    }

    [Fact]
    public async Task A_new_key_replaces_the_stored_one()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("one@proj.iam.gserviceaccount.com"));

        var saved = await h.SaveKeyAsync(Key("two@proj.iam.gserviceaccount.com"));

        Assert.Equal("two@proj.iam.gserviceaccount.com", saved.Key.ServiceAccountEmail);
    }

    [Fact]
    public async Task Removing_the_key_deletes_it()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("svc@proj.iam.gserviceaccount.com"));
        Assert.Contains(DocumentKeys.ProtectedKey, h.Store.Documents.Keys);

        var saved = await h.Settings.SaveAsync(new SeoReportSettings(), null, removeKey: true);

        Assert.DoesNotContain(DocumentKeys.ProtectedKey, h.Store.Documents.Keys);
        Assert.Equal(new KeyStatus(false, KeySourceKind.None, null, false), saved.Key);
    }

    [Fact]
    public async Task Removing_a_key_that_is_not_there_is_not_an_error()
    {
        var h = new SettingsHarness();

        var saved = await h.Settings.SaveAsync(new SeoReportSettings(), null, removeKey: true);

        Assert.False(saved.Key.HasKey);
    }

    [Fact]
    public async Task A_new_key_wins_over_remove_when_both_are_given()
    {
        var h = new SettingsHarness();

        var saved = await h.Settings.SaveAsync(new SeoReportSettings(), Key("svc@proj.iam.gserviceaccount.com"), removeKey: true);

        Assert.True(saved.Key.HasKey);
    }

    // ---- What is kept of a key change: one line in the log, naming the action and where the key is kept --------

    [Fact]
    public async Task Saving_a_key_is_logged_once_with_the_action_and_the_source_and_nothing_of_the_key()
    {
        var h = new SettingsHarness();

        await h.Settings.SaveAsync(new SeoReportSettings(), KeyWithMarker("audit@proj.iam.gserviceaccount.com"), false);

        var line = Assert.Single(h.Log.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("saved", line.Text);
        Assert.Contains(nameof(KeySourceKind.Database), line.Text);
        Assert.DoesNotContain(Marker, h.Log.AllText);
        Assert.DoesNotContain("audit@", h.Log.AllText);
        Assert.DoesNotContain("PRIVATE KEY", h.Log.AllText);
        Assert.DoesNotContain(PemFragment, h.Log.AllText);
    }

    [Fact]
    public async Task Removing_a_key_is_logged_once_with_the_action_and_the_source()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(KeyWithMarker("audit@proj.iam.gserviceaccount.com"));

        await h.Settings.SaveAsync(new SeoReportSettings(), null, removeKey: true);

        var lines = h.Log.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Contains("removed", lines[1].Text);
        Assert.Contains(nameof(KeySourceKind.Database), lines[1].Text);
        Assert.DoesNotContain(Marker, h.Log.AllText);
        Assert.DoesNotContain("audit@", h.Log.AllText);
    }

    [Fact]
    public async Task Removing_a_key_that_is_not_stored_logs_no_removal()
    {
        var h = new SettingsHarness();

        await h.Settings.SaveAsync(new SeoReportSettings(), null, removeKey: true);

        // A trace that says "removed" when nothing was there would be wrong the day someone reads it to see what happened.
        Assert.DoesNotContain(h.Log.Entries, e => e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task Saving_settings_without_touching_the_key_logs_nothing_about_it()
    {
        var h = new SettingsHarness();

        await h.Settings.SaveAsync(new SeoReportSettings { DisplayName = "Acme" }, "  ", false);

        Assert.DoesNotContain(h.Log.Entries, e => e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task A_refused_key_is_not_logged_as_saved()
    {
        var h = new SettingsHarness();

        await Assert.ThrowsAsync<SettingsValidationException>(() => h.Settings.SaveAsync(new SeoReportSettings(), "not json " + Marker, false));

        Assert.DoesNotContain(h.Log.Entries, e => e.Level == LogLevel.Information);
        Assert.DoesNotContain(Marker, h.Log.AllText);
    }

    public static TheoryData<string> Keys_that_are_refused() => new()
    {
        "not json",
        "[]",
        "{}",
        "wrong type",
        "no private key",
        "junk PEM",
        "oversized",
    };

    private static string RefusedKey(string label)
    {
        var key = FakeServiceAccount.Object();
        key["private_key_id"] = Marker;
        switch (label)
        {
            case "not json": return $"{Marker} not json";
            case "[]": return $"[\"{Marker}\"]";
            case "{}": return "{}";
            case "wrong type": key["type"] = "authorized_user"; break;
            case "no private key": key.Remove("private_key"); break;
            // Passes the shape check (a PEM header and footer) but is not an RSA key, so only building the credential refuses it.
            case "junk PEM": key["private_key"] = $"-----BEGIN PRIVATE KEY-----\n{Marker}\n-----END PRIVATE KEY-----\n"; break;
            case "oversized": key["pad"] = new string('x', ServiceAccountKey.MaxBytes); break;
            default: throw new ArgumentOutOfRangeException(nameof(label));
        }

        return key.ToJsonString();
    }

    [Theory]
    [MemberData(nameof(Keys_that_are_refused))]
    public async Task A_key_that_is_not_a_usable_service_account_key_is_refused_without_echoing_it_and_nothing_is_saved(string label)
    {
        var h = new SettingsHarness();
        var input = RefusedKey(label);

        var ex = await Assert.ThrowsAsync<SettingsValidationException>(() => h.SaveKeyAsync(input));

        Assert.Equal("keyInvalid", ex.Code);
        Assert.Equal("keyInvalid", ex.Message);
        Assert.Null(ex.InnerException);
        Assert.DoesNotContain(Marker, ex.ToString());
        Assert.DoesNotContain(PemFragment, ex.ToString());
        Assert.DoesNotContain(Marker, h.Log.AllText);
        Assert.Empty(h.Store.Documents);
    }

    [Fact]
    public async Task A_refused_key_leaves_the_stored_key_in_place()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("svc@proj.iam.gserviceaccount.com"));

        await Assert.ThrowsAsync<SettingsValidationException>(() => h.SaveKeyAsync("not json"));

        Assert.Equal("svc@proj.iam.gserviceaccount.com", (await h.Settings.GetAsync()).Key.ServiceAccountEmail);
    }

    // ---- The key: which one is in force, and when it cannot be read ------------------------------------------

    [Fact]
    public async Task A_configured_key_is_in_force_and_a_stored_key_is_ignored()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("db@proj.iam.gserviceaccount.com"));
        h.Configure("ServiceAccountJson", Key("cfg@proj.iam.gserviceaccount.com"));
        ExpireCache(h);

        var effective = await h.Settings.GetAsync();

        Assert.Equal(new KeyStatus(true, KeySourceKind.Configuration, "cfg@proj.iam.gserviceaccount.com", false), effective.Key);
    }

    [Fact]
    public async Task A_key_can_still_be_saved_while_configuration_supplies_one_and_applies_when_that_is_removed()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountJson", Key("cfg@proj.iam.gserviceaccount.com"));

        var saved = await h.SaveKeyAsync(Key("db@proj.iam.gserviceaccount.com"));
        Assert.Equal(KeySourceKind.Configuration, saved.Key.Source);

        h.Configure("ServiceAccountJson", null);
        ExpireCache(h);

        Assert.Equal(new KeyStatus(true, KeySourceKind.Database, "db@proj.iam.gserviceaccount.com", false), (await h.Settings.GetAsync()).Key);
    }

    [Fact]
    public async Task A_configured_key_file_counts_as_configuration()
    {
        using var dir = new TempDirectory();
        var h = new SettingsHarness();
        h.Configure("ServiceAccountKeyFile", dir.Write("key.json", Key("file@proj.iam.gserviceaccount.com")));

        var effective = await h.Settings.GetAsync();

        Assert.Equal(new KeyStatus(true, KeySourceKind.Configuration, "file@proj.iam.gserviceaccount.com", false), effective.Key);
    }

    [Fact]
    public async Task A_key_that_cannot_be_decrypted_is_reported_as_unreadable_and_never_throws()
    {
        var other = new SettingsHarness();   // another key ring: what a restored backup looks like
        await other.SaveKeyAsync(KeyWithMarker());
        var h = new SettingsHarness();
        await h.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, other.Store.Documents[DocumentKeys.ProtectedKey]);

        var effective = await h.Settings.GetAsync();

        Assert.Equal(new KeyStatus(false, KeySourceKind.Database, null, true), effective.Key);
        Assert.Equal(SetupState.KeyUnreadable, await h.Settings.GetSetupStateAsync());
        Assert.DoesNotContain(Marker, h.Log.AllText);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"cipher\":\"\"}")]
    [InlineData("{\"version\":1,\"cipher\":\"not a ciphertext\"}")]
    [InlineData("{\"version\":2,\"cipher\":\"x\"}")]
    [InlineData("[1]")]
    public async Task A_damaged_key_document_is_reported_as_unreadable(string document)
    {
        var h = new SettingsHarness();
        await h.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, document);

        var effective = await h.Settings.GetAsync();

        Assert.Equal(new KeyStatus(false, KeySourceKind.Database, null, true), effective.Key);
        Assert.Equal(SetupState.KeyUnreadable, await h.Settings.GetSetupStateAsync());
    }

    [Fact]
    public async Task A_stored_key_that_decrypts_but_no_longer_validates_is_unreadable()
    {
        var h = new SettingsHarness();
        await h.StoreRawProtectedKeyAsync("{\"type\":\"authorized_user\"}");

        var effective = await h.Settings.GetAsync();

        Assert.Equal(new KeyStatus(false, KeySourceKind.Database, null, true), effective.Key);
    }

    [Fact]
    public async Task An_unreadable_key_is_logged_once_not_every_time_the_cache_refreshes()
    {
        var h = new SettingsHarness();
        await h.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, "{\"version\":1,\"cipher\":\"not a ciphertext\"}");

        for (var i = 0; i < 4; i++)
        {
            await h.Settings.GetAsync();
            ExpireCache(h);
        }

        Assert.Single(h.Log.Entries, e => e.Level == LogLevel.Warning);
    }

    [Theory]
    [InlineData("ServiceAccountJson", "not json")]
    [InlineData("ServiceAccountKeyFile", "C:/does/not/exist/key.json")]
    public async Task A_configured_key_that_is_unusable_wins_and_is_unreadable_it_does_not_fall_back_to_the_stored_one(string name, string value)
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("db@proj.iam.gserviceaccount.com"));
        h.Configure(name, value);
        ExpireCache(h);

        var effective = await h.Settings.GetAsync();

        Assert.Equal(new KeyStatus(false, KeySourceKind.Configuration, null, true), effective.Key);
        Assert.Equal(SetupState.KeyUnreadable, await h.Settings.GetSetupStateAsync());
    }

    [Fact]
    public async Task A_key_written_in_configuration_as_a_JSON_object_is_unreadable_and_does_not_fall_back_to_the_stored_one()
    {
        // The downloaded key is JSON, so pasting it into appsettings.json as an object is a likely mistake. The configuration
        // system then holds ServiceAccountJson:type, ServiceAccountJson:client_email, ... and no string at ServiceAccountJson.
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("db@proj.iam.gserviceaccount.com"));
        foreach (var (name, value) in FakeServiceAccount.Object())
        {
            h.Configure($"ServiceAccountJson:{name}", value!.GetValue<string>());
        }

        ExpireCache(h);

        var effective = await h.Settings.GetAsync();

        Assert.Equal(new KeyStatus(false, KeySourceKind.Configuration, null, true), effective.Key);
        Assert.Equal(SetupState.KeyUnreadable, await h.Settings.GetSetupStateAsync());
        Assert.Single(h.Log.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain("PRIVATE KEY", h.Log.AllText);
    }

    // ---- Setup state and what is missing ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false, SetupState.NotConfigured, "key,ga4PropertyId,searchConsoleSite")]
    [InlineData(true, false, false, SetupState.NotConfigured, "ga4PropertyId,searchConsoleSite")]
    [InlineData(false, true, true, SetupState.NotConfigured, "key")]
    [InlineData(true, true, false, SetupState.NotConfigured, "searchConsoleSite")]
    [InlineData(true, false, true, SetupState.NotConfigured, "ga4PropertyId")]
    [InlineData(false, true, false, SetupState.NotConfigured, "key,searchConsoleSite")]
    [InlineData(true, true, true, SetupState.Ready, "")]
    public async Task Ready_needs_a_key_and_both_properties(bool key, bool ga4, bool site, SetupState expected, string missing)
    {
        var h = new SettingsHarness();
        await h.Settings.SaveAsync(
            new SeoReportSettings { Ga4PropertyId = ga4 ? "123" : null, SearchConsoleSite = site ? "example.com" : null },
            key ? Key("svc@proj.iam.gserviceaccount.com") : null,
            false);

        Assert.Equal(expected, await h.Settings.GetSetupStateAsync());
        Assert.Equal(missing.Length == 0 ? Array.Empty<string>() : missing.Split(','), await h.Settings.GetMissingAsync());
    }

    [Fact]
    public async Task An_unreadable_key_wins_over_not_configured_and_counts_as_a_missing_key()
    {
        var h = new SettingsHarness();
        await h.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, "garbage");

        Assert.Equal(SetupState.KeyUnreadable, await h.Settings.GetSetupStateAsync());
        Assert.Equal(["key", "ga4PropertyId", "searchConsoleSite"], await h.Settings.GetMissingAsync());

        await h.Settings.SaveAsync(new SeoReportSettings { Ga4PropertyId = "1", SearchConsoleSite = "example.com" }, null, false);

        Assert.Equal(SetupState.KeyUnreadable, await h.Settings.GetSetupStateAsync());
        Assert.Equal(["key"], await h.Settings.GetMissingAsync());
    }

    [Fact]
    public async Task Configuration_can_supply_everything_needed_to_be_ready()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountJson", Key("cfg@proj.iam.gserviceaccount.com"));
        h.Configure("Ga4PropertyId", "123");
        h.Configure("SearchConsoleSite", "https://www.example.com");

        Assert.Equal(SetupState.Ready, await h.Settings.GetSetupStateAsync());
        Assert.Empty(await h.Settings.GetMissingAsync());
    }

    [Fact]
    public void Document_keys_are_the_documented_names()
    {
        Assert.Equal("settings", DocumentKeys.Settings);
        Assert.Equal("protectedKey", DocumentKeys.ProtectedKey);
        Assert.Equal("targets", DocumentKeys.Targets);
        Assert.Equal("fetchState", DocumentKeys.FetchState);
    }
}
