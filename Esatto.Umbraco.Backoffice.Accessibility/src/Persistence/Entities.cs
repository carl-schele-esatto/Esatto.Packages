using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Esatto.Umbraco.Backoffice.Accessibility.Persistence;

// The package's tables, all prefixed esattoA11y. Public because NPoco and the migration need them;
// nothing outside the package should depend on their shape.

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = false)]
[ExplicitColumns]
public sealed class SettingsEntity
{
    public const string TableName = "esattoA11ySettings";

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
    public const string TableName = "esattoA11yScan";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("siteKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_esattoA11yScan_siteKey_startedUtc", ForColumns = "siteKey,startedUtc")]
    public Guid SiteKey { get; set; }

    [Column("startedUtc")]
    public DateTime StartedUtc { get; set; }

    [Column("finishedUtc")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? FinishedUtc { get; set; }

    /// <summary>"V22" etc.</summary>
    [Column("version")]
    [Length(8)]
    public string Version { get; set; } = string.Empty;

    /// <summary>"AA" etc.</summary>
    [Column("level")]
    [Length(8)]
    public string Level { get; set; } = string.Empty;

    [Column("mobile")]
    public bool Mobile { get; set; }

    [Column("userName")]
    [Length(256)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>running | complete | incomplete.</summary>
    [Column("status")]
    [Length(16)]
    public string Status { get; set; } = ScanStatus.Running;

    [Column("pagesTotal")] public int PagesTotal { get; set; }
    [Column("pagesDone")] public int PagesDone { get; set; }
    [Column("pagesSkipped")] public int PagesSkipped { get; set; }
    [Column("pagesFailed")] public int PagesFailed { get; set; }

    /// <summary>Whole percent at the time the scan finished; null when nothing could be scored.</summary>
    [Column("score")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public int? Score { get; set; }

    [Column("passCount")] public int PassCount { get; set; }
    [Column("failCount")] public int FailCount { get; set; }
    [Column("unverifiedCount")] public int UnverifiedCount { get; set; }
    [Column("naCount")] public int NaCount { get; set; }

    /// <summary>Distinct document links (PDF/Office) found on the scanned pages, one per line.</summary>
    [Column("documentLinks")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? DocumentLinks { get; set; }
}

public static class ScanStatus
{
    public const string Running = "running";
    public const string Complete = "complete";
    public const string Incomplete = "incomplete";
}

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class ScanPageEntity
{
    public const string TableName = "esattoA11yScanPage";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("scanId")]
    [Index(IndexTypes.NonClustered, Name = "IX_esattoA11yScanPage_scanId", ForColumns = "scanId")]
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

    [Column("viewport")]
    public int Viewport { get; set; }

    /// <summary>ok | skipped | failed.</summary>
    [Column("status")]
    [Length(16)]
    public string Status { get; set; } = PageStatus.Ok;

    [Column("reason")]
    [Length(512)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Reason { get; set; }

    /// <summary>Comma list of the WCAG criteria at least one passing axe rule covered on this page.</summary>
    [Column("passedCriteria")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? PassedCriteria { get; set; }

    /// <summary>Document links (PDF/Office) found on this page, one per line; merged into the scan at finish.</summary>
    [Column("documentLinks")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? DocumentLinks { get; set; }
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
public sealed class FindingEntity
{
    public const string TableName = "esattoA11yFinding";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("scanId")]
    [Index(IndexTypes.NonClustered, Name = "IX_esattoA11yFinding_scanId", ForColumns = "scanId")]
    public int ScanId { get; set; }

    [Column("scanPageId")]
    public int ScanPageId { get; set; }

    [Column("ruleId")]
    [Length(128)]
    public string RuleId { get; set; } = string.Empty;

    [Column("help")]
    [Length(256)]
    public string Help { get; set; } = string.Empty;

    [Column("helpUrl")]
    [Length(512)]
    public string HelpUrl { get; set; } = string.Empty;

    /// <summary>Comma list of WCAG criterion ids, e.g. "2.4.4,4.1.2"; empty for best-practice rules.</summary>
    [Column("criteria")]
    [Length(256)]
    public string Criteria { get; set; } = string.Empty;

    [Column("impact")]
    [Length(16)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Impact { get; set; }

    /// <summary>violation | incomplete.</summary>
    [Column("kind")]
    [Length(16)]
    public string Kind { get; set; } = FindingKind.Violation;

    [Column("selector")]
    [Length(1024)]
    public string Selector { get; set; } = string.Empty;

    [Column("html")]
    [Length(1024)]
    public string Html { get; set; } = string.Empty;

    [Column("mediaKey")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public Guid? MediaKey { get; set; }
}

public static class FindingKind
{
    public const string Violation = "violation";
    public const string Incomplete = "incomplete";
}

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class FalsePositiveEntity
{
    public const string TableName = "esattoA11yFalsePositive";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("siteKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_esattoA11yFalsePositive_siteKey", ForColumns = "siteKey")]
    public Guid SiteKey { get; set; }

    [Column("ruleId")]
    [Length(128)]
    public string RuleId { get; set; } = string.Empty;

    [Column("contentKey")]
    public Guid ContentKey { get; set; }

    /// <summary>The element's selector, or null for "every occurrence of this rule on the page".</summary>
    [Column("selector")]
    [Length(1024)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Selector { get; set; }

    [Column("note")]
    [Length(1024)]
    public string Note { get; set; } = string.Empty;

    [Column("userName")]
    [Length(256)]
    public string UserName { get; set; } = string.Empty;

    [Column("createdUtc")]
    public DateTime CreatedUtc { get; set; }
}

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class ManualResultEntity
{
    public const string TableName = "esattoA11yManualResult";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("siteKey")]
    [Index(IndexTypes.UniqueNonClustered, Name = "IX_esattoA11yManualResult_siteKey_criterionId", ForColumns = "siteKey,criterionId")]
    public Guid SiteKey { get; set; }

    [Column("criterionId")]
    [Length(16)]
    public string CriterionId { get; set; } = string.Empty;

    /// <summary>pass | fail | na.</summary>
    [Column("status")]
    [Length(16)]
    public string Status { get; set; } = string.Empty;

    [Column("note")]
    [Length(2048)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Note { get; set; }

    [Column("userName")]
    [Length(256)]
    public string UserName { get; set; } = string.Empty;

    [Column("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }
}
