namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// A fetch from Google could not even start, or failed, for a reason with a stable code (for example <c>notConfigured</c> or
/// <c>keyInvalid</c>). The message is the code and nothing else, so nothing about the key or the response can leak through it.
/// </summary>
public sealed class ReportFetchException : Exception
{
    public string Code { get; }

    public ReportFetchException(string code) : base(code)
    {
        Code = code;
    }
}
