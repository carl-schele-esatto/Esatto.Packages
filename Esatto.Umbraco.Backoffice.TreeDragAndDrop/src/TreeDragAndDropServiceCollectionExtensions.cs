using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

public static class TreeDragAndDropServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="TreeDragAndDropOptions"/> and registers <see cref="TreeDragAndDropManifestReader"/>.
    /// Idempotent.
    /// </summary>
    /// <remarks>
    /// Umbraco registers its manifest readers with plain AddSingleton. PackageManifestService reads
    /// ALL of them, but PackagingService resolves ONE (the last registered) to build
    /// Packages → Installed. Appending ours (AddSingleton or TryAddEnumerable alike) would make it
    /// that one and hide every App_Plugins package from the Installed list, so it is inserted in
    /// front of the existing readers instead.
    /// </remarks>
    public static IServiceCollection AddTreeDragAndDrop(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IPackageManifestReader)
                              && d.ImplementationType == typeof(TreeDragAndDropManifestReader)))
        {
            return services;
        }

        services.AddOptions<TreeDragAndDropOptions>().BindConfiguration(TreeDragAndDropOptions.SectionName);

        var descriptor = ServiceDescriptor.Singleton<IPackageManifestReader, TreeDragAndDropManifestReader>();
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
