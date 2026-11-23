using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.Build.Tests.Support;

/// <summary>An <see cref="ILogger"/> that keeps what was logged, so tests can assert on levels and messages.</summary>
public sealed class ListLogger : ILogger
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _entries.Add((logLevel, formatter(state, exception)));
}
