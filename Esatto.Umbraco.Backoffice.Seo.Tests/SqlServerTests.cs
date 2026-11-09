using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Persistence.SqlServer.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

/// <summary>
/// The store's hand-written SQL in SQL Server's dialect (Umbraco Cloud). 1.0.0 listed the column
/// <c>trigger</c> unquoted: a reserved word in SQL Server, so every Overview and Issues request failed there
/// ("Incorrect syntax near the keyword 'trigger'"), while SQLite, used by the tests and the smoke site, accepted it.
/// </summary>
public class SqlServerTests
{
    private static readonly SqlServerSyntaxProvider SqlServer = new(Options.Create(new GlobalSettings()));

    [Fact]
    public void Scan_list_quotes_every_column()
    {
        var sql = NPocoSeoStore.ScansSql(SqlServer);
        Assert.Contains("[trigger]", sql);
        Assert.DoesNotMatch(@"[\s,]trigger[\s,]", sql);
    }
}
