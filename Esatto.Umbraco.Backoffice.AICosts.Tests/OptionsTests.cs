using Esatto.Umbraco.Backoffice.AICosts;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

public class OptionsTests
{
    [Fact]
    public void Defaults()
    {
        var options = new AICostOptions();
        Assert.Equal("Esatto:AICosts", AICostOptions.SectionName);
        Assert.Equal("USD", options.BaseCurrency);
        Assert.Equal("SEK", options.DefaultCurrency);
        Assert.Empty(options.Currencies);
        Assert.Empty(options.PricesPerMillionTokens);
        Assert.Null(options.MonthlyBudget);
        Assert.Null(options.BudgetCurrency);
        Assert.Equal(["SEK", "EUR", "USD", "NOK", "DKK", "GBP"], AICostOptions.DefaultCurrencies);
    }

    // Ported: the binder appends to a pre-filled list, so Currencies must start empty.
    [Fact]
    public void Binding_currencies_from_config_does_not_duplicate_them()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Esatto:AICosts:Currencies:0"] = "SEK",
            ["Esatto:AICosts:Currencies:1"] = "EUR",
        }).Build();

        var options = new AICostOptions();
        config.GetSection(AICostOptions.SectionName).Bind(options);

        Assert.Equal(["SEK", "EUR"], options.Currencies);
    }

    [Fact]
    public void Model_prices_bind_case_insensitively()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Esatto:AICosts:PricesPerMillionTokens:claude-sonnet-5-5:Input"] = "2",
            ["Esatto:AICosts:PricesPerMillionTokens:claude-sonnet-5-5:Output"] = "10",
        }).Build();

        var options = new AICostOptions();
        config.GetSection(AICostOptions.SectionName).Bind(options);

        Assert.Equal(10m, options.PricesPerMillionTokens["CLAUDE-SONNET-5-5"].Output);
    }
}
