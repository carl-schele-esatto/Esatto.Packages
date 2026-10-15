using Esatto.Umbraco.Backoffice.BlockPreview.Manifest;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Esatto.Umbraco.Backoffice.BlockPreview;

public static class BlockPreviewServiceCollectionExtensions
{
    /// <summary>Binds <see cref="BlockPreviewOptions"/> and registers the manifest reader and renderer. Idempotent.</summary>
    /// <remarks>
    /// Umbraco registers its manifest readers with plain AddSingleton; PackagingService resolves the
    /// LAST one for its installed-package queries. Appending ours would make it that one, so it is
    /// inserted in front of the existing readers instead (same approach as TreeDragAndDrop).
    /// </remarks>
    public static IServiceCollection AddBlockPreview(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IPackageManifestReader)
                              && d.ImplementationType == typeof(BlockPreviewManifestReader)))
        {
            return services;
        }

        services.AddOptions<BlockPreviewOptions>().BindConfiguration(BlockPreviewOptions.SectionName);

        var descriptor = ServiceDescriptor.Singleton<IPackageManifestReader, BlockPreviewManifestReader>();
        int firstReader = services.ToList().FindIndex(d => d.ServiceType == typeof(IPackageManifestReader));
        if (firstReader < 0)
        {
            services.Add(descriptor);
        }
        else
        {
            services.Insert(firstReader, descriptor);
        }

        return services;
    }
}
