namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Stable keys for everything <see cref="CookieBannerSchemaInstaller"/> creates. Keeping them in
/// one place makes the installer idempotent across environments: a re-run finds the existing
/// entity by key instead of creating a duplicate, and a uSync export produces the same GUIDs on
/// every site. These are a fresh namespace, deliberately unrelated to the NDSTK series they were
/// extracted from, so installing the package on the NDSTK site cannot adopt or overwrite site
/// schema.
/// </summary>
internal static class CookieBannerKeys
{
    /// <summary>Data types this package adds on top of the Umbraco defaults.</summary>
    internal static class DataTypes
    {
        internal static readonly Guid CookieCategory = new("c00c1e00-0001-4000-8000-000000000001");
        internal static readonly Guid StorageType = new("c00c1e00-0001-4000-8000-000000000002");
        internal static readonly Guid CookieRegistry = new("c00c1e00-0001-4000-8000-000000000003");
    }

    /// <summary>Element types used as Block List blocks.</summary>
    internal static class ElementTypes
    {
        internal static readonly Guid CookieDefinition = new("c00c1e00-0002-4000-8000-000000000001");
    }

    internal static class DocumentTypes
    {
        internal static readonly Guid CookiePolicy = new("c00c1e00-0003-4000-8000-000000000001");
    }

    internal static class Templates
    {
        internal static readonly Guid CookiePolicy = new("c00c1e00-0004-4000-8000-000000000001");
    }

    /// <summary>
    /// Content nodes the seeder creates. Continues the c00c1e00 series with the -0005- segment so
    /// the whole package occupies one readable GUID namespace and a uSync export produces the
    /// same key on every environment.
    /// </summary>
    internal static class Nodes
    {
        internal static readonly Guid CookiePolicy = new("c00c1e00-0005-4000-8000-000000000001");
    }

    /// <summary>Umbraco's built-in data types, reused as-is.</summary>
    internal static class BuiltInDataTypes
    {
        internal static readonly Guid Textstring = new("0cc0eba1-9960-42c9-bf9b-60e150b429ae");
        internal static readonly Guid Textarea = new("c6bac0dd-4ab9-45b1-8e30-e4b619ee5da3");
        internal static readonly Guid RichtextEditor = new("ca90c950-0aff-4e72-b976-a30b1ac57dad");
    }
}
