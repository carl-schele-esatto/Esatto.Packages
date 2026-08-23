using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentEndpointHandlerTests
{
    private static ConsentEndpointHandler Build(int policyVersion = 1, int throttleRequestsPerMinute = 10)
    {
        IOptions<CookieBannerOptions> options = Options.Create(new CookieBannerOptions
        {
            PolicyVersion = policyVersion,
            ThrottleRequestsPerMinute = throttleRequestsPerMinute,
        });

        return new ConsentEndpointHandler(
            new ConsentCookieWriter(options),
            new ConsentThrottle(options, TimeProvider.System),
            options);
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.4");
        return context;
    }

    private static ConsentStateResponse Ok(IResult result)
        => Assert.IsType<Ok<ConsentStateResponse>>(result).Value!;

    [Fact]
    public void Accepting_returns_the_stored_state_and_writes_the_cookie()
    {
        // Pins the response shape consent.js reads back (version + categories + id) and that the
        // endpoint really writes a cookie rather than trusting the browser to.
        ConsentEndpointHandler handler = Build();
        DefaultHttpContext context = NewContext();

        IResult result = handler.Handle(new ConsentRequest(["statistics", "marketing"], "accept-all"), context);

        ConsentStateResponse response = Ok(result);
        Assert.Equal(1, response.Version);
        Assert.Equal(["marketing", "statistics"], response.Categories);
        Assert.NotEmpty(response.ConsentId);
        Assert.NotEmpty(context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public void Rejecting_stores_no_categories()
    {
        // Reject-all must produce an empty grant set, never a silent accept-all.
        ConsentEndpointHandler handler = Build();

        IResult result = handler.Handle(new ConsentRequest([], "reject-all"), NewContext());

        Assert.Empty(Ok(result).Categories);
    }

    [Fact]
    public void Unknown_categories_are_discarded_rather_than_trusted()
    {
        // The body is untrusted: an invented category is dropped and "necessary" is never echoed
        // back as a granted choice.
        ConsentEndpointHandler handler = Build();

        IResult result = handler.Handle(
            new ConsentRequest(["statistics", "telepathy", "necessary"], "custom"),
            NewContext());

        Assert.Equal(["statistics"], Ok(result).Categories);
    }

    [Fact]
    public void An_unknown_action_is_rejected()
    {
        // An unrecognised action is a hard 400, so a typo in the client cannot write a cookie
        // whose provenance nobody can explain.
        ConsentEndpointHandler handler = Build();

        IResult result = handler.Handle(
            new ConsentRequest([], "definitely-not-an-action"),
            NewContext());

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public void The_response_records_the_current_policy_version()
    {
        // PolicyVersion comes from options, not a constant: bumping it is what re-prompts visitors.
        ConsentEndpointHandler handler = Build(policyVersion: 7);

        IResult result = handler.Handle(new ConsentRequest([], "reject-all"), NewContext());

        Assert.Equal(7, Ok(result).Version);
    }

    [Fact]
    public void Requests_beyond_the_throttle_budget_get_429()
    {
        // Preserves the status code the removed ASP.NET Core rate limiter returned, now without
        // requiring UseRateLimiter() to be threaded through the consumer's Umbraco pipeline.
        ConsentEndpointHandler handler = Build(throttleRequestsPerMinute: 1);

        Assert.IsType<Ok<ConsentStateResponse>>(handler.Handle(new ConsentRequest([], "reject-all"), NewContext()));

        IResult second = handler.Handle(new ConsentRequest([], "reject-all"), NewContext());

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            Assert.IsType<StatusCodeHttpResult>(second).StatusCode);
    }

    [Fact]
    public void The_throttle_is_consulted_before_the_action_is_validated()
    {
        // Order matters: a flood of invalid actions must consume budget too, otherwise the cheap
        // rejection path is an unmetered way to hammer the endpoint.
        ConsentEndpointHandler handler = Build(throttleRequestsPerMinute: 1);

        Assert.IsType<BadRequest<string>>(handler.Handle(new ConsentRequest([], "nonsense"), NewContext()));

        IResult second = handler.Handle(new ConsentRequest([], "reject-all"), NewContext());

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            Assert.IsType<StatusCodeHttpResult>(second).StatusCode);
    }
}
