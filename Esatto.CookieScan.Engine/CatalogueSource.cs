using Esatto.CookieScan.Core;

namespace Esatto.CookieScan.Engine;

/// <summary>
/// Where a scan's catalogue comes from, for every front end.
/// </summary>
/// <remarks>
/// Shared rather than duplicated per front end. If the window loaded the embedded catalogue while
/// the console tool honoured an override beside the exe, the same site would be scanned with
/// different categories and different Swedish wording depending on which one was used - exactly
/// the divergence <see cref="ScanRunner"/> exists to prevent.
/// <para>
/// Takes an <see cref="IScanLog"/> rather than writing to the console, so the window's notice
/// about an override lands in its log pane instead of nowhere.
/// </para>
/// </remarks>
public static class CatalogueSource
{
    /// <summary>The catalogue this run should use.</summary>
    /// <param name="log">Where a notice about an override file goes.</param>
    /// <param name="consentCookie">
    /// What this site calls the banner's consent cookie, when it is not the <c>cookie-consent</c>
    /// default. Applied after the override file is read, so a site can rename the consent entry of
    /// its own catalogue as easily as of the shipped one.
    /// </param>
    public static CookieCatalogue Load(IScanLog log, string? consentCookie = null)
    {
        // An override beside the exe replaces the embedded catalogue wholesale, so legal wording can be
        // changed without a rebuild.
        //
        // Beside the EXE, which is not AppContext.BaseDirectory. Both published exes set
        // IncludeAllContentForSelfExtract - Playwright's driver ships files the bundler cannot embed as
        // native libraries - and that switches the app into full-extraction mode, where BaseDirectory is
        // the extraction directory under %TEMP%\.net rather than the exe's folder. Resolving the override
        // there meant this feature could never work in a published build, which is how it shipped:
        // dropping the file beside the published exe and watching the notice fail to appear is what found
        // it. Environment.ProcessPath is the exe in a single-file build and the apphost in a normal one,
        // so its directory is the right answer for both.
        string? besideTheExe = Path.GetDirectoryName(Environment.ProcessPath);
        string beside = Path.Combine(besideTheExe ?? AppContext.BaseDirectory, "cookie-catalogue.json");

        CookieCatalogue catalogue;

        if (File.Exists(beside))
        {
            log.Info($"Using the catalogue override at {beside}.");

            catalogue = CookieCatalogue.Parse(File.ReadAllText(beside));
        }
        else
        {
            catalogue = CookieCatalogue.Default();
        }

        if (string.IsNullOrWhiteSpace(consentCookie) is false)
        {
            log.Info($"Treating '{consentCookie.Trim()}' as this site's consent cookie.");
        }

        return catalogue.WithConsentCookieNamed(consentCookie);
    }
}
