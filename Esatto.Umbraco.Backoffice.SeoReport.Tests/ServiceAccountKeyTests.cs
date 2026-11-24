using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The pasted service-account key is a credential. These tests pin what is accepted, what is repaired on the way in
/// (a BOM, CRLF, a double-escaped PEM) and, above all, that no failure ever echoes any of the input back.
/// </summary>
public class ServiceAccountKeyTests
{
    private const string Marker = FakeServiceAccount.LeakMarker;

    /// <summary>A stretch of the key's base64 body, which would only appear in a text that printed the key.</summary>
    private static string KeyBodyFragment => FakeServiceAccount.PrivateKeyPem.Split('\n')[1][..30];

    // ---- Validate: accepted ----------------------------------------------------------------------------------

    [Fact]
    public void A_key_in_the_shape_Google_downloads_is_valid_and_exposes_only_the_client_email()
    {
        var result = ServiceAccountKey.Validate(FakeServiceAccount.Json());

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
        Assert.Equal("svc@proj.iam.gserviceaccount.com", result.ClientEmail);
    }

    [Fact]
    public void The_client_email_is_returned_without_surrounding_whitespace()
    {
        var result = ServiceAccountKey.Validate(FakeServiceAccount.Json(clientEmail: " svc@proj.iam.gserviceaccount.com "));

        Assert.True(result.IsValid);
        Assert.Equal("svc@proj.iam.gserviceaccount.com", result.ClientEmail);
    }

    [Fact]
    public void A_valid_result_does_not_print_the_private_key()
    {
        var text = ServiceAccountKey.Validate(FakeServiceAccount.Json()).ToString();

        Assert.DoesNotContain("PRIVATE KEY", text);
        Assert.DoesNotContain(KeyBodyFragment, text);
    }

    [Fact]
    public void Surrounding_whitespace_a_BOM_and_CRLF_are_tolerated()
    {
        var indented = FakeServiceAccount.Object().ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .ReplaceLineEndings("\r\n");

        var result = ServiceAccountKey.Validate("\uFEFF\r\n  " + indented + "\r\n\r\n  ");

        Assert.True(result.IsValid);
        Assert.Equal(FakeServiceAccount.DefaultClientEmail, result.ClientEmail);
    }

    [Theory]
    [InlineData("\uFEFF")]
    [InlineData("\r\n\uFEFF")]
    [InlineData("\uFEFF \t\r\n")]
    [InlineData("\r\n\uFEFF\r\n")]
    public void A_BOM_is_stripped_wherever_it_sits_in_the_leading_whitespace(string prefix)
        => Assert.True(ServiceAccountKey.Validate(prefix + FakeServiceAccount.Json()).IsValid);

    [Fact]
    public void A_private_key_whose_newlines_were_pasted_as_the_two_characters_backslash_n_is_valid()
    {
        var doubleEscaped = FakeServiceAccount.PrivateKeyPem.Replace("\n", "\\n");

        var json = FakeServiceAccount.Json(privateKey: doubleEscaped);

        Assert.Contains("\\\\n", json); // the JSON text really carries backslash backslash n
        Assert.True(ServiceAccountKey.Validate(json).IsValid);
    }

    [Fact]
    public void A_private_key_with_CRLF_inside_the_value_is_valid()
    {
        var json = FakeServiceAccount.Json(privateKey: FakeServiceAccount.PrivateKeyPem.Replace("\n", "\r\n"));

        Assert.True(ServiceAccountKey.Validate(json).IsValid);
    }

    [Fact]
    public void A_trailing_comma_is_tolerated()
    {
        var json = FakeServiceAccount.Json();

        Assert.True(ServiceAccountKey.Validate(json[..^1] + ",}").IsValid);
    }

    [Fact]
    public void Exactly_the_size_limit_is_allowed()
        => Assert.True(ServiceAccountKey.Validate(PaddedTo(ServiceAccountKey.MaxBytes)).IsValid);

    // ---- Validate: refused -----------------------------------------------------------------------------------

    [Fact]
    public void One_byte_over_the_size_limit_is_refused()
        => AssertRefused(ServiceAccountKey.Validate(PaddedTo(ServiceAccountKey.MaxBytes + 1)));

    [Fact]
    public void The_size_limit_counts_UTF8_bytes_not_characters()
    {
        // 6000 three-byte characters is 18000 bytes but only 6000 characters.
        var key = FakeServiceAccount.Object();
        key["project_id"] = new string('€', 6000);
        var json = key.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        Assert.True(Encoding.UTF8.GetByteCount(json) > ServiceAccountKey.MaxBytes);
        Assert.True(json.Length < ServiceAccountKey.MaxBytes);

        AssertRefused(ServiceAccountKey.Validate(json));
    }

    [Fact]
    public void The_size_limit_is_measured_on_the_input_as_given_before_whitespace_is_trimmed()
    {
        var json = PaddedTo(ServiceAccountKey.MaxBytes);

        AssertRefused(ServiceAccountKey.Validate(json + " "));
    }

    public static TheoryData<string, string?> Unusable_inputs() => new()
    {
        { "null", null },
        { "empty", "" },
        { "whitespace", "   \r\n\t " },
        { "not json", "not json" },
        { "truncated json", "{\"type\":\"service_account\"," },
        { "an array", "[]" },
        { "a string", "\"service_account\"" },
        { "a number", "123" },
        { "json null", "null" },
        { "an empty object", "{}" },
        { "a comment", "// hello\n{}" },
    };

    [Theory]
    [MemberData(nameof(Unusable_inputs))]
    public void Input_that_is_not_a_service_account_object_is_refused(string label, string? input)
    {
        Assert.NotNull(label);

        AssertRefused(ServiceAccountKey.Validate(input));
    }

    [Theory]
    [InlineData("authorized_user")]
    [InlineData("")]
    [InlineData("SERVICE_ACCOUNT")]
    [InlineData("service_account ")]
    [InlineData("external_account")]
    public void Any_type_other_than_service_account_is_refused(string type)
        => AssertRefused(ServiceAccountKey.Validate(FakeServiceAccount.Json(type: type)));

    [Fact]
    public void A_missing_or_non_string_type_is_refused()
    {
        var missing = FakeServiceAccount.Object();
        missing.Remove("type");
        var number = FakeServiceAccount.Object();
        number["type"] = 7;

        AssertRefused(ServiceAccountKey.Validate(missing.ToJsonString()));
        AssertRefused(ServiceAccountKey.Validate(number.ToJsonString()));
    }

    [Fact]
    public void A_missing_client_email_is_refused()
    {
        var key = FakeServiceAccount.Object();
        key.Remove("client_email");

        AssertRefused(ServiceAccountKey.Validate(key.ToJsonString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_client_email_is_refused(string email)
        => AssertRefused(ServiceAccountKey.Validate(FakeServiceAccount.Json(clientEmail: email)));

    [Fact]
    public void A_client_email_that_is_not_a_string_is_refused()
    {
        var key = FakeServiceAccount.Object();
        key["client_email"] = 42;

        AssertRefused(ServiceAccountKey.Validate(key.ToJsonString()));
    }

    [Fact]
    public void A_missing_private_key_is_refused()
    {
        var key = FakeServiceAccount.Object();
        key.Remove("private_key");

        AssertRefused(ServiceAccountKey.Validate(key.ToJsonString()));
    }

    [Fact]
    public void A_private_key_that_is_not_a_string_is_refused()
    {
        var key = FakeServiceAccount.Object();
        key["private_key"] = new JsonArray("-----BEGIN PRIVATE KEY-----");

        AssertRefused(ServiceAccountKey.Validate(key.ToJsonString()));
    }

    public static TheoryData<string, string> Private_keys_that_are_not_PEM_private_keys() => new()
    {
        { "empty", "" },
        { "plain text", "hello" },
        { "no BEGIN line", "MIIEvQIBADANBgkqhkiG9w0BAQEFAASC" },
        { "a public key", "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkq\n-----END PUBLIC KEY-----\n" },
        { "a certificate", "-----BEGIN CERTIFICATE-----\nMIIBIjANBgkq\n-----END CERTIFICATE-----\n" },
        { "a header without the key type", "-----BEGIN-----\nMIIBIjANBgkq\n-----END-----\n" },
    };

    [Theory]
    [MemberData(nameof(Private_keys_that_are_not_PEM_private_keys))]
    public void A_private_key_that_is_not_a_PEM_private_key_is_refused(string label, string privateKey)
    {
        Assert.NotNull(label);

        AssertRefused(ServiceAccountKey.Validate(FakeServiceAccount.Json(privateKey: privateKey)));
    }

    [Theory]
    [InlineData("-----BEGIN PRIVATE KEY-----")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----")]
    [InlineData("-----BEGIN ENCRYPTED PRIVATE KEY-----")]
    public void The_PEM_private_key_headers_Google_and_OpenSSL_use_are_accepted(string header)
        => Assert.True(ServiceAccountKey.Validate(FakeServiceAccount.Json(privateKey: header + "\nMIIE\n-----END PRIVATE KEY-----\n")).IsValid);

    [Fact]
    public void A_repeated_property_is_refused_even_when_both_copies_are_acceptable()
    {
        // Both copies pass the checks, so only a strict parse refuses it; a lenient one would leave it unclear which
        // copy Google's loader ends up using.
        var json = FakeServiceAccount.Json();

        AssertRefused(ServiceAccountKey.Validate(json[..^1] + ",\"type\":\"service_account\"}"));
        AssertRefused(ServiceAccountKey.Validate(json[..^1] + $",\"client_email\":\"{FakeServiceAccount.DefaultClientEmail}\"}}"));
    }

    // ---- Validate: the token endpoint ------------------------------------------------------------------------
    // Google's loader posts the signed assertion to the key's token_uri, so a key could point the server at any address. Only
    // Google's own endpoints (the current one and the one older keys carry), written exactly as Google writes them, are accepted.

    [Theory]
    [InlineData("http://127.0.0.1:9/x")]
    [InlineData("https://evil.example/token")]
    [InlineData("http://oauth2.googleapis.com/token")]
    [InlineData("https://oauth2.googleapis.com/token/")]
    [InlineData("https://oauth2.googleapis.com/token?x=1")]
    [InlineData("HTTPS://OAUTH2.GOOGLEAPIS.COM/TOKEN")]
    [InlineData("https://oauth2.googleapis.com.evil.example/token")]
    [InlineData("")]
    public void A_token_endpoint_other_than_Googles_is_refused(string tokenUri)
    {
        var key = FakeServiceAccount.Object();
        key["token_uri"] = tokenUri;

        AssertRefused(ServiceAccountKey.Validate(key.ToJsonString()));
    }

    [Fact]
    public void A_token_endpoint_that_is_not_a_string_is_refused()
    {
        foreach (JsonNode? value in new JsonNode?[] { null, JsonValue.Create(1), new JsonObject(), new JsonArray("https://oauth2.googleapis.com/token") })
        {
            var key = FakeServiceAccount.Object();
            key["token_uri"] = value;

            AssertRefused(ServiceAccountKey.Validate(key.ToJsonString()));
        }
    }

    [Fact]
    public void A_token_endpoint_spelled_with_other_letter_case_is_refused_too()
    {
        // Google's loader matches property names without regard to case, so "Token_Uri" would reach it as the token endpoint.
        var key = FakeServiceAccount.Object();
        key["Token_Uri"] = "http://127.0.0.1:9/x";

        AssertRefused(ServiceAccountKey.Validate(key.ToJsonString()));
    }

    [Fact]
    public void Googles_own_token_endpoint_is_accepted()
    {
        var key = FakeServiceAccount.Object();
        key["token_uri"] = "https://oauth2.googleapis.com/token";

        Assert.True(ServiceAccountKey.Validate(key.ToJsonString()).IsValid);
    }

    [Fact]
    public void The_token_endpoint_older_Google_keys_name_is_accepted_and_not_kept()
    {
        // Keys Google issued before 2020 carry this address, and Google still honours them. It is Google's own, and it is
        // dropped with every other endpoint, so the loader signs for the current one.
        var key = FakeServiceAccount.Object();
        key["token_uri"] = ServiceAccountKey.LegacyGoogleTokenEndpoint;
        var json = key.ToJsonString();

        Assert.Equal("https://accounts.google.com/o/oauth2/token", ServiceAccountKey.LegacyGoogleTokenEndpoint);
        Assert.True(ServiceAccountKey.Validate(json).IsValid);
        Assert.True(ServiceAccountKey.TryNormalize(json, out var normalized));
        Assert.DoesNotContain("token_uri", normalized);
        Assert.DoesNotContain("accounts.google.com", normalized);
    }

    [Theory]
    [InlineData("http://accounts.google.com/o/oauth2/token")]
    [InlineData("https://accounts.google.com/o/oauth2/token/")]
    [InlineData("https://accounts.google.com/o/oauth2/v2/token")]
    [InlineData("https://accounts.google.com.evil.example/o/oauth2/token")]
    [InlineData("HTTPS://ACCOUNTS.GOOGLE.COM/O/OAUTH2/TOKEN")]
    public void Only_the_exact_legacy_token_endpoint_is_accepted_beside_the_current_one(string tokenUri)
    {
        var key = FakeServiceAccount.Object();
        key["token_uri"] = tokenUri;

        AssertRefused(ServiceAccountKey.Validate(key.ToJsonString()));
    }

    [Fact]
    public void A_key_without_a_token_endpoint_is_accepted()
    {
        var key = FakeServiceAccount.Object();
        key.Remove("token_uri");

        Assert.True(ServiceAccountKey.Validate(key.ToJsonString()).IsValid);
    }

    [Fact]
    public void Absurdly_deep_nesting_is_refused_without_throwing()
        => AssertRefused(ServiceAccountKey.Validate(new string('[', 2000) + new string(']', 2000)));

    public static TheoryData<string, string> Keys_with_a_lone_surrogate()
    {
        var json = FakeServiceAccount.Json();

        return new TheoryData<string, string>
        {
            // The parser throws an ArgumentException (not a JsonException) for these, so they pin the second catch.
            { "a lone surrogate in an unrelated property", json[..^1] + ",\"x\":\"\ud800\"}" },
            { "an escaped lone high surrogate", json[..^1] + ",\"x\":\"\\ud800\"}" },
            { "an escaped lone low surrogate", json[..^1] + ",\"x\":\"\\udc00\"}" },
            { "an escaped lone surrogate in the client email", json.Replace(FakeServiceAccount.DefaultClientEmail, "\\ud800") },
            // The duplicate-property check reads the names, so an escaped lone surrogate there throws an InvalidOperationException
            // while parsing, not an ArgumentException or a JsonException.
            { "an escaped lone high surrogate in a property name", json[..^1] + ",\"\\ud800\":1}" },
            { "an escaped lone low surrogate in a property name", json[..^1] + ",\"a\\udc00\":1}" },
        };
    }

    [Theory]
    [MemberData(nameof(Keys_with_a_lone_surrogate), DisableDiscoveryEnumeration = true)]
    public void Text_that_is_not_valid_UTF16_is_refused_and_not_normalized_without_throwing(string label, string input)
    {
        Assert.NotNull(label);

        KeyCheck? result = null;
        var normalized = "unset";
        var returned = true;
        var thrown = Record.Exception(() =>
        {
            result = ServiceAccountKey.Validate(input);
            returned = ServiceAccountKey.TryNormalize(input, out normalized);
        });

        Assert.Null(thrown);
        AssertRefused(result);
        Assert.False(returned);
        Assert.Equal("", normalized);
    }

    // ---- Validate: nothing of the input comes back -----------------------------------------------------------

    public static TheoryData<string, string> Refused_inputs_that_carry_the_marker()
    {
        var wrongType = FakeServiceAccount.Object(clientEmail: $"{Marker}@x.example", type: "authorized_user");
        var noPrivateKey = FakeServiceAccount.Object(clientEmail: $"{Marker}@x.example");
        noPrivateKey.Remove("private_key");
        var wrongTypeText = FakeServiceAccount.Object(type: Marker);
        var badPrivateKey = FakeServiceAccount.Object(privateKey: Marker);
        var oversized = FakeServiceAccount.Object(clientEmail: $"{Marker}@x.example");
        oversized["pad"] = new string('x', ServiceAccountKey.MaxBytes);
        var foreignTokenEndpoint = FakeServiceAccount.Object(clientEmail: $"{Marker}@x.example");
        foreignTokenEndpoint["token_uri"] = $"https://{Marker}.example/token";

        return new TheoryData<string, string>
        {
            { "a token endpoint that is not Google's", foreignTokenEndpoint.ToJsonString() },
            { "garbage that is not JSON", $"{Marker} not json" },
            { "JSON cut off inside a value", $"{{\"client_email\":\"{Marker}\"," },
            { "a string of the marker", $"\"{Marker}\"" },
            { "the wrong key type", wrongType.ToJsonString() },
            { "a missing private key", noPrivateKey.ToJsonString() },
            { "the marker as the type", wrongTypeText.ToJsonString() },
            { "the marker as the private key", badPrivateKey.ToJsonString() },
            { "an oversized key", oversized.ToJsonString() },
            { "the marker inside a comment", $"/* {Marker} */ {{}}" },
        };
    }

    [Theory]
    [MemberData(nameof(Refused_inputs_that_carry_the_marker), DisableDiscoveryEnumeration = true)]
    public void A_refused_key_never_echoes_any_of_its_text(string label, string input)
    {
        Assert.NotNull(label);
        Assert.Contains(Marker, input);

        KeyCheck? result = null;
        var thrown = Record.Exception(() => result = ServiceAccountKey.Validate(input));

        Assert.Null(thrown);
        AssertRefused(result);
        Assert.DoesNotContain(Marker, result!.ToString());
        Assert.DoesNotContain(Marker, result.ErrorCode);
        Assert.DoesNotContain(Marker, result.ClientEmail ?? "");
    }

    [Fact]
    public void KeyCheck_carries_nothing_but_the_verdict_the_code_and_the_client_email()
    {
        // KeyCheck is a record, so its generated ToString prints every property. A property holding the key would leak it.
        var properties = typeof(KeyCheck).GetProperties().Select(p => p.Name).Order().ToArray();

        Assert.Equal(["ClientEmail", "ErrorCode", "IsValid"], properties);
    }

    [Fact]
    public void The_size_limit_is_16_KB()
        => Assert.Equal(16 * 1024, ServiceAccountKey.MaxBytes);

    // ---- TryNormalize ----------------------------------------------------------------------------------------

    [Fact]
    public void Normalizing_keeps_only_what_the_loader_needs_so_it_falls_back_to_Googles_own_endpoints()
    {
        var original = FakeServiceAccount.Object();
        original["project_id"] = "Åre – café";
        original["token_uri"] = "http://127.0.0.1:9/x";
        original["universe_domain"] = "internal.example";
        original["Token_Uri"] = "http://127.0.0.1:9/y";
        original["extra_number"] = 12345678901234567L;
        original["extra_flag"] = true;

        Assert.True(ServiceAccountKey.TryNormalize(original.ToJsonString(), out var normalized));

        var expected = new JsonObject();
        foreach (var name in new[] { "type", "project_id", "private_key_id", "private_key", "client_email", "client_id" })
        {
            expected[name] = original[name]!.DeepClone();
        }

        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(normalized)), normalized.Replace(FakeServiceAccount.PrivateKeyPem, "<pem>"));
    }

    [Fact]
    public void Normalizing_keeps_the_kept_properties_in_the_order_they_came()
    {
        var reordered = new JsonObject
        {
            ["client_email"] = FakeServiceAccount.DefaultClientEmail,
            ["auth_uri"] = "https://accounts.google.com/o/oauth2/auth",
            ["private_key"] = FakeServiceAccount.PrivateKeyPem,
            ["type"] = "service_account",
        };

        Assert.True(ServiceAccountKey.TryNormalize(reordered.ToJsonString(), out var normalized));

        Assert.Equal(["client_email", "private_key", "type"], JsonNode.Parse(normalized)!.AsObject().Select(p => p.Key));
    }

    [Fact]
    public void Normalizing_strips_the_BOM_and_surrounding_whitespace_and_returns_plain_JSON()
    {
        Assert.True(ServiceAccountKey.TryNormalize("\uFEFF \r\n" + FakeServiceAccount.Json() + "\r\n\t ", out var normalized));

        Assert.StartsWith("{", normalized);
        Assert.EndsWith("}", normalized);
        Assert.NotNull(JsonNode.Parse(normalized));
    }

    [Fact]
    public void Normalizing_turns_a_double_escaped_private_key_into_real_newlines()
    {
        var json = FakeServiceAccount.Json(privateKey: FakeServiceAccount.PrivateKeyPem.Replace("\n", "\\n"));

        Assert.True(ServiceAccountKey.TryNormalize(json, out var normalized));

        Assert.Equal(FakeServiceAccount.PrivateKeyPem, PrivateKeyOf(normalized));
    }

    [Fact]
    public void Normalizing_turns_a_double_escaped_CRLF_into_a_single_newline()
    {
        var json = FakeServiceAccount.Json(privateKey: FakeServiceAccount.PrivateKeyPem.Replace("\n", "\\r\\n"));

        Assert.True(ServiceAccountKey.TryNormalize(json, out var normalized));

        Assert.Equal(FakeServiceAccount.PrivateKeyPem, PrivateKeyOf(normalized));
    }

    [Fact]
    public void Normalizing_turns_CRLF_inside_the_private_key_into_LF()
    {
        var json = FakeServiceAccount.Json(privateKey: FakeServiceAccount.PrivateKeyPem.Replace("\n", "\r\n"));

        Assert.True(ServiceAccountKey.TryNormalize(json, out var normalized));

        Assert.Equal(FakeServiceAccount.PrivateKeyPem, PrivateKeyOf(normalized));
    }

    [Fact]
    public void A_private_key_that_already_has_real_newlines_keeps_a_literal_backslash_n()
    {
        // Real newlines mean the key is not double-escaped, so a backslash-n in it is content, not a stand-in.
        const string value = "-----BEGIN PRIVATE KEY-----\nline\\nstays\n-----END PRIVATE KEY-----\n";

        Assert.True(ServiceAccountKey.TryNormalize(FakeServiceAccount.Json(privateKey: value), out var normalized));

        Assert.Equal(value, PrivateKeyOf(normalized));
    }

    [Fact]
    public void A_private_key_that_is_not_a_string_is_left_alone()
    {
        var key = FakeServiceAccount.Object();
        key["private_key"] = 5;

        Assert.True(ServiceAccountKey.TryNormalize(key.ToJsonString(), out var normalized));

        Assert.Equal(5, JsonNode.Parse(normalized)!["private_key"]!.GetValue<int>());
    }

    [Fact]
    public void Normalizing_does_not_enforce_the_field_requirements_or_the_size_limit()
    {
        Assert.True(ServiceAccountKey.TryNormalize("{}", out var empty));
        Assert.Equal("{}", empty);

        Assert.True(ServiceAccountKey.TryNormalize(PaddedTo(ServiceAccountKey.MaxBytes + 1), out _));
    }

    [Fact]
    public void Normalizing_is_idempotent()
    {
        var messy = "\uFEFF\r\n" + FakeServiceAccount.Json(privateKey: FakeServiceAccount.PrivateKeyPem.Replace("\n", "\\n"));

        Assert.True(ServiceAccountKey.TryNormalize(messy, out var once));
        Assert.True(ServiceAccountKey.TryNormalize(once, out var twice));

        Assert.Equal(once, twice);
    }

    public static TheoryData<string, string?> Texts_that_cannot_be_normalized() => new()
    {
        { "null", null },
        { "empty", "" },
        { "whitespace", " \r\n " },
        { "a lone BOM", "\uFEFF" },
        { "not json", $"{Marker} not json" },
        { "an array", "[1]" },
        { "a number", "7" },
        { "json null", "null" },
        { "truncated", $"{{\"client_email\":\"{Marker}\"," },
        { "a repeated property", "{\"a\":1,\"a\":2}" },
    };

    [Theory]
    [MemberData(nameof(Texts_that_cannot_be_normalized))]
    public void Text_that_is_not_a_JSON_object_is_not_normalized_and_nothing_comes_back(string label, string? input)
    {
        Assert.NotNull(label);

        var returned = true;
        string normalized = "unset";
        var thrown = Record.Exception(() => returned = ServiceAccountKey.TryNormalize(input, out normalized));

        Assert.Null(thrown);
        Assert.False(returned);
        Assert.Equal("", normalized);
    }

    [Fact]
    public void Normalizing_absurdly_deep_nesting_returns_false_without_throwing()
    {
        var returned = true;
        var normalized = "unset";
        var thrown = Record.Exception(() =>
            returned = ServiceAccountKey.TryNormalize(new string('[', 2000) + new string(']', 2000), out normalized));

        Assert.Null(thrown);
        Assert.False(returned);
        Assert.Equal("", normalized);
    }

    // ---- helpers ---------------------------------------------------------------------------------------------

    private static void AssertRefused(KeyCheck? result)
    {
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Equal("keyInvalid", result.ErrorCode);
        Assert.Null(result.ClientEmail);
    }

    private static string PrivateKeyOf(string json)
        => JsonNode.Parse(json)!["private_key"]!.GetValue<string>();

    /// <summary>A valid key, padded through an extra property until its UTF-8 size is exactly <paramref name="bytes"/>.</summary>
    private static string PaddedTo(int bytes)
    {
        var key = FakeServiceAccount.Object();
        key["pad"] = "";
        var shortBy = bytes - Encoding.UTF8.GetByteCount(key.ToJsonString());
        Assert.True(shortBy >= 0, "the unpadded key is already over the target size");
        key["pad"] = new string('x', shortBy);

        var json = key.ToJsonString();
        Assert.Equal(bytes, Encoding.UTF8.GetByteCount(json));
        return json;
    }
}
