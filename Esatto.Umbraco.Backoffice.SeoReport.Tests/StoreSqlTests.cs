using System.Reflection;
using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseModelDefinitions;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Cms.Persistence.Sqlite.Services;
using Umbraco.Cms.Persistence.SqlServer.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The store's hand-written SQL in both dialects the package supports. SEO Health 1.0.0 listed a reserved word
/// unquoted and every request failed on SQL Server while SQLite, used by the tests and the smoke site, accepted it.
/// Several columns here are reserved or keyword-like (<c>key</c>, <c>name</c>, <c>owner</c>, <c>text</c>), so every
/// identifier must come from the syntax provider. These tests render and read the SQL text; the statements run against
/// real databases in the end-to-end task.
/// </summary>
public class StoreSqlTests
{
    private static readonly SqlServerSyntaxProvider SqlServer = new(Options.Create(new GlobalSettings()));

    private static readonly SqliteSyntaxProvider Sqlite = new(Options.Create(new GlobalSettings()), NullLogger<SqliteSyntaxProvider>.Instance);

    public static TheoryData<string> Providers => new() { nameof(SqlServer), nameof(Sqlite) };

    private static ISqlSyntaxProvider Provider(string name) => name == nameof(SqlServer) ? SqlServer : Sqlite;

    private static readonly string[] Tables =
        [ReportEntity.TableName, NoteEntity.TableName, DocumentEntity.TableName, LockEntity.TableName];

    private static readonly string[] Columns =
        ["period", "json", "fetchedUtc", "contractVersion", "text", "key", "updatedUtc", "name", "owner", "expiresUtc"];

    /// <summary>The words a statement may contain outside quoted identifiers and parameters.</summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "UPDATE", "SET", "DELETE", "AND", "OR", "ORDER", "BY", "ASC", "INSERT", "INTO", "NOT", "EXISTS",
    };

    /// <summary>Every <c>internal static string XxxSql(ISqlSyntaxProvider)</c> on the store: the hand-written statements.</summary>
    private static IReadOnlyList<MethodInfo> StatementMethods { get; } = typeof(NPocoSeoReportStore)
        .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
        .Where(method => method.ReturnType == typeof(string)
                         && method.Name.EndsWith("Sql", StringComparison.Ordinal)
                         && method.GetParameters() is [{ } parameter]
                         && parameter.ParameterType == typeof(ISqlSyntaxProvider))
        .OrderBy(method => method.Name, StringComparer.Ordinal)
        .ToList();

    private static string Render(MethodInfo statement, ISqlSyntaxProvider syntax)
        => (string)statement.Invoke(null, [syntax])!;

    [Fact]
    public void Every_statement_is_covered()
    {
        // Pinned, so a new statement cannot slip in without being rendered and checked below.
        string[] expected =
        [
            "AcquireLockSql", "DeleteDocumentSql", "DeleteNoteSql", "InsertLockSql", "ListPeriodsSql",
            "ReleaseLockSql", "UpdateDocumentSql", "UpdateNoteSql", "UpdateReportSql",
        ];

        Assert.Equal(expected, StatementMethods.Select(method => method.Name));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Every_statement_renders_in_the_dialect(string provider)
    {
        foreach (var statement in StatementMethods)
        {
            var sql = Render(statement, Provider(provider));
            Assert.False(string.IsNullOrWhiteSpace(sql), statement.Name);
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Every_identifier_is_quoted_by_the_syntax_provider(string provider)
    {
        var syntax = Provider(provider);

        foreach (var statement in StatementMethods)
        {
            var sql = Render(statement, syntax);

            // Take every provider-quoted table and column name out; whatever words remain must be SQL keywords, so a bare
            // (hard-coded, unquoted) identifier has nowhere to hide.
            var remainder = sql;
            foreach (var table in Tables) remainder = remainder.Replace(syntax.GetQuotedTableName(table), " ", StringComparison.Ordinal);
            foreach (var column in Columns) remainder = remainder.Replace(syntax.GetQuotedColumnName(column), " ", StringComparison.Ordinal);
            remainder = Regex.Replace(remainder, @"@\d+", " ");

            var bare = Regex.Matches(remainder, @"[A-Za-z_][A-Za-z_0-9]*").Select(match => match.Value)
                .Where(word => !Keywords.Contains(word)).ToList();
            Assert.True(bare.Count == 0, $"{statement.Name} on {provider} has unquoted identifiers [{string.Join(", ", bare)}]: {sql}");
        }
    }

    [Fact]
    public void The_dialects_quote_differently_so_nothing_is_hard_coded()
    {
        Assert.Equal("[key]", SqlServer.GetQuotedColumnName("key"));
        Assert.Equal("\"key\"", Sqlite.GetQuotedColumnName("key"));

        Assert.Contains("[key]", NPocoSeoReportStore.UpdateDocumentSql(SqlServer));
        Assert.DoesNotContain("\"", NPocoSeoReportStore.UpdateDocumentSql(SqlServer));
        Assert.Contains("\"key\"", NPocoSeoReportStore.UpdateDocumentSql(Sqlite));
        Assert.DoesNotContain("[", NPocoSeoReportStore.UpdateDocumentSql(Sqlite));
    }

    [Fact]
    public void The_reserved_word_columns_are_quoted_on_SQL_Server()
    {
        // key is reserved in SQL Server; name, owner and text are keyword-like. None may appear bare.
        var document = NPocoSeoReportStore.UpdateDocumentSql(SqlServer);
        Assert.Matches(@"\[key\]\s*=\s*@\d", document);
        Assert.DoesNotMatch(@"(?<![\[\w])key(?![\]\w])", document);

        var acquire = NPocoSeoReportStore.AcquireLockSql(SqlServer);
        Assert.Contains("[name]", acquire);
        Assert.Contains("[owner]", acquire);
        Assert.DoesNotMatch(@"(?<![\[\w@])(name|owner)(?![\]\w])", acquire);

        Assert.Contains("[text]", NPocoSeoReportStore.UpdateNoteSql(SqlServer));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void The_lock_is_taken_by_one_conditional_statement(string provider)
    {
        var syntax = Provider(provider);
        var sql = NPocoSeoReportStore.AcquireLockSql(syntax);

        // One UPDATE decides: take the row when it is ours or has expired. The comparison is the database's, not the process's.
        Assert.StartsWith("UPDATE " + syntax.GetQuotedTableName(LockEntity.TableName), sql, StringComparison.Ordinal);
        Assert.Contains(syntax.GetQuotedColumnName("owner") + " = @3", sql);
        Assert.Contains(syntax.GetQuotedColumnName("expiresUtc") + " <= @4", sql);
        Assert.Contains(syntax.GetQuotedColumnName("name") + " = @2", sql);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void The_lock_row_is_inserted_only_when_none_exists(string provider)
    {
        var syntax = Provider(provider);
        var table = syntax.GetQuotedTableName(LockEntity.TableName);
        var name = syntax.GetQuotedColumnName("name");
        var sql = NPocoSeoReportStore.InsertLockSql(syntax);

        // A held lock must make this insert nothing, not fail on the primary key: the existence test is in the statement.
        Assert.StartsWith($"INSERT INTO {table} ({name}, {syntax.GetQuotedColumnName("owner")}, {syntax.GetQuotedColumnName("expiresUtc")}) ", sql, StringComparison.Ordinal);
        Assert.EndsWith($"SELECT @0, @1, @2 WHERE NOT EXISTS (SELECT 1 FROM {table} WHERE {name} = @3)", sql, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Release_only_deletes_the_owners_row(string provider)
    {
        var syntax = Provider(provider);
        var sql = NPocoSeoReportStore.ReleaseLockSql(syntax);

        Assert.StartsWith("DELETE FROM " + syntax.GetQuotedTableName(LockEntity.TableName), sql, StringComparison.Ordinal);
        Assert.Contains(syntax.GetQuotedColumnName("name") + " = @0", sql);
        Assert.Contains(syntax.GetQuotedColumnName("owner") + " = @1", sql);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Periods_are_listed_oldest_first(string provider)
    {
        var syntax = Provider(provider);
        var sql = NPocoSeoReportStore.ListPeriodsSql(syntax);

        Assert.StartsWith("SELECT " + syntax.GetQuotedColumnName("period"), sql, StringComparison.Ordinal);
        Assert.EndsWith("ORDER BY " + syntax.GetQuotedColumnName("period") + " ASC", sql, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Every_table_is_prefixed_and_defined_with_the_documented_columns(string provider)
    {
        var syntax = Provider(provider);

        Assert.All(Tables, table => Assert.StartsWith("esattoSeoReport", table, StringComparison.Ordinal));

        var report = Definition<ReportEntity>(syntax);
        Assert.Equal(["period", "json", "fetchedUtc", "contractVersion"], report.Columns.Select(c => c.Name));
        AssertPrimaryKey(report, "period", length: 7);
        Assert.Equal(ReportEntity.TableName, report.Name);

        var note = Definition<NoteEntity>(syntax);
        Assert.Equal(["period", "text"], note.Columns.Select(c => c.Name));
        AssertPrimaryKey(note, "period", length: 7);
        Assert.Equal(1000, note.Columns.Single(c => c.Name == "text").Size);

        var document = Definition<DocumentEntity>(syntax);
        Assert.Equal(["key", "json", "updatedUtc"], document.Columns.Select(c => c.Name));
        AssertPrimaryKey(document, "key", length: 100);

        var @lock = Definition<LockEntity>(syntax);
        Assert.Equal(["name", "owner", "expiresUtc"], @lock.Columns.Select(c => c.Name));
        AssertPrimaryKey(@lock, "name", length: 50);
        Assert.Equal(100, @lock.Columns.Single(c => c.Name == "owner").Size);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void The_json_columns_are_unbounded_text(string provider)
    {
        var syntax = Provider(provider);

        foreach (var definition in new[] { Definition<ReportEntity>(syntax), Definition<DocumentEntity>(syntax) })
        {
            var json = definition.Columns.Single(c => c.Name == "json");
            Assert.Equal("NVARCHARMAX", json.CustomDbType?.ToString(), ignoreCase: true);
        }
    }

    private static TableDefinition Definition<T>(ISqlSyntaxProvider syntax) => DefinitionFactory.GetTableDefinition(typeof(T), syntax);

    private static void AssertPrimaryKey(TableDefinition table, string column, int length)
    {
        var key = Assert.Single(table.Columns, c => c.IsPrimaryKey);
        Assert.Equal(column, key.Name);
        Assert.False(key.IsIdentity);
        Assert.Equal(length, key.Size);
    }
}
