using System.Reflection;
using Esatto.Umbraco.Backoffice.Build.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Build.Tests;

public sealed class BuildInfoServiceTests : IDisposable
{
    private const string Stamp = """
        {
          "buildNumber": "42",
          "commit": "0123456789abcdef0123456789abcdef01234567",
          "branch": "main",
          "targetEnvironment": "live"
        }
        """;

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("build-info-service-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void App_version_comes_from_the_host_assembly_not_the_package()
    {
        var host = FakeAssemblies.Create("Example.Site", "7.7.7+abc1234");

        var response = ServiceRig.Create(_root.FullName, hostAssembly: host).Get();

        // Raw, with the +sha: the client reads the commit from it.
        Assert.Equal("7.7.7+abc1234", response.AppVersion);
    }

    [Fact]
    public void A_null_host_assembly_gives_no_app_version_and_no_exception()
    {
        var response = ServiceRig.Create(_root.FullName, hostAssembly: null).Get();

        Assert.Null(response.AppVersion);
    }

    [Fact]
    public void A_host_assembly_without_an_informational_version_gives_no_app_version()
    {
        var response = ServiceRig.Create(_root.FullName, hostAssembly: FakeAssemblies.Create("Example.Site")).Get();

        Assert.Null(response.AppVersion);
    }

    [Fact]
    public void The_public_constructor_reads_the_app_version_from_the_entry_assembly()
    {
        var service = new BuildInfoService(
            ServiceRig.Host(_root.FullName), ServiceRig.UmbracoVersion(), Options.Create(new BuildOptions()), NullLogger<BuildInfoService>.Instance);

        var entry = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Assert.Equal(entry, service.Get().AppVersion);
    }

    [Fact]
    public void Reports_the_environment_instance_and_umbraco_version()
    {
        var before = DateTimeOffset.UtcNow;

        var response = ServiceRig.Create(_root.FullName, environmentName: "Staging").Get();

        Assert.Equal("Staging", response.Environment);
        Assert.Equal(Environment.MachineName, response.Instance);
        Assert.Equal("18.3.1", response.UmbracoVersion);
        Assert.Equal(TimeSpan.Zero, response.RunningSinceUtc.Offset);
        Assert.True(response.RunningSinceUtc <= before);
    }

    [Fact]
    public void The_start_time_is_the_process_start_time_in_utc()
    {
        var local = new DateTime(2026, 11, 20, 9, 12, 0, DateTimeKind.Local);

        var started = BuildInfoService.ReadStartTimeUtc(() => local);

        Assert.Equal(TimeSpan.Zero, started.Offset);
        Assert.Equal(local.ToUniversalTime(), started.UtcDateTime);
    }

    [Fact]
    public void The_start_time_falls_back_to_now_when_the_process_start_time_cannot_be_read()
    {
        // A restricted /proc or a locked-down container makes Process.StartTime throw.
        var before = DateTimeOffset.UtcNow;

        var started = BuildInfoService.ReadStartTimeUtc(() => throw new InvalidOperationException("The process start time is not available."));

        Assert.Equal(TimeSpan.Zero, started.Offset);
        Assert.InRange(started, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Reads_the_stamp_from_the_configured_file_path()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "custom"));
        File.WriteAllText(Path.Combine(_root.FullName, "custom", "stamp.json"), Stamp);

        var response = ServiceRig.Create(_root.FullName, options: new BuildOptions { FilePath = "custom/stamp.json" }).Get();

        Assert.Equal("42", response.Pipeline?.BuildNumber);
        Assert.Equal("live", response.Pipeline?.TargetEnvironment);
        Assert.Null(response.Local);
    }

    [Fact]
    public void Reads_build_info_json_next_to_the_site_by_default()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "build-info.json"), Stamp);

        var response = ServiceRig.Create(_root.FullName).Get();

        Assert.Equal("42", response.Pipeline?.BuildNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_null_empty_or_blank_file_path_falls_back_to_build_info_json(string? filePath)
    {
        // Configuration binding can hand over null; the response is cached for the process lifetime, so it must never throw.
        File.WriteAllText(Path.Combine(_root.FullName, "build-info.json"), Stamp);

        var response = ServiceRig.Create(_root.FullName, options: new BuildOptions { FilePath = filePath! }).Get();

        Assert.Equal("42", response.Pipeline?.BuildNumber);
    }

    [Fact]
    public void Null_packages_are_treated_as_none_configured()
    {
        var response = ServiceRig.Create(_root.FullName, options: new BuildOptions { Packages = null! }).Get();

        // Nothing configured, so only the built-in list: the Esatto family is loaded (this package is part of it).
        Assert.Contains(response.Packages, p => p.Name == "Esatto.Umbraco.Backoffice.Build");
    }

    [Fact]
    public void Null_and_blank_package_entries_are_ignored()
    {
        var name = $"Example.Tools.{Guid.NewGuid():N}";
        _ = FakeAssemblies.Create(name, "2.4.0");

        var response = ServiceRig.Create(_root.FullName, options: new BuildOptions { Packages = [null!, " ", name] }).Get();

        Assert.Contains(new PackageVersion(name, "2.4.0"), response.Packages);
    }

    [Fact]
    public void The_response_is_built_once()
    {
        var path = Path.Combine(_root.FullName, "build-info.json");
        File.WriteAllText(path, Stamp);
        var service = ServiceRig.Create(_root.FullName);

        var first = service.Get();
        File.Delete(path);
        var second = service.Get();

        Assert.NotNull(second.Pipeline);
        Assert.Same(first.Pipeline, second.Pipeline);
        Assert.Equal(first.RunningSinceUtc, second.RunningSinceUtc);
    }

    [Theory]
    [InlineData("Development", false, true)]
    [InlineData("Development", true, false)]
    [InlineData("Production", false, false)]
    [InlineData("Production", true, false)]
    public void Local_changes_only_in_development_and_only_without_a_stamp(string environmentName, bool stampPresent, bool expectLocal)
    {
        // An absolute FilePath wins over the content root, so the stamp can live in the temp folder while the
        // content root stays inside this repository (where there is a git log to read).
        var stamp = Path.Combine(_root.FullName, stampPresent ? "stamp.json" : "missing.json");
        if (stampPresent)
        {
            File.WriteAllText(stamp, Stamp);
        }

        var response = ServiceRig.Create(AppContext.BaseDirectory, environmentName, new BuildOptions { FilePath = stamp }).Get();

        Assert.Equal(stampPresent, response.Pipeline is not null);
        Assert.Equal(expectLocal, response.Local is not null);
    }

    [Fact]
    public void Configured_packages_reach_the_response()
    {
        var name = $"Example.Tools.{Guid.NewGuid():N}";
        _ = FakeAssemblies.Create(name, "2.4.0+abc1234");

        var response = ServiceRig.Create(_root.FullName, options: new BuildOptions { Packages = [name, "Not.Loaded.Anywhere"] }).Get();

        Assert.Contains(new PackageVersion(name, "2.4.0"), response.Packages);
        Assert.DoesNotContain(response.Packages, p => p.Name == "Not.Loaded.Anywhere");
        // The Esatto family is always listed, and this package is part of it.
        Assert.Contains(response.Packages, p => p.Name == "Esatto.Umbraco.Backoffice.Build");
    }
}
