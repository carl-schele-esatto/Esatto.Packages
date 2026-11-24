using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>The archive row keeps its period as plain <c>YYYY-MM</c> text for NPoco; these are the typed ways in and out.</summary>
public class ReportEntityTests
{
    [Fact]
    public void From_writes_the_period_in_its_wire_form()
    {
        var entity = ReportEntity.From(new Period(2026, 5), """{"x":1}""", new DateTime(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc), 3);

        Assert.Equal("2026-05", entity.Period);
        Assert.Equal("""{"x":1}""", entity.Json);
        Assert.Equal(new DateTime(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc), entity.FetchedUtc);
        Assert.Equal(3, entity.ContractVersion);
    }

    [Fact]
    public void ToPeriod_reads_it_back()
        => Assert.Equal(new Period(2026, 5), ReportEntity.From(new Period(2026, 5), "{}", DateTime.UtcNow, 1).ToPeriod());

    [Theory]
    [InlineData("")]
    [InlineData("2026-5")]
    [InlineData("2026-13")]
    [InlineData(" 2026-05")]
    [InlineData("2026-05'; DROP TABLE x;--")]
    public void ToPeriod_refuses_anything_but_YYYY_MM(string text)
        => Assert.Throws<ArgumentException>(() => new ReportEntity { Period = text }.ToPeriod());

    [Fact]
    public void The_entity_is_a_plain_NPoco_row_with_public_get_and_set()
    {
        var entity = new ReportEntity();
        Assert.Equal(string.Empty, entity.Period);
        Assert.Equal("{}", entity.Json);

        foreach (var property in typeof(ReportEntity).GetProperties().Where(p => p.GetCustomAttributes(typeof(NPoco.ColumnAttribute), false).Length > 0))
        {
            Assert.True(property.CanRead && property.CanWrite && property.SetMethod!.IsPublic, property.Name);
        }
    }
}
