using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Esatto.Umbraco.Backoffice.NavigationOrder;

/// <summary>
/// One saved list: the top-bar sections (<c>sections</c>) or one section's tabs (<c>tabs:&lt;sectionAlias&gt;</c>).
/// Lists in Umbraco's default order have no row.
/// </summary>
[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class NavigationOrderEntity
{
    public const string TableName = "NavigationOrder";
    public const string ListKeyIndexName = "IX_NavigationOrder_listKey";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("listKey")]
    [Length(255)]
    [Index(IndexTypes.UniqueNonClustered, Name = ListKeyIndexName, ForColumns = "listKey")]
    public string ListKey { get; set; } = string.Empty;

    /// <summary>The list's aliases, first to last, as a JSON string array.</summary>
    [Column("aliasesJson")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    public string AliasesJson { get; set; } = "[]";

    [Column("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }

    [Column("updatedBy")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? UpdatedBy { get; set; }
}
