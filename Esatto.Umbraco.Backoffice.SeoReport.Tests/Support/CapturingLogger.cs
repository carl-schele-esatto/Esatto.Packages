using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// An <see cref="ILogger{T}"/> that keeps everything it is given: the formatted message, the exception's full text (type,
/// message, stack) and the structured values, so a test can prove a secret never reached a log in any of those forms.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<Entry> _entries = [];

    public IReadOnlyList<Entry> Entries
    {
        get { lock (_entries) return _entries.ToList(); }
    }

    /// <summary>Everything logged, as one text (message, exception and structured values of every entry).</summary>
    public string AllText
    {
        get { lock (_entries) return string.Join("\n", _entries.Select(e => e.Text)); }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? string.Join(" ", pairs.Select(p => $"{p.Key}={p.Value}"))
            : "";
        var text = $"{formatter(state, exception)} {values} {exception}";

        lock (_entries) _entries.Add(new Entry(logLevel, text));
    }

    internal sealed record Entry(LogLevel Level, string Text);
}
