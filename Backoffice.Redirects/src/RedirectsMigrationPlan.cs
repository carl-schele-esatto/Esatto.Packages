using Umbraco.Cms.Infrastructure.Migrations;

namespace Backoffice.Redirects;

/// <summary>
/// Migration plan for the Redirects feature. Runs on every app startup;
/// each step is idempotent so re-runs are safe.
/// </summary>
/// <remarks>
/// <para>
/// Step order is deliberate. The rename step runs first so that ESATTO
/// LEGACY consumers (who already have a table called <c>esattoRedirects</c>
/// from the pre-package implementation) have it renamed to the canonical
/// <c>Redirects</c> name BEFORE any other migration looks at it. Fresh
/// installs (no legacy table) skip the rename and the create step builds
/// the table from scratch.
/// </para>
/// <para>
/// The plan name <c>"Backoffice.Redirects"</c> is a NEW key in Umbraco's
/// <c>umbracoKeyValue</c> migration history. Esatto.Web's old plan
/// (<c>"Esatto.Redirects"</c>) is recorded as completed and stays
/// recorded — but is no longer executed because the in-tree
/// <c>RedirectsMigrationComposer</c> is removed when the package is
/// installed. So both plans co-exist in the history table without conflict;
/// only this new plan runs going forward.
/// </para>
/// </remarks>
public sealed class RedirectsMigrationPlan : MigrationPlan
{
    public RedirectsMigrationPlan() : base("Backoffice.Redirects")
    {
        From(string.Empty)
            .To<RenameLegacyTableMigration>("rename-legacy-table")
            .To<AddRedirectsTableMigration>("add-redirects-table")
            .To<EnsureSiteKeyColumnMigration>("ensure-sitekey-column");
    }
}
