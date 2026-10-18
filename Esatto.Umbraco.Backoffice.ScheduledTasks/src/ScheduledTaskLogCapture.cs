using System.Globalization;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Captures what is logged while a task runs into the run's log: a Serilog sink (Umbraco's logger
/// reads sinks registered in DI) that writes the events logged inside a capture scope, which flows
/// with the run's async context, to that run. Events outside a run cost one AsyncLocal read.
/// </summary>
internal sealed class ScheduledTaskLogCapture : ILogEventSink
{
    private static readonly AsyncLocal<Capture?> Current = new();

    /// <summary>
    /// Captures events of <paramref name="minimum"/> and above logged in this async context until
    /// disposed. Null when <paramref name="minimum"/> is <see cref="LogLevel.None"/>.
    /// </summary>
    public static IDisposable? Begin(IScheduledTaskRunLog runLog, string taskId, Guid runId, LogLevel minimum)
    {
        if (minimum == LogLevel.None) return null;

        var previous = Current.Value;
        Current.Value = new Capture(runLog, taskId, runId, ToSerilog(minimum));
        return new Scope(previous);
    }

    public void Emit(LogEvent logEvent)
    {
        if (Current.Value is not { } capture || logEvent.Level < capture.Minimum) return;

        try
        {
            capture.RunLog.AppendLog(capture.TaskId, capture.RunId, Format(logEvent));
        }
        catch
        {
            // A sink must never throw into the code that logged.
        }
    }

    /// <summary>"2026-10-18T07:00:03.120Z INF ProductFeedImportJob: Imported 120 products" (ISO time first, so the dashboard can show it in local time).</summary>
    internal static string Format(LogEvent logEvent)
    {
        var source = logEvent.Properties.TryGetValue("SourceContext", out var value) && value is ScalarValue { Value: string context }
            ? context[(context.LastIndexOf('.') + 1)..]
            : null;
        var text = Render(logEvent);
        if (logEvent.Exception is { } exception) text += $" — {exception.GetType().Name}: {exception.Message}";

        var time = logEvent.Timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        return source is null ? $"{time} {Level(logEvent.Level)} {text}" : $"{time} {Level(logEvent.Level)} {source}: {text}";
    }

    // As Serilog renders the message, but strings as they are: "Downloading https://…", not "Downloading \"https://…\"".
    private static string Render(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        foreach (var token in logEvent.MessageTemplate.Tokens)
        {
            if (token is PropertyToken property
                && logEvent.Properties.TryGetValue(property.PropertyName, out var value)
                && value is ScalarValue { Value: string text })
            {
                writer.Write(text);
            }
            else
            {
                token.Render(logEvent.Properties, writer, CultureInfo.InvariantCulture);
            }
        }

        return writer.ToString();
    }

    private static string Level(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        _ => "FTL",
    };

    private static LogEventLevel ToSerilog(LogLevel level) => level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        _ => LogEventLevel.Fatal,
    };

    private sealed record Capture(IScheduledTaskRunLog RunLog, string TaskId, Guid RunId, LogEventLevel Minimum);

    private sealed class Scope(Capture? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
