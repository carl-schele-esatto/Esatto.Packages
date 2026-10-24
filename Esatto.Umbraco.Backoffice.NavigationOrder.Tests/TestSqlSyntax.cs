using System.Data;
using System.Diagnostics.CodeAnalysis;
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseModelDefinitions;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;

namespace Esatto.Umbraco.Backoffice.NavigationOrder.Tests;

/// <summary>A SQL context for building (not running) NPoco SQL in tests. Same as ScheduledTasks.Tests'.</summary>
internal sealed class TestSqlSyntax : SqlSyntaxProviderBase<TestSqlSyntax>
{
    public static ISqlContext CreateContext()
    {
        var mappers = new MapperCollection();
        var factory = new FluentPocoDataFactory((type, _) => new PocoDataBuilder(type, mappers).Init(), mappers);
        return new SqlContext(new TestSqlSyntax(), DatabaseType.SQLite, factory);
    }

    public override string ProviderName => "Test";

    public override IsolationLevel DefaultIsolationLevel => IsolationLevel.ReadCommitted;

    public override string DbProvider => "Test";

    public override Sql<ISqlContext> InsertForUpdateHint(Sql<ISqlContext> sql) => sql.Append(" WITH (UPDLOCK)");

    public override IEnumerable<Tuple<string, string, string, bool>> GetDefinedIndexes(IDatabase db) => throw new NotSupportedException();

    public override bool TryGetDefaultConstraint(IDatabase db, string? tableName, string columnName, [MaybeNullWhen(false)] out string constraintName)
        => throw new NotSupportedException();

    public override Sql<ISqlContext>.SqlJoinClause<ISqlContext> LeftJoinWithNestedJoin<TDto>(
        Sql<ISqlContext> sql, Func<Sql<ISqlContext>, Sql<ISqlContext>> nestedJoin, string? alias = null)
        => throw new NotSupportedException();

    public override Sql<ISqlContext> SelectTop(Sql<ISqlContext> sql, int top) => throw new NotSupportedException();

    public override void HandleCreateTable(IDatabase database, TableDefinition tableDefinition, bool skipKeysAndIndexes = false)
        => throw new NotSupportedException();

    protected override string? FormatSystemMethods(SystemMethods systemMethod) => throw new NotSupportedException();

    protected override string FormatIdentity(ColumnDefinition column) => throw new NotSupportedException();
}
