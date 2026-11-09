using Esatto.Umbraco.Backoffice.Seo.Pages;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Esatto.Umbraco.Backoffice.Seo.Tests.Support;

/// <summary>A scan setup over the in-memory web and store: one site (<see cref="Site"/>) with the pages added.</summary>
internal sealed class Rig
{
    public static readonly Guid Site = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public FakeHttp Http { get; } = new();
    public TestStore Store { get; } = new();
    public List<PageCandidate> Candidates { get; } = [];
    public Dictionary<string, string?> Config { get; } = new();
    public ScanCoordinator Coordinator { get; } = new(null!, NullLogger<ScanCoordinator>.Instance);
    public Esatto.Umbraco.Backoffice.Seo.Reporting.ReportCache Cache { get; } = new();
    public ScanRowLock RowLock { get; } = new();

    public Rig Page(string path, string? html = null, bool root = false, string culture = "en", string? url = null, Guid? key = null)
    {
        var abs = url ?? Snap.Host + path;
        if (url is null || url.StartsWith("http")) Http.Html(abs, html ?? Snap.Good(path, culture));
        Candidates.Add(new PageCandidate(key ?? Guid.NewGuid(), "Page " + path, culture, abs, false, true, root, false, []));
        return this;
    }

    public IConfiguration Configuration => new ConfigurationBuilder().AddInMemoryCollection(Config).Build();

    public IPageSource Source()
    {
        var source = Substitute.For<IPageSource>();
        source.GetCandidatesAsync(Site).Returns(_ => Candidates.ToList());
        source.GetSiteCulturesAsync(Site).Returns(["en"]);
        source.GetSitesAsync().Returns([new SiteInfo(Site, "Esatto", ["esatto.se"])]);
        return source;
    }

    public SettingsService Settings() => new(Store, Configuration, TimeProvider.System);

    public ScanRunner Runner()
    {
        var env = new SeoEnvironment(Configuration);
        var fetcher = Http.Fetcher(Config);
        return new ScanRunner(Source(), Settings(), env, Configuration, Store, fetcher, new LinkChecker(fetcher), new SiteProber(fetcher),
            NullLogger<ScanRunner>.Instance);
    }

    public RecheckService Recheck()
    {
        var env = new SeoEnvironment(Configuration);
        var fetcher = Http.Fetcher(Config);
        return new RecheckService(Store, fetcher, new LinkChecker(fetcher), new SnapshotLoader(Store, env), Coordinator, Cache, RowLock);
    }

    public Task<int> RunAsync(CancellationToken ct = default) => Runner().RunAsync(Site, "manual", "Carl", null, null, ct);
}
