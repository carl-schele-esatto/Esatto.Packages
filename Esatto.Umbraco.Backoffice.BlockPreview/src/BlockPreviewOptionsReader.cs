using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.BlockPreview;

internal static class BlockPreviewOptionsReader
{
    /// <summary>
    /// The bound options, or the defaults when <c>Esatto:BlockPreview</c> can't be bound (e.g. a
    /// non-boolean <c>Enabled</c>): a bad appsettings value must not take down the backoffice manifest
    /// or fail every preview. Used by both, so they always agree.
    /// </summary>
    public static BlockPreviewOptions ValueOrDefaults(this IOptions<BlockPreviewOptions> options, ILogger logger)
    {
        try
        {
            return options.Value;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Invalid '{Section}' configuration; using defaults.", BlockPreviewOptions.SectionName);
            return new BlockPreviewOptions();
        }
    }
}
