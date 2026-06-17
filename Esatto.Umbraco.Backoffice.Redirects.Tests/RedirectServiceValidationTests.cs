using NSubstitute;
using Umbraco.Cms.Infrastructure.Scoping;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Redirects.Tests;

public class RedirectServiceValidationTests
{
    private static RedirectService NewService()
        => new RedirectService(Substitute.For<IScopeProvider>());

    [Fact]
    public async Task Create_EmptyOldPath_ReturnsRequiredError()
    {
        var error = await NewService().TryCreateAsync(new CreateRedirectRequest("", "/new"));
        Assert.Equal("Old URL is required.", error);
    }

    [Fact]
    public async Task Create_InvalidDestination_ReturnsDestinationError()
    {
        var error = await NewService().TryCreateAsync(new CreateRedirectRequest("/old", "notaurl"));
        Assert.Equal("New URL must be a relative path (/...) or absolute URL.", error);
    }

    [Fact]
    public async Task Create_SameOldAndNew_ReturnsSameError()
    {
        var error = await NewService().TryCreateAsync(new CreateRedirectRequest("/dup", "/dup"));
        Assert.Equal("Old URL and New URL cannot be the same.", error);
    }

    [Fact]
    public async Task Update_EmptyOldPath_ReturnsRequiredError()
    {
        var error = await NewService().TryUpdateAsync(1, new UpdateRedirectRequest("", "/new"));
        Assert.Equal("Old URL is required.", error);
    }
}
