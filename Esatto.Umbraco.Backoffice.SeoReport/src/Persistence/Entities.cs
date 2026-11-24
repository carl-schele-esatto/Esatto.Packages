using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;
using ReportPeriod = Esatto.Umbraco.Backoffice.SeoReport.Reporting.Period;

namespace Esatto.Umbraco.Backoffice.SeoReport.Persistence;

// The package's tables, all prefixed esattoSeoReport. Public because NPoco and the migration need them; nothing outside
// the package should depend on their shape. Column names are camelCase, as in the other Esatto packages, and every
// hand-written statement quotes them through the database's syntax provider (key, name, owner and text are reserved or
// keyword-like in SQL Server).

/// <summary>One archived month: the report JSON, when it was fetched, and the contract version that wrote it.</summary>
[TableName(TableName)]
[PrimaryKey("period", AutoIncrement = false)]
[ExplicitColumns]
public sealed class ReportEntity
{
    public const string TableName = "esattoSeoReportReport";

    /// <summary>The length of <c>YYYY-MM</c>.</summary>
    public const int PeriodLength = 7;

    /// <summary><c>YYYY-MM</c>. Write it with <see cref="From"/>, read it with <see cref="ToPeriod"/>.</summary>
    [Column("period")]
    [PrimaryKeyColumn(AutoIncrement = false)]
    [Length(PeriodLength)]
    public string Period { get; set; } = string.Empty;

    [Column("json")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    public string Json { get; set; } = "{}";

    /// <summary>When the month was fetched from Google, UTC.</summary>
    [Column("fetchedUtc")]
    public DateTime FetchedUtc { get; set; }

    /// <summary>The report contract version that wrote <see cref="Json"/>; an older one counts as missing.</summary>
    [Column("contractVersion")]
    public int ContractVersion { get; set; }

    /// <summary>A row for <paramref name="period"/>; the typed period is what guarantees the key is <c>YYYY-MM</c>.</summary>
    public static ReportEntity From(ReportPeriod period, string json, DateTime fetchedUtc, int contractVersion)
        => new() { Period = period.ToString(), Json = json, FetchedUtc = fetchedUtc, ContractVersion = contractVersion };

    /// <summary>The row's period. Throws <see cref="ArgumentException"/> unless <see cref="Period"/> is exactly <c>YYYY-MM</c>.</summary>
    public ReportPeriod ToPeriod() => ReportPeriod.Parse(Period);
}

/// <summary>The note a user wrote for one month.</summary>
[TableName(TableName)]
[PrimaryKey("period", AutoIncrement = false)]
[ExplicitColumns]
public sealed class NoteEntity
{
    public const string TableName = "esattoSeoReportNote";

    /// <summary>The longest note the column holds.</summary>
    public const int MaxLength = 1000;

    [Column("period")]
    [PrimaryKeyColumn(AutoIncrement = false)]
    [Length(ReportEntity.PeriodLength)]
    public string Period { get; set; } = string.Empty;

    [Column("text")]
    [Length(MaxLength)]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// A small JSON document by key: <c>settings</c>, <c>protectedKey</c>, <c>targets</c>, <c>fetchState</c>. One table instead
/// of one per concern; the services own each document's shape.
/// </summary>
[TableName(TableName)]
[PrimaryKey("key", AutoIncrement = false)]
[ExplicitColumns]
public sealed class DocumentEntity
{
    public const string TableName = "esattoSeoReportDocument";

    /// <summary>The longest key the column holds.</summary>
    public const int KeyLength = 100;

    [Column("key")]
    [PrimaryKeyColumn(AutoIncrement = false)]
    [Length(KeyLength)]
    public string Key { get; set; } = string.Empty;

    [Column("json")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    public string Json { get; set; } = "{}";

    [Column("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>A named lock row, so two instances don't fetch the same month at once. The primary key on the name settles races.</summary>
[TableName(TableName)]
[PrimaryKey("name", AutoIncrement = false)]
[ExplicitColumns]
public sealed class LockEntity
{
    public const string TableName = "esattoSeoReportLock";

    /// <summary>The longest lock name the column holds.</summary>
    public const int NameLength = 50;

    /// <summary>The longest owner the column holds.</summary>
    public const int OwnerLength = 100;

    [Column("name")]
    [PrimaryKeyColumn(AutoIncrement = false)]
    [Length(NameLength)]
    public string Name { get; set; } = string.Empty;

    [Column("owner")]
    [Length(OwnerLength)]
    public string Owner { get; set; } = string.Empty;

    /// <summary>When the lock lapses, UTC. A lock is free at this instant and after it.</summary>
    [Column("expiresUtc")]
    public DateTime ExpiresUtc { get; set; }
}
