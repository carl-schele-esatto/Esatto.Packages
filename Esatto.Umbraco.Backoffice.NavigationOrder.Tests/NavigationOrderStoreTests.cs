using Microsoft.Extensions.Time.Testing;
using NPoco;
using NSubstitute;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Xunit;

namespace Esatto.Umbraco.Backoffice.NavigationOrder.Tests;

public class NavigationOrderStoreTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero));
    private readonly IUmbracoDatabase _db = Substitute.For<IUmbracoDatabase>();
    private readonly IScope _scope = Substitute.For<IScope>();
    private readonly IScopeProvider _scopes = Substitute.For<IScopeProvider>();
    private readonly NavigationOrderStore _store;

    public NavigationOrderStoreTests()
    {
        ISqlContext sqlContext = TestSqlSyntax.CreateContext();
        _scope.Database.Returns(_db);
        _scope.SqlContext.Returns(sqlContext);
        _scopes.CreateScope(default, default, default, default, default, default, default).ReturnsForAnyArgs(_scope);
        _scopes.SqlContext.Returns(sqlContext);
        _store = new NavigationOrderStore(_scopes, _time);
    }

    [Fact]
    public void Save_replaces_every_row_in_one_scope()
    {
        var inserted = new List<NavigationOrderEntity>();
        _db.When(d => d.Insert(Arg.Any<NavigationOrderEntity>())).Do(call => inserted.Add(call.Arg<NavigationOrderEntity>()));
        var settings = new NavigationOrderSettings(["B", "A"], [new TabOrder("Umb.Section.Settings", ["X", "Y"])]);

        var saved = _store.Save(settings, "Carl");

        _scopes.ReceivedWithAnyArgs(1).CreateScope();
        _db.Received(1).Execute(Arg.Is<Sql>(sql => sql.SQL.TrimStart().StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) && sql.SQL.Contains("NavigationOrder")));
        Assert.Equal(2, inserted.Count);
        Assert.All(inserted, row =>
        {
            Assert.Equal(_time.GetUtcNow().UtcDateTime, row.UpdatedUtc);
            Assert.Equal("Carl", row.UpdatedBy);
        });
        _scope.Received(1).Complete();
        Assert.Equal(NavigationOrderRows.ToStored(inserted).Settings.Sections, saved.Settings.Sections);
        Assert.Equal(["X", "Y"], Assert.Single(saved.Settings.Tabs).Aliases);
        Assert.Equal("Carl", saved.UpdatedBy);
    }

    [Fact]
    public void Get_reads_the_rows()
    {
        List<NavigationOrderEntity> rows =
        [
            new() { ListKey = "sections", AliasesJson = """["B","A"]""", UpdatedUtc = new DateTime(2026, 10, 1), UpdatedBy = "Anna" },
            new() { ListKey = "tabs:S", AliasesJson = """["X"]""", UpdatedUtc = new DateTime(2026, 10, 2), UpdatedBy = "Carl" },
        ];
        _db.Fetch<NavigationOrderEntity>(Arg.Any<Sql>()).Returns(rows);

        var stored = _store.Get();

        var expected = NavigationOrderRows.ToStored(rows);
        Assert.Equal(expected.Settings.Sections, stored.Settings.Sections);
        Assert.Equal(["X"], Assert.Single(stored.Settings.Tabs).Aliases);
        Assert.Equal(expected.UpdatedUtc, stored.UpdatedUtc);
        Assert.Equal("Carl", stored.UpdatedBy);
    }
}
