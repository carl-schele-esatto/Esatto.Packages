using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.Build;

/// <summary>What <c>GET esatto/build-info</c> returns, and what the dashboard shows. The TypeScript client reads these names.</summary>
public sealed record BuildInfoResponse(
    PipelineBuild? Pipeline,
    string? AppVersion,
    string UmbracoVersion,
    string Environment,
    string Instance,
    DateTimeOffset RunningSinceUtc,
    IReadOnlyList<PackageVersion> Packages,
    LocalChanges? Local = null);

/// <summary>
/// What is running right now, for Content ▸ Build. The response is built once: nothing in it changes without a
/// restart, and a deploy is always a restart. The exception is the git log of a local build, which is read on every
/// call because you commit while the site is running.
/// </summary>
public sealed class BuildInfoService
{
    private readonly Lazy<BuildInfoResponse> _response;
    private readonly string _contentRoot;
    private readonly bool _isDevelopment;
    private readonly ILogger<BuildInfoService> _logger;

    // The public constructor is the one the DI container uses (it only considers public ones); the internal one lets
    // tests choose which assembly stands in for the host app.
    public BuildInfoService(
        IHostEnvironment environment,
        IUmbracoVersion umbracoVersion,
        IOptions<BuildOptions> options,
        ILogger<BuildInfoService> logger)
        : this(environment, umbracoVersion, options, logger, Assembly.GetEntryAssembly())
    {
    }

    internal BuildInfoService(
        IHostEnvironment environment,
        IUmbracoVersion umbracoVersion,
        IOptions<BuildOptions> options,
        ILogger<BuildInfoService> logger,
        Assembly? hostAssembly)
    {
        _contentRoot = environment.ContentRootPath;
        _isDevelopment = environment.IsDevelopment();
        _logger = logger;
        _response = new Lazy<BuildInfoResponse>(() =>
        {
            var settings = options.Value;
            // Configuration binding can hand over null. The response is cached for the process lifetime (a Lazy keeps
            // an exception too), so a bad setting must fall back to the default rather than throw.
            var file = string.IsNullOrWhiteSpace(settings.FilePath) ? PipelineBuildReader.DefaultFileName : settings.FilePath;
            return new BuildInfoResponse(
                PipelineBuildReader.Read(Path.Combine(_contentRoot, file), logger),
                // The host app's version, not this package's. Built locally the SDK puts the commit after the "+"
                // (SourceLink reads it from .git); on a deployed site the client reads it from there too.
                hostAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                umbracoVersion.SemanticVersion.ToSemanticStringWithoutBuild(),
                environment.EnvironmentName,
                Environment.MachineName,
                ReadStartTimeUtc(CurrentProcessStartTime),
                PackageCatalog.Resolve(AppDomain.CurrentDomain.GetAssemblies(), settings.Packages));
        });
    }

    public BuildInfoResponse Get()
    {
        var response = _response.Value;

        // Only a local build reads git, and only when the pipeline left no stamp: a deployed site has no .git.
        return response.Pipeline is null && _isDevelopment
            ? response with { Local = LocalGitLog.Read(_contentRoot, _logger) }
            : response;
    }

    /// <summary>
    /// When the process started, in UTC; now when that cannot be read. <c>Process.StartTime</c> throws where /proc is
    /// restricted or in a locked-down container, and a dashboard detail must not take the whole response down.
    /// </summary>
    internal static DateTimeOffset ReadStartTimeUtc(Func<DateTime> startTime)
    {
        try
        {
            return new DateTimeOffset(startTime().ToUniversalTime());
        }
        catch (Exception)
        {
            return DateTimeOffset.UtcNow;
        }
    }

    private static DateTime CurrentProcessStartTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime;
    }
}
