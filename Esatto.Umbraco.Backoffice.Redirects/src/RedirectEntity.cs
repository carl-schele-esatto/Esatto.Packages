using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Esatto.Umbraco.Backoffice.Redirects;

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class RedirectEntity
{
    public const string TableName = "Redirects";

    // Single-site unique index on oldPath (the entity index below + collapse migration).
    public const string OldPathIndexName = "IX_Redirects_oldPath";

    // Legacy multi-site composite index — renamed-in by the legacy rename step,
    // dropped by CollapseToSingleSiteMigration.
    public const string LegacyCompositeIndexName = "IX_Redirects_siteKey_oldPath";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("oldPath")]
    [Length(2048)]
    [Index(IndexTypes.UniqueNonClustered, Name = OldPathIndexName, ForColumns = "oldPath")]
    public string OldPath { get; set; } = string.Empty;

    [Column("newUrl")]
    [Length(2048)]
    public string NewUrl { get; set; } = string.Empty;

    [Column("createdUtc")]
    public DateTime CreatedUtc { get; set; }

    [Column("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }
}
