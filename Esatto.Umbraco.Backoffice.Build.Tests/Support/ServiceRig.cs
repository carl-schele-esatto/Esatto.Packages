using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Configuration;
using Umbraco.Cms.Core.Semver;

namespace Esatto.Umbraco.Backoffice.Build.Tests.Support;

/// <summary>Builds a <see cref="BuildInfoService"/> from substituted host pieces, through the internal test constructor.</summary>
public static class ServiceRig
{
    public static IHostEnvironment Host(string contentRoot, string environmentName = "Production")
    {
        var host = Substitute.For<IHostEnvironment>();
        host.ContentRootPath.Returns(contentRoot);
        host.EnvironmentName.Returns(environmentName);
        return host;
    }

    /// <summary>Umbraco 18.3.1 with a build suffix, as the CMS reports itself.</summary>
    public static IUmbracoVersion UmbracoVersion()
    {
        var version = Substitute.For<IUmbracoVersion>();
        version.SemanticVersion.Returns(new SemVersion(18, 3, 1, build: "f1ff7ec"));
        return version;
    }

    public static BuildInfoService Create(
        string contentRoot,
        string environmentName = "Production",
        BuildOptions? options = null,
        Assembly? hostAssembly = null)
        => new(Host(contentRoot, environmentName), UmbracoVersion(), Options.Create(options ?? new BuildOptions()),
            NullLogger<BuildInfoService>.Instance, hostAssembly);
}
