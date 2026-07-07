using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Backoffice.CrossContent;

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
    }

    private static void ConfigureClient(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<CrossContentOptions>>().Value;
        if (!string.IsNullOrWhiteSpace(o.BaseUrl)) client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        if (!string.IsNullOrWhiteSpace(o.ApiKey)) client.DefaultRequestHeaders.Add("Api-Key", o.ApiKey);
    }
}
