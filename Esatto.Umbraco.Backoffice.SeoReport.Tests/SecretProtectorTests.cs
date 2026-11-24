using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>The stored service-account key is ciphertext only, and a ciphertext that cannot be read is reported, never thrown.</summary>
public class SecretProtectorTests
{
    private const string Marker = FakeServiceAccount.LeakMarker;

    private static SecretProtector NewProtector(IDataProtectionProvider? provider = null)
        => new(provider ?? new EphemeralDataProtectionProvider());

    [Fact]
    public void The_purpose_is_the_documented_versioned_string()
        => Assert.Equal("Esatto.Umbraco.Backoffice.SeoReport.ServiceAccountKey.v1", SecretProtector.Purpose);

    [Fact]
    public void A_protected_text_reads_back_unchanged()
    {
        var protector = NewProtector();
        var plain = FakeServiceAccount.Json();

        var cipher = protector.Protect(plain);

        Assert.True(protector.TryUnprotect(cipher, out var roundTripped));
        Assert.Equal(plain, roundTripped);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("Åre – café 日本語 😀")]
    public void Any_text_round_trips(string plain)
    {
        var protector = NewProtector();

        Assert.True(protector.TryUnprotect(protector.Protect(plain), out var roundTripped));
        Assert.Equal(plain, roundTripped);
    }

    [Fact]
    public void The_ciphertext_does_not_contain_the_plaintext()
    {
        var plain = FakeServiceAccount.Json(clientEmail: $"{Marker}@x.example");

        var cipher = NewProtector().Protect(plain);

        Assert.DoesNotContain(plain, cipher);
        Assert.DoesNotContain(Marker, cipher);
        Assert.DoesNotContain("PRIVATE KEY", cipher);
        Assert.DoesNotContain("BEGIN", cipher);
        Assert.NotEqual(plain, cipher);
    }

    [Fact]
    public void The_ciphertext_is_URL_safe_text_with_no_padding_or_whitespace()
    {
        var cipher = NewProtector().Protect(FakeServiceAccount.Json());

        Assert.NotEmpty(cipher);
        Assert.All(cipher, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_', $"unexpected character '{c}'"));
    }

    [Fact]
    public void Protecting_the_same_text_twice_gives_different_ciphertexts()
    {
        var protector = NewProtector();

        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }

    [Fact]
    public void A_ciphertext_from_another_key_ring_cannot_be_read_and_does_not_throw()
    {
        var cipher = NewProtector(new EphemeralDataProtectionProvider()).Protect(FakeServiceAccount.Json());
        var other = NewProtector(new EphemeralDataProtectionProvider());

        var thrown = Record.Exception(() =>
        {
            Assert.False(other.TryUnprotect(cipher, out var plain));
            Assert.Equal("", plain);
        });

        Assert.Null(thrown);
    }

    [Fact]
    public void Two_protectors_over_the_same_provider_read_each_others_ciphertext()
    {
        var provider = new EphemeralDataProtectionProvider();
        var cipher = NewProtector(provider).Protect("secret");

        Assert.True(NewProtector(provider).TryUnprotect(cipher, out var plain));
        Assert.Equal("secret", plain);
    }

    [Fact]
    public void The_ciphertext_is_bound_to_the_purpose_both_ways()
    {
        var provider = new EphemeralDataProtectionProvider();

        var ours = provider.CreateProtector(SecretProtector.Purpose).Protect("ours");
        var foreign = provider.CreateProtector("some other purpose").Protect("foreign");

        Assert.True(NewProtector(provider).TryUnprotect(ours, out var plain));
        Assert.Equal("ours", plain);
        Assert.False(NewProtector(provider).TryUnprotect(foreign, out _));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!not base64!!!")]
    [InlineData("AAAA")]
    [InlineData("A")]
    [InlineData("====")]
    [InlineData(Marker)]
    public void Text_that_is_not_a_ciphertext_is_unreadable_not_an_error(string cipher)
    {
        var protector = NewProtector();

        var thrown = Record.Exception(() =>
        {
            Assert.False(protector.TryUnprotect(cipher, out var plain));
            Assert.Equal("", plain);
        });

        Assert.Null(thrown);
    }

    [Fact]
    public void A_null_ciphertext_is_unreadable_not_an_error()
    {
        var thrown = Record.Exception(() =>
        {
            Assert.False(NewProtector().TryUnprotect(null, out var plain));
            Assert.Equal("", plain);
        });

        Assert.Null(thrown);
    }

    [Fact]
    public void A_very_long_garbage_text_is_unreadable_not_an_error()
    {
        var thrown = Record.Exception(() =>
        {
            Assert.False(NewProtector().TryUnprotect(new string('A', 200_000), out var plain));
            Assert.Equal("", plain);
        });

        Assert.Null(thrown);
    }

    [Fact]
    public void A_truncated_or_tampered_ciphertext_is_unreadable_not_an_error()
    {
        var protector = NewProtector();
        var cipher = protector.Protect(FakeServiceAccount.Json());

        var middle = cipher.Length / 2;
        var flipped = cipher[..middle] + (cipher[middle] == 'A' ? 'B' : 'A') + cipher[(middle + 1)..];
        var truncated = cipher[..^8];

        var thrown = Record.Exception(() =>
        {
            Assert.False(protector.TryUnprotect(flipped, out var plainFlipped));
            Assert.Equal("", plainFlipped);
            Assert.False(protector.TryUnprotect(truncated, out var plainTruncated));
            Assert.Equal("", plainTruncated);
        });

        Assert.Null(thrown);
    }

    /// <summary>A key ring whose backend fails while reading (an unreachable blob container or Redis, say) with the exception given.</summary>
    internal static IDataProtectionProvider BrokenKeyRing(Exception failure)
    {
        var protector = Substitute.For<IDataProtector>();
        protector.Protect(Arg.Any<byte[]>()).Returns(call => call.Arg<byte[]>());
        protector.Unprotect(Arg.Any<byte[]>()).Returns(_ => throw failure);
        var provider = Substitute.For<IDataProtectionProvider>();
        provider.CreateProtector(Arg.Any<string>()).Returns(protector);
        return provider;
    }

    public static TheoryData<string> Key_ring_failures() => new()
    {
        nameof(InvalidOperationException),
        nameof(IOException),
        nameof(TimeoutException),
        nameof(TaskCanceledException),
        nameof(UnauthorizedAccessException),
    };

    private static Exception Failure(string name) => name switch
    {
        nameof(InvalidOperationException) => new InvalidOperationException($"ring {Marker}"),
        nameof(IOException) => new IOException($"ring {Marker}"),
        nameof(TimeoutException) => new TimeoutException($"ring {Marker}"),
        nameof(TaskCanceledException) => new TaskCanceledException($"ring {Marker}"),
        nameof(UnauthorizedAccessException) => new UnauthorizedAccessException($"ring {Marker}"),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(Key_ring_failures))]
    public void A_key_ring_that_fails_to_read_is_unreadable_not_an_error(string failure)
    {
        var protector = NewProtector(BrokenKeyRing(Failure(failure)));

        var thrown = Record.Exception(() =>
        {
            Assert.False(protector.TryUnprotect("AAAA", out var plain));
            Assert.Equal("", plain);
        });

        Assert.Null(thrown);
    }

    /// <summary>The real Data Protection stack over key storage that cannot be read (an unreachable blob container or Redis, say).</summary>
    internal static IDataProtectionProvider UnreachableKeyStorage()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        services.Configure<KeyManagementOptions>(options => options.XmlRepository = new UnreachableRepository());
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }

    [Fact]
    public void Key_storage_that_cannot_be_read_is_unreadable_not_an_error()
    {
        var protector = NewProtector(UnreachableKeyStorage());
        var cipher = NewProtector().Protect(FakeServiceAccount.Json());

        var thrown = Record.Exception(() =>
        {
            Assert.False(protector.TryUnprotect(cipher, out var plain));
            Assert.Equal("", plain);
        });

        Assert.Null(thrown);
    }

    private sealed class UnreachableRepository : IXmlRepository
    {
        public IReadOnlyCollection<XElement> GetAllElements() => throw new IOException($"key storage unreachable {Marker}");

        public void StoreElement(XElement element, string friendlyName) => throw new IOException($"key storage unreachable {Marker}");
    }

    [Fact]
    public void Protecting_null_is_refused_without_putting_text_in_the_message()
    {
        var thrown = Assert.Throws<ArgumentNullException>(() => NewProtector().Protect(null!));

        Assert.DoesNotContain(Marker, thrown.Message);
    }
}
