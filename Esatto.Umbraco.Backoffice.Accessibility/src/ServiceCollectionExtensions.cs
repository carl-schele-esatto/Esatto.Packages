using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Manual;
using Esatto.Umbraco.Backoffice.Accessibility.Pages;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Reporting;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esatto.Umbraco.Backoffice.Accessibility;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the accessibility services. Called by <see cref="AccessibilityComposer"/>; idempotent.</summary>
    public static IServiceCollection AddAccessibility(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(KnowledgeBase))) return services;

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<KnowledgeBase>();
        services.AddSingleton<IAccessibilityStore, NPocoAccessibilityStore>();
        // Singleton: holds the one-minute settings cache shared by the API and the public tag helpers.
        services.AddSingleton<SettingsService>();
        services.AddScoped<SitePageSource>();
        services.AddScoped<MediaResolver>();
        services.AddScoped<ReportService>();
        services.AddScoped<ScanService>();
        services.AddScoped<ManualService>();
        services.AddScoped<FalsePositiveService>();
        return services;
    }
}
