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

    private static CrossContentCaseListClient CreateWithTypes(
        StubHttpMessageHandler handler, params (string Alias, string? Label)[] types)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://target.example/") };
        var opts = new CrossContentOptions();
        foreach (var (alias, label) in types)
            opts.ContentTypes.Add(new CrossContentContentTypeOption { Alias = alias, Label = label });
        return new CrossContentCaseListClient(http, Options.Create(opts));
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

    [Fact]
    public async Task ListCasesAsync_LegacyMode_TagsItemsAsCasePage()
    {
        const string body = """
            { "items": [ { "id": "11111111-1111-1111-1111-111111111111", "name": "Case A" } ] }
            """;
        var client = Create(StubHttpMessageHandler.Json(HttpStatusCode.OK, body));

        var result = await client.ListCasesAsync(CancellationToken.None);

        Assert.Equal("casePage", result[0].Type);
    }

    [Fact]
    public async Task ListCasesAsync_MultiType_TagsEachItemWithItsType()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            var q = req.RequestUri!.Query;
            var body = q.Contains("articlePage")
                ? """{ "items": [ { "id": "22222222-2222-2222-2222-222222222222", "name": "Article A" } ] }"""
                : """{ "items": [ { "id": "11111111-1111-1111-1111-111111111111", "name": "Case A" } ] }""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        });
        var client = CreateWithTypes(handler, ("casePage", "Cases"), ("articlePage", "Articles"));

        var result = await client.ListCasesAsync(CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(2, result.Count);
        // Merge order follows config order: casePage first, then articlePage.
        Assert.Equal("Case A", result[0].Title);
        Assert.Equal("casePage", result[0].Type);
        Assert.Equal("Article A", result[1].Title);
        Assert.Equal("articlePage", result[1].Type);
    }

    [Fact]
    public async Task ListCasesAsync_MultiType_OneFailingTypeDoesNotKillOthers()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            var q = req.RequestUri!.Query;
            if (q.Contains("articlePage"))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            var body = """{ "items": [ { "id": "11111111-1111-1111-1111-111111111111", "name": "Case A" } ] }""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        });
        var client = CreateWithTypes(handler, ("casePage", null), ("articlePage", null));

        var result = await client.ListCasesAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("casePage", result[0].Type);
    }

    [Fact]
    public async Task ListCasesAsync_MultiType_SkipsBlankAlias_WithoutRequesting()
    {
        const string body = """{ "items": [ { "id": "11111111-1111-1111-1111-111111111111", "name": "Case A" } ] }""";
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        var client = CreateWithTypes(handler, ("casePage", null), ("   ", null));

        var result = await client.ListCasesAsync(CancellationToken.None);

        // Blank alias issues no request; only the valid type is fetched.
        Assert.Equal(1, handler.CallCount);
        Assert.Single(result);
        Assert.Equal("Case A", result[0].Title);
        Assert.Equal("casePage", result[0].Type);
    }
}
