using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Esatto.Umbraco.Backoffice.Seo.Persistence;

// The package's tables, all prefixed esattoSeo. Public because NPoco and the migration need them;
// nothing outside the package should depend on their shape. No findings table: findings are derived
// from the stored page facts at read time (see Evaluation/Evaluator).

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = false)]
[ExplicitColumns]
public sealed class SettingsEntity
{
    public const string TableName = "esattoSeoSettings";

    /// <summary>Always 1: the table holds a single row.</summary>
    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = false)]
    public int Id { get; set; } = 1;

    [Column("json")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    public string Json { get; set; } = "{}";

    [Column("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }
}

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class ScanEntity
{
    public const string TableName = "esattoSeoScan";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("siteKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_esattoSeoScan_siteKey_startedUtc", ForColumns = "siteKey,startedUtc")]
    public Guid SiteKey { get; set; }

    [Column("startedUtc")]
    public DateTime StartedUtc { get; set; }

    [Column("finishedUtc")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? FinishedUtc { get; set; }

    /// <summary>manual | scheduled.</summary>
    [Column("trigger")]
    [Length(16)]
    public string Trigger { get; set; } = ScanTrigger.Manual;

    [Column("userName")]
    [Length(256)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>See <see cref="ScanStatus"/>.</summary>
    [Column("status")]
    [Length(16)]
    public string Status { get; set; } = ScanStatus.Running;

    [Column("pagesTotal")] public int PagesTotal { get; set; }
    [Column("pagesDone")] public int PagesDone { get; set; }
    [Column("pagesFailed")] public int PagesFailed { get; set; }
    [Column("pagesSkipped")] public int PagesSkipped { get; set; }

    /// <summary>0–100 when the scan finished; null when it didn't.</summary>
    [Column("score")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public int? Score { get; set; }

    [Column("scoreVersion")] public int ScoreVersion { get; set; }
    [Column("critical")] public int Critical { get; set; }
    [Column("warning")] public int Warning { get; set; }
    [Column("notice")] public int Notice { get; set; }

    /// <summary>The site's cultures at scan time, comma-separated.</summary>
    [Column("cultures")]
    [Length(512)]
    public string Cultures { get; set; } = string.Empty;

    /// <summary>
    /// Hosts that may receive configured request headers (the basic-auth secret), comma-separated: bound
    /// domains and a configured base URL — never a host taken from a backoffice request.
    /// </summary>
    [Column("trustedHosts")]
    [Length(1024)]
    public string TrustedHosts { get; set; } = string.Empty;

    /// <summary>Serialized SiteProbe (robots, sitemap, https, hosts). Cleared when the scan is pruned.</summary>
    [Column("probeJson")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? ProbeJson { get; set; }

    /// <summary>Serialized link statuses (normalised URL → LinkStatus). Cleared when the scan is pruned.</summary>
    [Column("linksJson")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? LinksJson { get; set; }

    /// <summary>Why a scan failed or was skipped, shown on the Overview.</summary>
    [Column("message")]
    [Length(1024)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Message { get; set; }
}

public static class ScanTrigger
{
    public const string Manual = "manual";
    public const string Scheduled = "scheduled";
}

public static class ScanStatus
{
    public const string Running = "running";
    public const string Complete = "complete";

    /// <summary>The site restarted mid-scan.</summary>
    public const string Incomplete = "incomplete";

    /// <summary>Couldn't run (no base URL, every page failing). <see cref="ScanEntity.Message"/> says why.</summary>
    public const string Failed = "failed";

    public const string Cancelled = "cancelled";
}

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class PageEntity
{
    public const string TableName = "esattoSeoPage";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("scanId")]
    [Index(IndexTypes.NonClustered, Name = "IX_esattoSeoPage_scanId", ForColumns = "scanId")]
    public int ScanId { get; set; }

    [Column("contentKey")]
    public Guid ContentKey { get; set; }

    [Column("culture")]
    [Length(16)]
    public string Culture { get; set; } = string.Empty;

    [Column("name")]
    [Length(256)]
    public string Name { get; set; } = string.Empty;

    [Column("url")]
    [Length(2048)]
    public string Url { get; set; } = string.Empty;

    [Column("isRoot")] public bool IsRoot { get; set; }
    [Column("isVariant")] public bool IsVariant { get; set; }

    /// <summary>The node's other published cultures, comma-separated.</summary>
    [Column("otherCultures")]
    [Length(512)]
    public string OtherCultures { get; set; } = string.Empty;

    /// <summary>ok | skipped | failed.</summary>
    [Column("status")]
    [Length(16)]
    public string Status { get; set; } = PageStatus.Ok;

    [Column("reason")]
    [Length(512)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Reason { get; set; }

    /// <summary>Serialized PageFacts; null for skipped pages and for pruned (old) scans.</summary>
    [Column("factsJson")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? FactsJson { get; set; }
}

public static class PageStatus
{
    public const string Ok = "ok";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
}

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class IgnoreEntity
{
    public const string TableName = "esattoSeoIgnore";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("siteKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_esattoSeoIgnore_siteKey", ForColumns = "siteKey")]
    public Guid SiteKey { get; set; }

    [Column("checkId")]
    [Length(64)]
    public string CheckId { get; set; } = string.Empty;

    /// <summary>Null = the whole check on the site.</summary>
    [Column("contentKey")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public Guid? ContentKey { get; set; }

    /// <summary>Null = every culture of the page.</summary>
    [Column("culture")]
    [Length(16)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Culture { get; set; }

    [Column("note")]
    [Length(1024)]
    public string Note { get; set; } = string.Empty;

    [Column("userName")]
    [Length(256)]
    public string UserName { get; set; } = string.Empty;

    [Column("createdUtc")]
    public DateTime CreatedUtc { get; set; }
}
