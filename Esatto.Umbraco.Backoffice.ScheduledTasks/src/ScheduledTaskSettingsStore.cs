using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>What an editor changed on one task.</summary>
/// <param name="IsPaused">Scheduled runs are skipped.</param>
/// <param name="PeriodOverride">Interval to use instead of the job's own; null = the job's own.</param>
/// <param name="PropertiesJson">Property values as a JSON object (alias → value); null = all defaults.</param>
/// <param name="Calendar">A calendar schedule replacing the interval; null = runs on an interval.</param>
public sealed record ScheduledTaskSettingState(
    bool IsPaused,
    TimeSpan? PeriodOverride,
    string? PropertiesJson,
    ScheduledTaskCalendar? Calendar = null)
{
    public static readonly ScheduledTaskSettingState Default = new(false, null, null);

    public bool IsDefault => !IsPaused && PeriodOverride is null && PropertiesJson is null && Calendar is null;
}

/// <summary>An editor's changes to one task. Absent = the task runs on its defaults.</summary>
/// <param name="LastScheduledUtc">The last calendar occurrence a distributed task served, shared by all servers; bookkeeping, not an editor setting. Recurring tasks keep theirs per server (<see cref="IScheduledTaskSettingsStore.GetServerOccurrence"/>).</param>
public sealed record ScheduledTaskSetting(
    string TaskId,
    bool IsPaused,
    TimeSpan? PeriodOverride,
    string? PropertiesJson,
    DateTime UpdatedUtc,
    string? UpdatedBy,
    ScheduledTaskCalendar? Calendar = null,
    DateTime? LastScheduledUtc = null)
{
    public ScheduledTaskSettingState State => new(IsPaused, PeriodOverride, PropertiesJson, Calendar);
}

public interface IScheduledTaskSettingsStore
{
    /// <summary>The setting for a task, or null when it runs on its defaults. Served from cache.</summary>
    ScheduledTaskSetting? Get(string taskId);

    /// <summary>All settings, keyed by task id. Served from cache.</summary>
    IReadOnlyDictionary<string, ScheduledTaskSetting> GetAll();

    /// <summary>
    /// Applies <paramref name="change"/> to the task's current state as stored in the database (not
    /// the cache, so changes made on other servers aren't lost), saves it and refreshes the cache.
    /// A change back to <see cref="ScheduledTaskSettingState.Default"/> removes the row.
    /// </summary>
    /// <returns>The saved setting, or null when the task is back on its defaults.</returns>
    ScheduledTaskSetting? Update(string taskId, Func<ScheduledTaskSettingState, ScheduledTaskSettingState> change, string? updatedBy);

    /// <summary>
    /// Records that a distributed task served a calendar occurrence, for all servers (so neither
    /// another server nor a restart repeats it or misses one). Leaves who/when last changed the settings alone; does nothing without a
    /// calendar, and never moves the record back to an earlier occurrence.
    /// </summary>
    void RecordScheduledOccurrence(string taskId, DateTime utc);

    /// <summary>
    /// The last occurrence of the schedule saved at <paramref name="scheduleSavedUtc"/> that a
    /// recurring task handled on this server, or null. Served from cache.
    /// </summary>
    /// <remarks>
    /// A recurring job runs in-process on every server whose role matches, so each server keeps its
    /// own record (unlike <see cref="ScheduledTaskSetting.LastScheduledUtc"/>, which is shared).
    /// </remarks>
    DateTime? GetServerOccurrence(string taskId, DateTime scheduleSavedUtc) => null;

    /// <summary>
    /// Records that a recurring task handled <paramref name="occurrenceUtc"/> of the schedule saved
    /// at <paramref name="scheduleSavedUtc"/> on this server, so a restart of this server neither
    /// repeats it nor misses one. Never moves the record back to an earlier occurrence or schedule,
    /// and leaves the task's settings row alone.
    /// </summary>
    void RecordServerOccurrence(string taskId, DateTime occurrenceUtc, DateTime scheduleSavedUtc) { }

    /// <summary>
    /// True until the settings have been read from the database on this server: before the
    /// package's migration has run, or while the first read keeps failing. <see cref="Get"/> and
    /// <see cref="GetAll"/> then return nothing, which doesn't mean every task is on its defaults.
    /// </summary>
    bool IsWaitingForDatabase => false;

    /// <summary>
    /// Re-reads all settings from the database now, bypassing (and refreshing) the cache, so the
    /// next <see cref="Get"/>/<see cref="GetAll"/> reflect changes made on other servers. Does
    /// nothing before the package's migration has run; throws when the database can't be read.
    /// </summary>
    void Refresh() { }
}

/// <summary>
/// <see cref="IScheduledTaskSettingsStore"/> over the <c>ScheduledTaskSettings</c> table.
/// Reads hit an in-memory snapshot that is reloaded once it is older than
/// <see cref="ScheduledTasksOptions.SettingsRefreshInterval"/>, so the hot path (every job tick)
/// costs nothing and other servers in a load-balanced setup converge within that window.
/// Nothing is read before the package's migration has brought the table up to date
/// (<see cref="ScheduledTasksSchemaState"/>), and a failed first read isn't cached as "no settings".
/// </summary>
internal sealed class ScheduledTaskSettingsStore : IScheduledTaskSettingsStore
{
    /// <summary>How long to wait before retrying a first read that failed.</summary>
    internal static readonly TimeSpan FirstLoadRetryDelay = TimeSpan.FromSeconds(5);

    private const int MaxWriteAttempts = 3;

    private static readonly IReadOnlyDictionary<string, ScheduledTaskSetting> Empty =
        new Dictionary<string, ScheduledTaskSetting>(StringComparer.Ordinal);

    private readonly IScopeProvider _scopeProvider;
    private readonly ScheduledTasksSchemaState _schema;
    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<ScheduledTasksOptions> _options;
    private readonly ILogger<ScheduledTaskSettingsStore> _logger;
    private readonly Lock _loadLock = new();

    private static readonly IReadOnlyDictionary<string, ServerOccurrence> NoOccurrences =
        new Dictionary<string, ServerOccurrence>(StringComparer.Ordinal);

    // Null until a read from the database has succeeded.
    private volatile IReadOnlyDictionary<string, ScheduledTaskSetting>? _snapshot;

    // This server's handled calendar occurrences of recurring tasks, keyed by task id; read with the settings.
    private volatile IReadOnlyDictionary<string, ServerOccurrence> _serverOccurrences = NoOccurrences;

    // Stored schedules already warned about as unreadable, so a reload every 30 s doesn't repeat it.
    private readonly HashSet<string> _warnedUnreadableSchedules = new(StringComparer.Ordinal);
    private long _loadedAtTimestamp;
    private long _firstLoadFailedAtTimestamp;

    public ScheduledTaskSettingsStore(
        IScopeProvider scopeProvider,
        ScheduledTasksSchemaState schema,
        TimeProvider timeProvider,
        IOptionsMonitor<ScheduledTasksOptions> options,
        ILogger<ScheduledTaskSettingsStore> logger)
    {
        _scopeProvider = scopeProvider;
        _schema = schema;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    public bool IsWaitingForDatabase => _snapshot is null;

    /// <summary>This server's name in <see cref="ScheduledTaskOccurrenceEntity.Server"/> (as in the run log).</summary>
    internal string ServerName { get; init; } = Environment.MachineName;

    public ScheduledTaskSetting? Get(string taskId)
        => GetAll().TryGetValue(taskId, out var setting) ? setting : null;

    public IReadOnlyDictionary<string, ScheduledTaskSetting> GetAll()
    {
        var snapshot = _snapshot;
        if (snapshot is not null && !IsStale()) return snapshot;

        // Until the migration has run the table may lack columns (even at runtime level Run).
        if (!_schema.IsReady) return snapshot ?? Empty;

        // A failed first read is retried after a short back-off, not on every call.
        if (snapshot is null && IsFirstLoadBackingOff()) return Empty;

        lock (_loadLock)
        {
            if (_snapshot is not null && !IsStale()) return _snapshot;

            try
            {
                Publish(Load());
            }
            catch (Exception ex) when (_snapshot is not null)
            {
                // Keep serving the last good snapshot; retry after the refresh interval.
                _logger.LogWarning(ex, "Could not load scheduled task settings; using the previous values.");
                Interlocked.Exchange(ref _loadedAtTimestamp, _timeProvider.GetTimestamp());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load scheduled task settings; retrying in {Delay}.", FirstLoadRetryDelay);
                Interlocked.Exchange(ref _firstLoadFailedAtTimestamp, _timeProvider.GetTimestamp());
                return Empty;
            }

            return _snapshot!;
        }
    }

    public void Refresh()
    {
        if (!_schema.IsReady) return;

        lock (_loadLock)
        {
            Publish(Load());
        }
    }

    public ScheduledTaskSetting? Update(
        string taskId,
        Func<ScheduledTaskSettingState, ScheduledTaskSettingState> change,
        string? updatedBy)
    {
        if (!_schema.IsReady)
            throw new InvalidOperationException("The Scheduled Tasks settings can't be saved until the site has finished starting.");

        ScheduledTaskSetting? saved;

        lock (_loadLock)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    saved = Write(taskId, change, updatedBy);
                    break;
                }
                catch (Exception ex) when (attempt < MaxWriteAttempts && ex is DbException or RowChangedException)
                {
                    // Most likely another server inserted (or deleted) this task's row between our
                    // read and write (unique index on taskId); read it again and apply the change on top.
                }
            }

            PublishOne(taskId, saved);
        }

        return saved;
    }

    public void RecordScheduledOccurrence(string taskId, DateTime utc)
    {
        if (!_schema.IsReady) return;
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        lock (_loadLock)
        {
            ScheduledTaskSetting? saved;
            using (var scope = _scopeProvider.CreateScope())
            {
                // Locked, so a concurrent change on another server waits for (or is seen by) this one.
                var existing = Find(scope, taskId);
                if (existing is not { ScheduleJson: not null })
                {
                    saved = existing is null ? null : Map(existing);
                }
                else if (existing.LastScheduledUtc is { } last && last >= utc)
                {
                    saved = Map(existing); // a later occurrence is already recorded
                }
                else
                {
                    // Only this column: an UPDATE never brings back a row deleted meanwhile.
                    existing.LastScheduledUtc = utc;
                    saved = scope.Database.Update(existing, [ScheduledTaskSettingEntity.LastScheduledColumn]) > 0
                        ? Map(existing)
                        : null;
                }

                scope.Complete();
            }

            PublishOne(taskId, saved);
        }
    }

    public DateTime? GetServerOccurrence(string taskId, DateTime scheduleSavedUtc)
    {
        GetAll(); // reloads with the settings when due
        return _serverOccurrences.TryGetValue(taskId, out var recorded) && recorded.ScheduleSavedTicks == SavedTicks(scheduleSavedUtc)
            ? recorded.OccurrenceUtc
            : null;
    }

    public void RecordServerOccurrence(string taskId, DateTime occurrenceUtc, DateTime scheduleSavedUtc)
    {
        if (!_schema.IsReady) return;
        occurrenceUtc = DateTime.SpecifyKind(occurrenceUtc, DateTimeKind.Utc);
        var savedTicks = SavedTicks(scheduleSavedUtc);
        var server = Truncate(ServerName, 255)!;

        lock (_loadLock)
        {
            ServerOccurrence recorded;
            using (var scope = _scopeProvider.CreateScope())
            {
                var existing = scope.Database.FirstOrDefault<ScheduledTaskOccurrenceEntity>(FindServerOccurrenceSql(scope.SqlContext, taskId, server));
                if (existing is not null
                    && (existing.ScheduleSavedTicks > savedTicks
                        || (existing.ScheduleSavedTicks == savedTicks && existing.OccurrenceUtc >= occurrenceUtc)))
                {
                    recorded = ToServerOccurrence(existing); // a later schedule or occurrence is already recorded
                }
                else
                {
                    var entity = existing ?? new ScheduledTaskOccurrenceEntity { TaskId = taskId, Server = server };
                    entity.OccurrenceUtc = occurrenceUtc;
                    entity.ScheduleSavedTicks = savedTicks;
                    if (existing is null) scope.Database.Insert(entity);
                    else scope.Database.Update(entity, [OccurrenceColumn, ScheduleSavedTicksColumn]);
                    recorded = ToServerOccurrence(entity);
                }

                scope.Complete();
            }

            var occurrences = new Dictionary<string, ServerOccurrence>(_serverOccurrences, StringComparer.Ordinal) { [taskId] = recorded };
            _serverOccurrences = occurrences;
        }
    }

    private const string OccurrenceColumn = "occurrenceUtc";
    private const string ScheduleSavedTicksColumn = "scheduleSavedTicks";

    internal static Sql<ISqlContext> FindServerOccurrenceSql(ISqlContext sqlContext, string taskId, string server)
        => sqlContext.Sql()
            .SelectAll()
            .From<ScheduledTaskOccurrenceEntity>()
            .Where<ScheduledTaskOccurrenceEntity>(x => x.TaskId == taskId && x.Server == server)
            .ForUpdate();

    internal static Sql<ISqlContext> ServerOccurrencesSql(ISqlContext sqlContext, string server)
        => sqlContext.Sql()
            .SelectAll()
            .From<ScheduledTaskOccurrenceEntity>()
            .Where<ScheduledTaskOccurrenceEntity>(x => x.Server == server);

    private static long SavedTicks(DateTime scheduleSavedUtc) => DateTime.SpecifyKind(scheduleSavedUtc, DateTimeKind.Utc).Ticks;

    private static ServerOccurrence ToServerOccurrence(ScheduledTaskOccurrenceEntity entity)
        => new(DateTime.SpecifyKind(entity.OccurrenceUtc, DateTimeKind.Utc), entity.ScheduleSavedTicks);

    /// <summary>
    /// Reads one task's row and locks it until the scope ends (<c>UPDLOCK</c> on SQL Server), so a
    /// read-modify-write on another server waits and then sees this one's result. SQLite has a
    /// single writer and needs no hint.
    /// </summary>
    private static ScheduledTaskSettingEntity? Find(IScope scope, string taskId)
        => scope.Database.FirstOrDefault<ScheduledTaskSettingEntity>(FindForUpdateSql(scope.SqlContext, taskId));

    internal static Sql<ISqlContext> FindForUpdateSql(ISqlContext sqlContext, string taskId)
        => sqlContext.Sql()
            .SelectAll()
            .From<ScheduledTaskSettingEntity>()
            .Where<ScheduledTaskSettingEntity>(x => x.TaskId == taskId)
            .ForUpdate();

    private ScheduledTaskSetting? Write(
        string taskId,
        Func<ScheduledTaskSettingState, ScheduledTaskSettingState> change,
        string? updatedBy)
    {
        using var scope = _scopeProvider.CreateScope();

        var existing = Find(scope, taskId);
        var current = existing is null ? ScheduledTaskSettingState.Default : Map(existing).State;
        var next = change(current);
        ScheduledTaskSetting? saved = null;

        // A stored schedule this version can't read (hand-edited, or written by a newer version) is
        // kept as it is unless a new schedule is saved; one it can read is kept verbatim (unknown
        // properties included) unless the change replaced it.
        var storedJson = string.IsNullOrWhiteSpace(existing?.ScheduleJson) ? null : existing.ScheduleJson;
        var keptJson = next.Calendar is null
            ? current.Calendar is null ? storedJson : null
            : ReferenceEquals(next.Calendar, current.Calendar) ? storedJson : null;

        if (next.IsDefault && keptJson is null)
        {
            if (existing is not null && scope.Database.Delete(existing) == 0) throw new RowChangedException();
        }
        else
        {
            var entity = existing ?? new ScheduledTaskSettingEntity { TaskId = taskId };
            entity.IsPaused = next.IsPaused;
            entity.PeriodTicks = next.PeriodOverride?.Ticks;
            entity.PropertiesJson = next.PropertiesJson;
            entity.ScheduleJson = keptJson ?? (next.Calendar is null ? null : JsonSerializer.Serialize(next.Calendar, CalendarJson));
            // The occurrence bookkeeping belongs to the calendar it was recorded under.
            if (keptJson is null && !SameSchedule(next.Calendar, current.Calendar)) entity.LastScheduledUtc = null;
            entity.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
            entity.UpdatedBy = Truncate(updatedBy, 255);

            if (existing is null) scope.Database.Insert(entity);
            else if (scope.Database.Update(entity) == 0) throw new RowChangedException();

            saved = Map(entity);
        }

        scope.Complete();
        return saved;
    }

    private (IReadOnlyDictionary<string, ScheduledTaskSetting> Settings, IReadOnlyDictionary<string, ServerOccurrence> Occurrences) Load()
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var settings = scope.Database
            .Fetch<ScheduledTaskSettingEntity>()
            .Select(Map)
            .ToDictionary(s => s.TaskId, StringComparer.Ordinal);
        var occurrences = (scope.Database.Fetch<ScheduledTaskOccurrenceEntity>(ServerOccurrencesSql(scope.SqlContext, Truncate(ServerName, 255)!)) ?? [])
            .GroupBy(o => o.TaskId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => ToServerOccurrence(g.First()), StringComparer.Ordinal);
        return (settings, occurrences);
    }

    private void Publish((IReadOnlyDictionary<string, ScheduledTaskSetting> Settings, IReadOnlyDictionary<string, ServerOccurrence> Occurrences) loaded)
    {
        _serverOccurrences = loaded.Occurrences;
        _snapshot = loaded.Settings;
        Interlocked.Exchange(ref _loadedAtTimestamp, _timeProvider.GetTimestamp());
    }

    /// <summary>
    /// Puts one task's saved state into the cache (null = no row). Before the first full read
    /// there is no cache to update: the first read picks it up.
    /// </summary>
    private void PublishOne(string taskId, ScheduledTaskSetting? saved)
    {
        if (_snapshot is not { } current) return;

        // Publish a new snapshot rather than mutating the shared one.
        var snapshot = new Dictionary<string, ScheduledTaskSetting>(current, StringComparer.Ordinal);
        if (saved is null) snapshot.Remove(taskId);
        else snapshot[taskId] = saved;
        _snapshot = snapshot;
    }

    private bool IsStale()
        => _timeProvider.GetElapsedTime(Interlocked.Read(ref _loadedAtTimestamp)) >= _options.CurrentValue.SettingsRefreshInterval;

    private bool IsFirstLoadBackingOff()
        => Interlocked.Read(ref _firstLoadFailedAtTimestamp) is var failedAt and not 0
           && _timeProvider.GetElapsedTime(failedAt) < FirstLoadRetryDelay;

    /// <summary>The row changed between this write's read and its update/delete.</summary>
    private sealed class RowChangedException : Exception;

    private ScheduledTaskSetting Map(ScheduledTaskSettingEntity entity) => new(
        entity.TaskId,
        entity.IsPaused,
        entity.PeriodTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null,
        string.IsNullOrWhiteSpace(entity.PropertiesJson) ? null : entity.PropertiesJson,
        DateTime.SpecifyKind(entity.UpdatedUtc, DateTimeKind.Utc),
        entity.UpdatedBy,
        ReadCalendar(entity.TaskId, entity.ScheduleJson),
        entity.LastScheduledUtc is { } last ? DateTime.SpecifyKind(last, DateTimeKind.Utc) : null);

    /// <summary>One task's handled occurrence on this server, and the schedule (its saved time in ticks) it belongs to.</summary>
    private readonly record struct ServerOccurrence(DateTime OccurrenceUtc, long ScheduleSavedTicks);

    // Records compare lists by reference, so compare what the calendar means instead.
    private static bool SameSchedule(ScheduledTaskCalendar? a, ScheduledTaskCalendar? b)
        => a is not null && b is not null
           && a.SavedUtc == b.SavedUtc
           && a.ToCronExpression() == b.ToCronExpression()
           && string.Equals(a.TimeZone, b.TimeZone, StringComparison.OrdinalIgnoreCase);

    internal static readonly JsonSerializerOptions CalendarJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// The stored calendar, or null when there is none or it can't be read (hand-edited, or written
    /// by a newer version): the task then runs as if it had none, and the stored value is kept (see
    /// <see cref="Write"/>). Logged once per stored value.
    /// </summary>
    private ScheduledTaskCalendar? ReadCalendar(string taskId, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        string problem;
        try
        {
            var calendar = JsonSerializer.Deserialize<ScheduledTaskCalendar>(json, CalendarJson);
            // "weekdays": null deserializes as null despite the property's type.
            if (calendar is not null && (IReadOnlyList<DayOfWeek>?)calendar.Weekdays is null) calendar = calendar with { Weekdays = [] };

            problem = calendar switch
            {
                null => "it is empty",
                _ when !Enum.IsDefined(calendar.Kind) => $"its kind {(int)calendar.Kind} is unknown",
                _ when string.IsNullOrWhiteSpace(calendar.TimeZone) => "it has no time zone",
                _ when calendar.Weekdays.Any(d => !Enum.IsDefined(d)) => "it has an unknown weekday",
                _ => string.Empty,
            };
            if (problem.Length == 0) return calendar;
        }
        catch (JsonException ex)
        {
            problem = ex.Message;
        }

        bool firstTime;
        lock (_warnedUnreadableSchedules) firstTime = _warnedUnreadableSchedules.Add(taskId + "\n" + json);
        if (firstTime)
        {
            _logger.LogWarning(
                "The calendar schedule stored for scheduled task {TaskId} can't be read ({Problem}), so the task runs as if it had none. "
                + "The stored value is kept until a new schedule is saved for the task. Stored value: {ScheduleJson}",
                taskId, problem, json);
        }

        return null;
    }

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];
}
