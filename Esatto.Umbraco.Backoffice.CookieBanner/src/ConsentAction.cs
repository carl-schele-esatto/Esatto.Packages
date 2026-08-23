namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>How a decision was reached. This is the endpoint's input contract, not a log record.</summary>
internal enum ConsentAction
{
    AcceptAll,
    RejectAll,
    Custom,
    Withdrawn,
}
