using System.Text;
using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google.Apis.Auth.OAuth2;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Turning the key into a Google credential: configuration JSON, then a configured key file, then the stored key; every way a
/// key reaches the package (pasted with a BOM, CRLF or escaped newlines, mounted as a file); and the codes for every way it fails.
/// </summary>
public class GoogleCredentialSourceTests : IDisposable
{
    private const string Marker = FakeServiceAccount.LeakMarker;
    private const string Email = "svc@proj.iam.gserviceaccount.com";

    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private static string BomText(string text) => new string((char)0xFEFF, 1) + text;

    private static string PemFragment => FakeServiceAccount.PrivateKeyPem.Split('\n')[1][..30];

    private static string Key(string email = Email) => FakeServiceAccount.Json(email);

    private static string KeyWithMarker()
    {
        var key = FakeServiceAccount.Object();
        key["private_key_id"] = Marker;
        return key.ToJsonString();
    }

    private static void ExpireCache(SettingsHarness h) => h.Time.Advance(TimeSpan.FromMinutes(2));

    /// <summary>
    /// The ways a key reaches the package that are not the file Google's console downloads. They are named, not passed as
    /// text, so the per-process random key does not end up in the test ids.
    /// </summary>
    public static TheoryData<string> Pasted_variations() => new()
    {
        "as downloaded",
        "with a BOM",
        "with a BOM, CRLF line endings and indentation",
        "surrounded by blank lines",
        "with the private key's newlines pasted as backslash-n",
        "with the private key's newlines pasted as backslash-r-backslash-n",
        "with CRLF inside the private key",
        "with a trailing comma",
    };

    private static string PastedVariation(string name)
    {
        var pem = FakeServiceAccount.PrivateKeyPem;
        return name switch
        {
            "as downloaded" => Key(),
            "with a BOM" => BomText(Key()),
            "with a BOM, CRLF line endings and indentation" => BomText(
                "\r\n" + FakeServiceAccount.Object().ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\r\n") + "\r\n"),
            "surrounded by blank lines" => "\r\n\r\n   " + Key() + "   \r\n\r\n",
            "with the private key's newlines pasted as backslash-n" => FakeServiceAccount.Json(privateKey: pem.Replace("\n", "\\n")),
            "with the private key's newlines pasted as backslash-r-backslash-n" => FakeServiceAccount.Json(privateKey: pem.Replace("\n", "\\r\\n")),
            "with CRLF inside the private key" => FakeServiceAccount.Json(privateKey: pem.Replace("\n", "\r\n")),
            "with a trailing comma" => Key()[..^1] + ",}",
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
    }

    private static void AssertWorks(CredentialResult result, KeySourceKind source)
    {
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.Credential);
        Assert.Equal(Email, result.ClientEmail);
        Assert.Equal(source, result.Source);
    }

    private static void AssertFailed(CredentialResult result, string code, KeySourceKind source)
    {
        Assert.Equal(code, result.ErrorCode);
        Assert.Null(result.Credential);
        Assert.Null(result.ClientEmail);
        Assert.Equal(source, result.Source);
    }

    // ---- Nothing set -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Without_any_key_the_result_is_notConfigured()
    {
        var h = new SettingsHarness();

        var result = await h.Credentials.GetAsync(CancellationToken.None);

        AssertFailed(result, "notConfigured", KeySourceKind.None);
    }

    [Fact]
    public async Task Blank_configuration_values_count_as_not_set()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountJson", "   ");
        h.Configure("ServiceAccountKeyFile", "");

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "notConfigured", KeySourceKind.None);
    }

    // ---- A stored key ----------------------------------------------------------------------------------------

    [Fact]
    public async Task A_stored_key_gives_a_service_account_credential_with_the_two_read_only_scopes()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key());

        var result = await h.Credentials.GetAsync(CancellationToken.None);

        AssertWorks(result, KeySourceKind.Database);
        var account = Assert.IsType<ServiceAccountCredential>(result.Credential!.UnderlyingCredential);
        Assert.Equal(Email, account.Id);
        Assert.Equal(
            ["https://www.googleapis.com/auth/analytics.readonly", "https://www.googleapis.com/auth/webmasters.readonly"],
            account.Scopes.Order().ToArray());
    }

    [Theory]
    [MemberData(nameof(Pasted_variations))]
    public async Task A_key_pasted_in_any_of_the_usual_shapes_is_saved_and_works(string variation)
    {
        var h = new SettingsHarness();

        await h.SaveKeyAsync(PastedVariation(variation));

        AssertWorks(await h.Credentials.GetAsync(CancellationToken.None), KeySourceKind.Database);
    }

    [Fact]
    public async Task Saving_a_key_takes_effect_at_once_with_no_restart_and_no_cache_expiry()
    {
        var h = new SettingsHarness();
        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "notConfigured", KeySourceKind.None);

        await h.SaveKeyAsync(Key("one@proj.iam.gserviceaccount.com"));
        var one = await h.Credentials.GetAsync(CancellationToken.None);

        await h.SaveKeyAsync(Key("two@proj.iam.gserviceaccount.com"));
        var two = await h.Credentials.GetAsync(CancellationToken.None);

        await h.Settings.SaveAsync(new SeoReportSettings(), null, removeKey: true);
        var none = await h.Credentials.GetAsync(CancellationToken.None);

        Assert.Equal("one@proj.iam.gserviceaccount.com", one.ClientEmail);
        Assert.Equal("two@proj.iam.gserviceaccount.com", two.ClientEmail);
        Assert.NotSame(one.Credential, two.Credential);
        AssertFailed(none, "notConfigured", KeySourceKind.None);
    }

    [Fact]
    public async Task Saving_other_settings_also_refreshes_the_cached_result_harmlessly()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key());
        await h.Credentials.GetAsync(CancellationToken.None);

        await h.Settings.SaveAsync(new SeoReportSettings { DisplayName = "Acme" }, null, false);

        AssertWorks(await h.Credentials.GetAsync(CancellationToken.None), KeySourceKind.Database);
    }

    [Fact]
    public async Task A_stored_key_that_cannot_be_decrypted_is_keyUnreadable()
    {
        var other = new SettingsHarness();
        await other.SaveKeyAsync(Key());
        var h = new SettingsHarness();
        await h.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, other.Store.Documents[DocumentKeys.ProtectedKey]);

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyUnreadable", KeySourceKind.Database);
    }

    [Fact]
    public async Task A_stored_key_whose_key_ring_storage_cannot_be_reached_is_keyUnreadable_and_not_an_error()
    {
        var other = new SettingsHarness();
        await other.SaveKeyAsync(Key());
        var h = new SettingsHarness(SecretProtectorTests.UnreachableKeyStorage());
        await h.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, other.Store.Documents[DocumentKeys.ProtectedKey]);

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyUnreadable", KeySourceKind.Database);
        Assert.Equal(SetupState.KeyUnreadable, await h.Settings.GetSetupStateAsync());
    }

    [Fact]
    public async Task A_stored_key_that_decrypts_to_something_that_is_not_a_service_account_key_is_keyInvalid()
    {
        var h = new SettingsHarness();
        await h.StoreRawProtectedKeyAsync("{\"type\":\"authorized_user\"}");

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyInvalid", KeySourceKind.Database);
    }

    // ---- Configuration: JSON ---------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Pasted_variations))]
    public async Task A_configured_key_in_any_of_the_usual_shapes_works(string variation)
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountJson", PastedVariation(variation));

        AssertWorks(await h.Credentials.GetAsync(CancellationToken.None), KeySourceKind.Configuration);
    }

    [Fact]
    public async Task A_configured_key_wins_over_a_stored_one()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("db@proj.iam.gserviceaccount.com"));
        h.Configure("ServiceAccountJson", Key());
        ExpireCache(h);

        AssertWorks(await h.Credentials.GetAsync(CancellationToken.None), KeySourceKind.Configuration);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"type\":\"authorized_user\",\"client_email\":\"a@b.c\",\"private_key\":\"-----BEGIN PRIVATE KEY-----\"}")]
    public async Task A_configured_key_that_is_not_a_service_account_key_is_keyInvalid_and_nothing_else_is_tried(string json)
    {
        using var dir = new TempDirectory();
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("db@proj.iam.gserviceaccount.com"));
        h.Configure("ServiceAccountKeyFile", dir.Write("good.json", Key("file@proj.iam.gserviceaccount.com")));
        h.Configure("ServiceAccountJson", json);
        ExpireCache(h);

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyInvalid", KeySourceKind.Configuration);
    }

    [Fact]
    public async Task A_key_written_in_configuration_as_a_JSON_object_is_keyInvalid_and_nothing_else_is_tried()
    {
        // appsettings.json with the downloaded key pasted as an object leaves no string at ServiceAccountJson, only
        // ServiceAccountJson:type, ServiceAccountJson:client_email, ... It must not be skipped for the key file or the stored key.
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("db@proj.iam.gserviceaccount.com"));
        h.Configure("ServiceAccountKeyFile", _dir.Write("good.json", Key("file@proj.iam.gserviceaccount.com")));
        foreach (var (name, value) in FakeServiceAccount.Object())
        {
            h.Configure($"ServiceAccountJson:{name}", value!.GetValue<string>());
        }

        ExpireCache(h);

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyInvalid", KeySourceKind.Configuration);
    }

    // ---- The token endpoint: only Google's ---------------------------------------------------------------------

    public static TheoryData<string> Foreign_token_endpoints() => new()
    {
        "http://127.0.0.1:9/x",
        "https://evil.example/token",
        "http://oauth2.googleapis.com/token",
        "https://oauth2.googleapis.com/token/",
    };

    private static string KeyWithTokenEndpoint(string tokenUri)
    {
        var key = FakeServiceAccount.Object();
        key["token_uri"] = tokenUri;
        return key.ToJsonString();
    }

    [Theory]
    [MemberData(nameof(Foreign_token_endpoints))]
    public async Task A_configured_key_whose_token_endpoint_is_not_Googles_is_keyInvalid(string tokenUri)
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountJson", KeyWithTokenEndpoint(tokenUri));

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyInvalid", KeySourceKind.Configuration);
    }

    [Theory]
    [MemberData(nameof(Foreign_token_endpoints))]
    public async Task A_key_file_whose_token_endpoint_is_not_Googles_is_keyInvalid(string tokenUri)
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountKeyFile", _dir.Write("key.json", KeyWithTokenEndpoint(tokenUri)));

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyInvalid", KeySourceKind.Configuration);
    }

    [Theory]
    [MemberData(nameof(Foreign_token_endpoints))]
    public async Task A_key_whose_token_endpoint_is_not_Googles_cannot_be_saved_and_one_stored_earlier_is_keyInvalid(string tokenUri)
    {
        var h = new SettingsHarness();

        var refused = await Assert.ThrowsAsync<SettingsValidationException>(() => h.SaveKeyAsync(KeyWithTokenEndpoint(tokenUri)));
        Assert.Equal("keyInvalid", refused.Code);
        Assert.Null(await h.Store.GetDocumentAsync(DocumentKeys.ProtectedKey));

        await h.StoreRawProtectedKeyAsync(KeyWithTokenEndpoint(tokenUri));
        ExpireCache(h);

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyInvalid", KeySourceKind.Database);
    }

    [Fact]
    public async Task The_credential_always_asks_Googles_own_token_endpoint_whatever_else_the_key_names()
    {
        var h = new SettingsHarness();
        var key = FakeServiceAccount.Object();
        key["universe_domain"] = "internal.example";
        key["auth_uri"] = "http://127.0.0.1:9/auth";
        key["client_x509_cert_url"] = "http://127.0.0.1:9/cert";
        h.Configure("ServiceAccountJson", key.ToJsonString());

        var result = await h.Credentials.GetAsync(CancellationToken.None);

        AssertWorks(result, KeySourceKind.Configuration);
        var account = Assert.IsType<ServiceAccountCredential>(result.Credential!.UnderlyingCredential);
        Assert.Equal("https://oauth2.googleapis.com/token", account.TokenServerUrl);
        Assert.Equal("googleapis.com", await result.Credential.GetUniverseDomainAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_key_with_the_right_shape_but_a_private_key_that_is_not_RSA_is_keyInvalid()
    {
        var h = new SettingsHarness();
        var key = FakeServiceAccount.Object(privateKey: "-----BEGIN PRIVATE KEY-----\nAAAA\n-----END PRIVATE KEY-----\n");
        h.Configure("ServiceAccountJson", key.ToJsonString());

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyInvalid", KeySourceKind.Configuration);
    }

    // ---- Configuration: key file -----------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Pasted_variations))]
    public async Task A_key_file_in_any_of_the_usual_shapes_works(string variation)
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountKeyFile", _dir.Write("key.json", PastedVariation(variation)));

        AssertWorks(await h.Credentials.GetAsync(CancellationToken.None), KeySourceKind.Configuration);
    }

    [Fact]
    public async Task A_key_file_saved_as_UTF16_with_a_BOM_works()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountKeyFile", _dir.Write("key16.json", Key(), new UnicodeEncoding(false, true)));

        AssertWorks(await h.Credentials.GetAsync(CancellationToken.None), KeySourceKind.Configuration);
    }

    [Fact]
    public async Task A_key_file_path_with_surrounding_whitespace_is_trimmed()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountKeyFile", "  " + _dir.Write("key.json", Key()) + "  ");

        AssertWorks(await h.Credentials.GetAsync(CancellationToken.None), KeySourceKind.Configuration);
    }

    [Fact]
    public async Task A_missing_key_file_is_keyUnreadable()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountKeyFile", Path.Combine(_dir.Root, "missing.json"));

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyUnreadable", KeySourceKind.Configuration);
    }

    [Fact]
    public async Task A_key_file_in_a_folder_that_does_not_exist_is_keyUnreadable()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountKeyFile", Path.Combine(_dir.Root, "no-such-folder", "key.json"));

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyUnreadable", KeySourceKind.Configuration);
    }

    [Theory]
    [InlineData("bad\0name.json")]
    [InlineData("?:*<>|")]
    [InlineData("a:b:c")]
    public async Task A_key_file_path_that_is_not_a_valid_path_is_keyUnreadable_and_does_not_throw(string path)
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountKeyFile", path);

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyUnreadable", KeySourceKind.Configuration);
    }

    [Fact]
    public async Task A_directory_given_as_the_key_file_is_keyUnreadable()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountKeyFile", _dir.Root);

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyUnreadable", KeySourceKind.Configuration);
    }

    [Fact]
    public async Task A_key_file_that_is_locked_by_another_process_is_keyUnreadable()
    {
        var h = new SettingsHarness();
        var path = _dir.Write("locked.json", Key());
        h.Configure("ServiceAccountKeyFile", path);

        using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyUnreadable", KeySourceKind.Configuration);
    }

    public static TheoryData<string> Files_that_are_not_service_account_keys() => new()
    {
        "an authorized-user file",
        "plain text",
        "an empty file",
        "binary bytes",
        "a JSON array",
        "an oversized file",
    };

    [Theory]
    [MemberData(nameof(Files_that_are_not_service_account_keys))]
    public async Task A_readable_file_that_is_not_a_service_account_key_is_keyInvalid(string kind)
    {
        var h = new SettingsHarness();
        var path = kind switch
        {
            "an authorized-user file" => _dir.Write("f.json", "{\"type\":\"authorized_user\",\"client_id\":\"x\",\"refresh_token\":\"y\"}"),
            "plain text" => _dir.Write("f.json", "hello"),
            "an empty file" => _dir.Write("f.json", ""),
            "binary bytes" => _dir.WriteBytes("f.json", [0x00, 0xFF, 0xFE, 0x01, 0x80, 0x7F]),
            "a JSON array" => _dir.Write("f.json", "[1,2,3]"),
            "an oversized file" => _dir.Write("f.json", new string(' ', 200_000) + Key()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        h.Configure("ServiceAccountKeyFile", path);

        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyInvalid", KeySourceKind.Configuration);
    }

    // A key file is read through ONE stream and never past the limit: nothing is checked first and read later, so a file that
    // grows in between cannot slip through, and a huge file costs no more than the limit to refuse.

    [Fact]
    public async Task A_key_file_of_exactly_the_limit_is_read_in_full()
    {
        var path = _dir.Write("exact.json", new string('x', 64));

        Assert.Equal(new string('x', 64), await KeyVault.ReadKeyFileAsync(path, 64, CancellationToken.None));
    }

    [Fact]
    public async Task A_key_file_one_byte_over_the_limit_is_not_read()
    {
        var path = _dir.Write("over.json", new string('x', 65));

        Assert.Null(await KeyVault.ReadKeyFileAsync(path, 64, CancellationToken.None));
    }

    [Fact]
    public async Task A_key_file_far_over_the_limit_is_not_read_either()
    {
        var path = _dir.Write("huge.json", new string('x', 1_000_000));

        Assert.Null(await KeyVault.ReadKeyFileAsync(path, 64, CancellationToken.None));
    }

    [Fact]
    public async Task A_key_file_read_within_the_limit_detects_its_encoding_from_the_BOM()
    {
        var utf16 = _dir.Write("utf16.json", "{\"a\":\"Å\"}", new UnicodeEncoding(false, true));
        var utf8 = _dir.Write("utf8.json", "{\"a\":\"Å\"}", new UTF8Encoding(true));

        Assert.Equal("{\"a\":\"Å\"}", await KeyVault.ReadKeyFileAsync(utf16, 64, CancellationToken.None));
        Assert.Equal("{\"a\":\"Å\"}", await KeyVault.ReadKeyFileAsync(utf8, 64, CancellationToken.None));
    }

    [Fact]
    public async Task A_missing_key_file_is_still_an_IO_failure_for_the_caller_to_report()
        => await Assert.ThrowsAnyAsync<IOException>(() => KeyVault.ReadKeyFileAsync(Path.Combine(_dir.Root, "missing.json"), 64, CancellationToken.None));

    [Fact]
    public async Task A_configured_key_file_wins_over_a_stored_key_and_loses_to_configured_JSON()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("db@proj.iam.gserviceaccount.com"));
        h.Configure("ServiceAccountKeyFile", _dir.Write("key.json", Key("file@proj.iam.gserviceaccount.com")));
        h.Configure("ServiceAccountJson", Key("json@proj.iam.gserviceaccount.com"));
        ExpireCache(h);

        var json = await h.Credentials.GetAsync(CancellationToken.None);

        h.Configure("ServiceAccountJson", null);
        ExpireCache(h);
        var file = await h.Credentials.GetAsync(CancellationToken.None);

        h.Configure("ServiceAccountKeyFile", null);
        ExpireCache(h);
        var database = await h.Credentials.GetAsync(CancellationToken.None);

        Assert.Equal(("json@proj.iam.gserviceaccount.com", KeySourceKind.Configuration), (json.ClientEmail, json.Source));
        Assert.Equal(("file@proj.iam.gserviceaccount.com", KeySourceKind.Configuration), (file.ClientEmail, file.Source));
        Assert.Equal(("db@proj.iam.gserviceaccount.com", KeySourceKind.Database), (database.ClientEmail, database.Source));
    }

    [Fact]
    public async Task A_rotated_key_file_is_picked_up_when_the_cache_expires()
    {
        var h = new SettingsHarness();
        var path = _dir.Write("key.json", Key("old@proj.iam.gserviceaccount.com"));
        h.Configure("ServiceAccountKeyFile", path);
        Assert.Equal("old@proj.iam.gserviceaccount.com", (await h.Credentials.GetAsync(CancellationToken.None)).ClientEmail);

        _dir.Write("key.json", Key("new@proj.iam.gserviceaccount.com"));
        Assert.Equal("old@proj.iam.gserviceaccount.com", (await h.Credentials.GetAsync(CancellationToken.None)).ClientEmail);

        ExpireCache(h);

        Assert.Equal("new@proj.iam.gserviceaccount.com", (await h.Credentials.GetAsync(CancellationToken.None)).ClientEmail);
    }

    // ---- Cache -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_credential_is_cached_for_a_minute()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountJson", Key("one@proj.iam.gserviceaccount.com"));
        var first = await h.Credentials.GetAsync(CancellationToken.None);

        h.Configure("ServiceAccountJson", Key("two@proj.iam.gserviceaccount.com"));
        h.Time.Advance(TimeSpan.FromSeconds(59));
        var cached = await h.Credentials.GetAsync(CancellationToken.None);

        h.Time.Advance(TimeSpan.FromSeconds(2));
        var fresh = await h.Credentials.GetAsync(CancellationToken.None);

        Assert.Same(first.Credential, cached.Credential);
        Assert.Equal("one@proj.iam.gserviceaccount.com", cached.ClientEmail);
        Assert.Equal("two@proj.iam.gserviceaccount.com", fresh.ClientEmail);
    }

    // ---- One credential for one key --------------------------------------------------------------------------
    //
    // The Google services are cached per credential instance (see GoogleClients), so the source must hand back the very same
    // credential for as long as the key and where it comes from stay the same, however often its own cache expires.

    [Fact]
    public async Task The_same_key_gives_the_same_result_and_credential_after_the_cache_expires()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountJson", Key());
        var first = await h.Credentials.GetAsync(CancellationToken.None);

        ExpireCache(h);
        var second = await h.Credentials.GetAsync(CancellationToken.None);
        ExpireCache(h);
        var third = await h.Credentials.GetAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Same(first, third);
        Assert.Same(first.Credential, third.Credential);
    }

    [Fact]
    public async Task The_same_key_saved_again_or_pasted_in_another_shape_keeps_the_credential()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key());
        var first = await h.Credentials.GetAsync(CancellationToken.None);

        await h.SaveKeyAsync(Key());                                      // a save is a new ciphertext of the same key
        var saved = await h.Credentials.GetAsync(CancellationToken.None);
        await h.SaveKeyAsync(BomText("\r\n   " + Key() + "   \r\n"));      // the same key, pasted with a BOM and blank lines
        var reshaped = await h.Credentials.GetAsync(CancellationToken.None);

        Assert.Same(first.Credential, saved.Credential);
        Assert.Same(first.Credential, reshaped.Credential);
    }

    [Fact]
    public async Task A_changed_key_gives_a_new_result_and_a_new_credential()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountJson", Key("one@proj.iam.gserviceaccount.com"));
        var first = await h.Credentials.GetAsync(CancellationToken.None);

        h.Configure("ServiceAccountJson", Key("two@proj.iam.gserviceaccount.com"));
        ExpireCache(h);
        var second = await h.Credentials.GetAsync(CancellationToken.None);
        ExpireCache(h);
        var again = await h.Credentials.GetAsync(CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.NotSame(first.Credential, second.Credential);
        Assert.Equal("two@proj.iam.gserviceaccount.com", second.ClientEmail);
        Assert.Same(second, again);   // and the new key is then kept like the old one was
    }

    [Fact]
    public async Task The_same_key_from_another_source_gives_a_new_credential()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key());
        var stored = await h.Credentials.GetAsync(CancellationToken.None);

        h.Configure("ServiceAccountJson", Key());
        ExpireCache(h);
        var configured = await h.Credentials.GetAsync(CancellationToken.None);

        Assert.Equal(KeySourceKind.Database, stored.Source);
        Assert.Equal(KeySourceKind.Configuration, configured.Source);
        Assert.NotSame(stored.Credential, configured.Credential);
    }

    [Fact]
    public async Task A_failure_is_returned_fresh_and_does_not_stand_in_for_the_last_good_key()
    {
        var h = new SettingsHarness();
        h.Configure("ServiceAccountJson", Key());
        var good = await h.Credentials.GetAsync(CancellationToken.None);

        h.Configure("ServiceAccountJson", $"{Marker} not a key");
        ExpireCache(h);
        var failed = await h.Credentials.GetAsync(CancellationToken.None);
        ExpireCache(h);
        var failedAgain = await h.Credentials.GetAsync(CancellationToken.None);

        h.Configure("ServiceAccountJson", Key());
        ExpireCache(h);
        var back = await h.Credentials.GetAsync(CancellationToken.None);

        AssertFailed(failed, "keyInvalid", KeySourceKind.Configuration);
        AssertFailed(failedAgain, "keyInvalid", KeySourceKind.Configuration);
        Assert.NotSame(failed, failedAgain);
        AssertWorks(back, KeySourceKind.Configuration);

        // The key was replaced by a broken one, so the old credential is not brought back for it.
        Assert.NotSame(good.Credential, back.Credential);
    }

    [Fact]
    public async Task A_removed_key_is_forgotten()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key());
        var first = await h.Credentials.GetAsync(CancellationToken.None);

        await h.Settings.SaveAsync(new SeoReportSettings(), null, true);
        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "notConfigured", KeySourceKind.None);

        await h.SaveKeyAsync(Key());
        var again = await h.Credentials.GetAsync(CancellationToken.None);

        AssertWorks(again, KeySourceKind.Database);
        Assert.NotSame(first.Credential, again.Credential);
    }

    [Fact]
    public async Task A_key_file_that_cannot_be_read_for_a_while_does_not_cost_the_credential()
    {
        var h = new SettingsHarness();
        var path = _dir.Write("key.json", Key());
        h.Configure("ServiceAccountKeyFile", path);
        var first = await h.Credentials.GetAsync(CancellationToken.None);

        File.Delete(path);
        ExpireCache(h);
        AssertFailed(await h.Credentials.GetAsync(CancellationToken.None), "keyUnreadable", KeySourceKind.Configuration);

        _dir.Write("key.json", Key());
        ExpireCache(h);
        var back = await h.Credentials.GetAsync(CancellationToken.None);

        Assert.Same(first.Credential, back.Credential);
    }

    // ---- The key never shows ---------------------------------------------------------------------------------

    [Fact]
    public async Task No_result_prints_the_key_whether_it_worked_or_not()
    {
        var h = new SettingsHarness();
        var results = new List<CredentialResult>();

        await h.SaveKeyAsync(KeyWithMarker());
        results.Add(await h.Credentials.GetAsync(CancellationToken.None));

        h.Configure("ServiceAccountJson", FakeServiceAccount.Json(privateKey: $"-----BEGIN PRIVATE KEY-----\n{Marker}\n-----END PRIVATE KEY-----\n"));
        ExpireCache(h);
        results.Add(await h.Credentials.GetAsync(CancellationToken.None));

        h.Configure("ServiceAccountJson", $"{Marker} not json");
        ExpireCache(h);
        results.Add(await h.Credentials.GetAsync(CancellationToken.None));

        Assert.Equal(new string?[] { null, "keyInvalid", "keyInvalid" }, results.Select(r => r.ErrorCode));
        foreach (var text in results.Select(r => r.ToString()).Append(h.Log.AllText))
        {
            Assert.DoesNotContain(Marker, text);
            Assert.DoesNotContain("PRIVATE KEY", text);
            Assert.DoesNotContain(PemFragment, text);
        }
    }

    [Fact]
    public void The_result_carries_nothing_but_the_credential_the_email_the_source_and_the_code()
    {
        var properties = typeof(CredentialResult).GetProperties().Select(p => p.Name).Order().ToArray();

        Assert.Equal(new[] { "ClientEmail", "Credential", "ErrorCode", "Source" }, properties);
    }

    [Fact]
    public async Task The_interface_is_what_the_service_is_registered_as()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key());

        IGoogleCredentialSource source = h.Credentials;

        AssertWorks(await source.GetAsync(CancellationToken.None), KeySourceKind.Database);
    }
}
