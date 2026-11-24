using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>Finds the package's client source and manifest from the test output folder.</summary>
internal static class ClientFiles
{
    public static string ClientDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var client = Path.Combine(dir.FullName, "Esatto.Umbraco.Backoffice.SeoReport", "Client");
            if (Directory.Exists(client)) return client;
        }

        throw new DirectoryNotFoundException("Esatto.Umbraco.Backoffice.SeoReport/Client not found above the test output.");
    }

    /// <summary>The parsed public/umbraco-package.json. Dispose it.</summary>
    public static JsonDocument Manifest()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(ClientDirectory(), "public", "umbraco-package.json")));
}
