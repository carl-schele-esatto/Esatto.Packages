using System.Net;
using Esatto.Umbraco.Backoffice.CrossContent.Tests.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CrossContent.Tests;

public class CrossContentCaseListClientTests
{
    private static CrossContentCaseListClient Create(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://target.example/") };
        var options = Options.Create(new CrossContentOptions());
        return new CrossContentCaseListClient(http, options);
    }

    [Fact]
    public async Task ListCasesAsync_MapsIdAndName_FromDeliveryApiItems()
    {
        const string body = """
            { "items": [
                { "id": "11111111-1111-1111-1111-111111111111", "name": "Case A" },
                { "id": "22222222-2222-2222-2222-222222222222", "name": "Case B" }
            ] }
            """;
        var client = Create(StubHttpMessageHandler.Json(HttpStatusCode.OK, body));

        var result = await client.ListCasesAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), result[0].Key);
        Assert.Equal("Case A", result[0].Title);
    }

    [Fact]
    public async Task ListCasesAsync_ReturnsEmpty_OnNonSuccess()
    {
        var client = Create(StubHttpMessageHandler.Json(HttpStatusCode.Unauthorized, "{}"));

        var result = await client.ListCasesAsync(CancellationToken.None);

        Assert.Empty(result);
    }
}
