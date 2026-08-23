namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Resolves a piece of consent copy by dictionary key.
/// </summary>
/// <remarks>
/// PUBLIC, deliberately. <c>ConsentEmbedTagHelper</c> is <c>public sealed</c> with a DI-activated
/// public constructor, and a public constructor cannot declare an internal parameter type
/// (CS0051). The implementation stays internal.
/// </remarks>
public interface IConsentTextProvider
{
    /// <summary>Dictionary item, else the embedded resx for the request culture, else English.</summary>
    string Get(string key);
}
