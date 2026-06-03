using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Backoffice.Redirects;

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class RedirectEntity
{
    public const string TableName = "Redirects";
    public const string OldPathSiteKeyIndexName = "IX_Redirects_siteKey_oldPath";
    public const string LegacyOldPathIndexName = "IX_Redirects_oldPath";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("siteKey")]
    [Length(64)]
    [Index(IndexTypes.UniqueNonClustered, Name = OldPathSiteKeyIndexName, ForColumns = "siteKey,oldPath")]
    public string SiteKey { get; set; } = string.Empty;

    [Column("oldPath")]
    [Length(2048)]
    public string OldPath { get; set; } = string.Empty;

    [Column("newUrl")]
    [Length(2048)]
    public string NewUrl { get; set; } = string.Empty;

    [Column("createdUtc")]
    public DateTime CreatedUtc { get; set; }

    [Column("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }
}
