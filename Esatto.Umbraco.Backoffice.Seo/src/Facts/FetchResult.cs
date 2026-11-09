namespace Esatto.Umbraco.Backoffice.Seo.Facts;

/// <summary>
/// What one HTTP fetch returned. <see cref="StatusCode"/> is 0 when no response arrived
/// (<see cref="Error"/> says why). <see cref="Body"/> is null when it wasn't read (not text, or a HEAD).
/// </summary>
public sealed record FetchResult(
    string RequestedUrl,
    string FinalUrl,
    int StatusCode,
    IReadOnlyList<string> RedirectChain,
    string? ContentType,
    string? XRobotsTag,
    string? Body,
    long Bytes,
    long ElapsedMs,
    string? Error);
