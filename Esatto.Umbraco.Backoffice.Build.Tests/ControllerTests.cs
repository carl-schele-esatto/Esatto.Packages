using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.Build.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Build.Tests;

public sealed class ControllerTests
{
    [Fact]
    public void Controller_requires_content_section()
        => Assert.Equal(AuthorizationPolicies.SectionAccessContent, typeof(BuildInfoController).GetCustomAttribute<AuthorizeAttribute>(inherit: false)!.Policy);

    [Fact]
    public void Route_is_esatto_build_info()
        => Assert.EndsWith("/esatto/build-info", typeof(BuildInfoController).GetCustomAttribute<VersionedApiBackOfficeRouteAttribute>()!.Template.TrimEnd('/'));

    [Fact]
    public void Api_group_is_esatto_build()
        => Assert.Equal("Esatto Build", typeof(BuildInfoController).GetCustomAttribute<ApiExplorerSettingsAttribute>()!.GroupName);

    [Fact]
    public void Get_returns_the_services_response()
    {
        var service = ServiceRig.Create(AppContext.BaseDirectory);

        var result = new BuildInfoController(service).Get();

        Assert.Equal(service.Get(), Assert.IsType<OkObjectResult>(result).Value);
    }
}

/// <summary>
/// The TypeScript client (<c>Client/src</c>) reads these names, so the JSON shape of <see cref="BuildInfoResponse"/> is
/// a contract. Umbraco's management API writes camelCase (<see cref="JsonSerializerDefaults.Web"/>).
/// </summary>
public sealed class ApiJsonContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset RunningSince = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<PackageVersion> Packages = [new("uSync", "18.3.0"), new("Esatto.Umbraco.Backoffice.Build", "1.0.0")];

    [Fact]
    public void A_pipeline_build_is_written_in_camel_case()
    {
        var pipeline = new PipelineBuild(
            "42", "0123456789abcdef", "https://ci.example.com/builds/42", "Fix the footer", "https://git.example.com/commit/0123456789abcdef",
            "main", "live", new DateTimeOffset(2026, 11, 20, 7, 30, 0, TimeSpan.Zero),
            new PipelineChanges("fedcba9876543210", "41", "https://ci.example.com/builds/41", Truncated: false,
            [
                new PipelineCommit("0123456789abcdef", "Fix the footer", "The link was wrong.", "Alex Example", new DateTimeOffset(2026, 11, 20, 7, 0, 0, TimeSpan.Zero), "https://git.example.com/commit/0123456789abcdef"),
            ]),
            SchemaVersion: 1);
        var response = new BuildInfoResponse(pipeline, "1.4.0+0123456", "18.3.1", "Production", "web-1", RunningSince, Packages);

        AssertJson("""
            {
              "pipeline": {
                "buildNumber": "42",
                "commit": "0123456789abcdef",
                "buildUrl": "https://ci.example.com/builds/42",
                "commitMessage": "Fix the footer",
                "commitUrl": "https://git.example.com/commit/0123456789abcdef",
                "branch": "main",
                "targetEnvironment": "live",
                "builtAtUtc": "2026-11-20T07:30:00+00:00",
                "changes": {
                  "sinceCommit": "fedcba9876543210",
                  "sinceBuildNumber": "41",
                  "sinceBuildUrl": "https://ci.example.com/builds/41",
                  "truncated": false,
                  "commits": [
                    {
                      "sha": "0123456789abcdef",
                      "subject": "Fix the footer",
                      "body": "The link was wrong.",
                      "author": "Alex Example",
                      "dateUtc": "2026-11-20T07:00:00+00:00",
                      "url": "https://git.example.com/commit/0123456789abcdef"
                    }
                  ]
                },
                "schemaVersion": 1
              },
              "appVersion": "1.4.0+0123456",
              "umbracoVersion": "18.3.1",
              "environment": "Production",
              "instance": "web-1",
              "runningSinceUtc": "2026-11-20T08:00:00+00:00",
              "packages": [
                { "name": "uSync", "version": "18.3.0" },
                { "name": "Esatto.Umbraco.Backoffice.Build", "version": "1.0.0" }
              ],
              "local": null
            }
            """, response);
    }

    [Fact]
    public void Local_changes_are_written_in_camel_case()
    {
        var local = new LocalChanges("feature/footer", 3, ["fedcba9876543210"],
        [
            new PipelineCommit("fedcba9876543210", "Wip footer", null, "Alex Example", new DateTimeOffset(2026, 11, 20, 7, 0, 0, TimeSpan.Zero), null),
        ]);
        var response = new BuildInfoResponse(null, null, "18.3.1", "Development", "dev-laptop", RunningSince, Packages, local);

        AssertJson("""
            {
              "pipeline": null,
              "appVersion": null,
              "umbracoVersion": "18.3.1",
              "environment": "Development",
              "instance": "dev-laptop",
              "runningSinceUtc": "2026-11-20T08:00:00+00:00",
              "packages": [
                { "name": "uSync", "version": "18.3.0" },
                { "name": "Esatto.Umbraco.Backoffice.Build", "version": "1.0.0" }
              ],
              "local": {
                "branch": "feature/footer",
                "uncommittedFiles": 3,
                "unpushedCommits": [ "fedcba9876543210" ],
                "commits": [
                  {
                    "sha": "fedcba9876543210",
                    "subject": "Wip footer",
                    "body": null,
                    "author": "Alex Example",
                    "dateUtc": "2026-11-20T07:00:00+00:00",
                    "url": null
                  }
                ]
              }
            }
            """, response);
    }

    private static void AssertJson(string expected, BuildInfoResponse response)
    {
        var actual = JsonSerializer.SerializeToNode(response, Web);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), actual), actual!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
