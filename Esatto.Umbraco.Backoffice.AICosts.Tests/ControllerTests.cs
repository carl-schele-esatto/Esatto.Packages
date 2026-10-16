using System.Reflection;
using Esatto.Umbraco.Backoffice.AICosts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.AI.Core.Analytics;
using Umbraco.AI.Web.Authorization;
using Xunit;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

public class ControllerTests
{
    [Fact]
    public void Gated_by_the_ai_section_policy()
    {
        // ManagementApiControllerBase adds its own [Authorize] (backoffice access); ours narrows it to the AI section.
        Assert.Contains(typeof(AICostsController).GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == AIAuthorizationPolicies.SectionAccessAI);
    }

    [Fact]
    public async Task Unknown_dimension_is_a_bad_request()
    {
        var options = Substitute.For<IOptionsMonitor<AICostOptions>>();
        options.CurrentValue.Returns(new AICostOptions());
        var queries = new AICostQueries(Usage.EmptyAnalytics());
        var clock = new TestClock(DateTime.UtcNow);
        var controller = new AICostsController(
            new AICostReportService(queries, Substitute.For<IExchangeRateProvider>(), options, Options.Create(new AIAnalyticsOptions()), clock),
            new AICostBreakdownService(queries, options, clock));

        Assert.IsType<BadRequestObjectResult>(await controller.GetBreakdown("model", 30, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.GetBreakdown("User", 30, CancellationToken.None));
    }
}
