using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Esatto.Umbraco.Backoffice.SharedPreviewLink;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests;

// Locks the data-protection contract shared by PreviewLinkController (mint)
// and PreviewLinkMiddleware (verify): same purpose string, GUID "N" payload,
// time-limited protector. Uses an ephemeral provider so no key ring is needed.
public class TokenContractTests
{
    private static ITimeLimitedDataProtector CreateProtector()
        => new EphemeralDataProtectionProvider()
            .CreateProtector(PreviewLinkController.ProtectorPurpose)
            .ToTimeLimitedDataProtector();

    [Fact]
    public void Protect_then_unprotect_round_trips_the_content_key()
    {
        var protector = CreateProtector();
        var key = Guid.NewGuid();

        var token = protector.Protect(key.ToString("N"), TimeSpan.FromDays(7));
        var restored = Guid.ParseExact(protector.Unprotect(token), "N");

        Assert.Equal(key, restored);
    }

    [Fact]
    public void Tampered_token_throws_CryptographicException()
    {
        var protector = CreateProtector();
        var token = protector.Protect(Guid.NewGuid().ToString("N"), TimeSpan.FromDays(7));

        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        Assert.Throws<CryptographicException>(() => protector.Unprotect(tampered));
    }

    [Fact]
    public void Garbage_token_throws_CryptographicException()
    {
        var protector = CreateProtector();
        Assert.Throws<CryptographicException>(() => protector.Unprotect("not-a-real-token"));
    }

    [Fact]
    public void Expired_token_throws_CryptographicException()
    {
        var protector = CreateProtector();
        var token = protector.Protect(
            Guid.NewGuid().ToString("N"),
            expiration: DateTimeOffset.UtcNow.AddSeconds(-1));

        Assert.Throws<CryptographicException>(() => protector.Unprotect(token));
    }

    [Fact]
    public void Unprotected_non_guid_payload_throws_FormatException_on_parse()
    {
        var protector = CreateProtector();
        var token = protector.Protect("not-a-guid", TimeSpan.FromDays(7));
        var payload = protector.Unprotect(token);

        Assert.Throws<FormatException>(() => Guid.ParseExact(payload, "N"));
    }
}
