using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.CrossContent;

public sealed class CrossContentComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<CrossContentOptions>(builder.Config.GetSection(CrossContentOptions.SectionName));
        builder.Services.TryAddSingleton(TimeProvider.System);

        builder.Services.AddHttpClient("crosscontent", ConfigureClient);
        builder.Services.AddSingleton<ICrossContentTeaserFetcher, CrossContentTeaserFetcher>();

        builder.Services.AddHttpClient<ICrossContentCaseListClient, CrossContentCaseListClient>(ConfigureClient);

        builder.Services.AddSingleton<ICrossContentTeaserClient, CrossContentTeaserClient>();

        // Default to a fail-closed (404) mapper so the producer endpoint never 500s on
        // consumer-only installs due to an unresolved ICrossContentTeaserMapper. A producer
        // site's own AddSingleton<ICrossContentTeaserMapper, ...>() registration wins over
        // this default regardless of registration order.
        builder.Services.TryAddSingleton<ICrossContentTeaserMapper, NullCrossContentTeaserMapper>();
    }

    private static void ConfigureClient(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<CrossContentOptions>>().Value;
        if (!string.IsNullOrWhiteSpace(o.BaseUrl)) client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        if (!string.IsNullOrWhiteSpace(o.ApiKey)) client.DefaultRequestHeaders.Add("Api-Key", o.ApiKey);
    }
}
