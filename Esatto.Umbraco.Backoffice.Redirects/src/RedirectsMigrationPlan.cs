using Umbraco.Cms.Infrastructure.Migrations;

namespace Esatto.Umbraco.Backoffice.Redirects;

/// <summary>
/// Migration plan for the Redirects feature. Runs on every app startup; each
/// step is idempotent so re-runs are safe.
/// </summary>
/// <remarks>
/// <para>
/// Fresh plan key <c>"Esatto.Umbraco.Backoffice.Redirects"</c>. The previous
/// <c>"Backoffice.Redirects"</c> plan stays recorded in <c>umbracoKeyValue</c>
/// but never runs again — its composer is gone once this renamed package
/// replaces the old one. Every step detects existing state, so the chain
/// converges from any starting point (fresh, legacy <c>esattoRedirects</c>, or
/// an old multi-site <c>Backoffice.Redirects</c> install).
/// </para>
/// </remarks>
public sealed class RedirectsMigrationPlan : MigrationPlan
{
    public RedirectsMigrationPlan() : base("Esatto.Umbraco.Backoffice.Redirects")
    {
        From(string.Empty)
            .To<RenameLegacyTableMigration>("rename-legacy-table")
            .To<AddRedirectsTableMigration>("add-redirects-table")
            .To<CollapseToSingleSiteMigration>("collapse-to-single-site");
    }
}
