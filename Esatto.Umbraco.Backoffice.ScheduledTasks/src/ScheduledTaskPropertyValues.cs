using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>A task's properties form: the fields and the value of each (saved, or the default).</summary>
/// <param name="Values">Editor values by alias, in the shape each property editor UI expects.</param>
/// <param name="IsCustomised">Some values were saved (as opposed to all defaults).</param>
/// <param name="Warnings">Saved values that no longer fit their property (e.g. its type changed); the default is shown instead.</param>
public sealed record ScheduledTaskPropertiesForm(
    IReadOnlyList<ScheduledTaskPropertyDefinition> Definitions,
    IReadOnlyDictionary<string, JsonNode?> Values,
    bool IsCustomised,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Converts between stored property values (a JSON object of editor values by alias) and typed
/// properties instances, and validates edits from the dashboard.
/// </summary>
internal sealed class ScheduledTaskPropertyValues
{
    private readonly ILogger<ScheduledTaskPropertyValues> _logger;

    public ScheduledTaskPropertyValues(ILogger<ScheduledTaskPropertyValues> logger) => _logger = logger;

    /// <summary>The form for a properties class, with the saved values merged over the defaults per property.</summary>
    public ScheduledTaskPropertiesForm Form(Type propertiesType, string? propertiesJson)
    {
        var definitions = ScheduledTaskPropertySchema.For(propertiesType);
        var defaults = Activator.CreateInstance(propertiesType)!;
        var saved = Parse(propertiesJson);
        var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var warnings = new List<string>();

        foreach (var definition in definitions)
        {
            if (definition.Converter is not { } converter) continue;

            if (saved is not null && saved.TryGetPropertyValue(definition.Alias, out var savedValue))
            {
                try
                {
                    ReadSaved(definition, converter, savedValue);
                    values[definition.Alias] = savedValue?.DeepClone();
                    continue;
                }
                catch (FormatException)
                {
                    warnings.Add($"The saved value of “{definition.Label}” no longer fits it, so its default is shown.");
                }
            }

            values[definition.Alias] = converter.ToEditor(definition.Property.GetValue(defaults));
        }

        return new ScheduledTaskPropertiesForm(definitions, values, saved is not null, warnings);
    }

    /// <summary>
    /// Validates editor values from the dashboard. Returns the JSON to store or the problems by
    /// alias. Only values that differ from their default are stored (null when none do), so the
    /// rest keep following the initialisers; a property left out of <paramref name="values"/>
    /// keeps its default.
    /// </summary>
    public (string? Json, IReadOnlyDictionary<string, string[]>? Errors) Prepare(
        Type propertiesType,
        IReadOnlyDictionary<string, JsonNode?> values)
    {
        var definitions = ScheduledTaskPropertySchema.For(propertiesType);
        var defaults = Activator.CreateInstance(propertiesType)!;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var stored = new JsonObject();

        foreach (var definition in definitions)
        {
            if (definition.Converter is not { } converter) continue;

            var defaultValue = definition.Property.GetValue(defaults);
            var defaultEditorValue = converter.ToEditor(defaultValue);
            if (!values.TryGetValue(definition.Alias, out var value)) value = defaultEditorValue;

            if (definition.Mandatory && converter.IsEmpty(value))
            {
                errors[definition.Alias] = [$"“{definition.Label}” is required."];
                continue;
            }

            object? clrValue;
            try
            {
                clrValue = converter.FromEditor(value);
            }
            catch (FormatException ex)
            {
                errors[definition.Alias] = [$"“{definition.Label}”: {ex.Message}"];
                continue;
            }

            // Compared as the job would read them, so editor details (e.g. a picked item's own key)
            // don't count. An empty value ("", [], an unset date) means "no value", like null.
            var effective = converter.IsEmpty(value) ? null : clrValue;
            var effectiveDefault = converter.IsEmpty(defaultEditorValue) ? null : defaultValue;
            if (!SameValue(effective, effectiveDefault)) stored[definition.Alias] = value?.DeepClone();
        }

        if (errors.Count > 0) return (null, errors);
        return (stored.Count == 0 ? null : stored.ToJsonString(), null);
    }

    // A saved value, as the job reads it. An empty value for a required property (e.g. a blank
    // number saved for an int before it was required) would bind to default(T), so it doesn't fit.
    private static object? ReadSaved(ScheduledTaskPropertyDefinition definition, IScheduledTaskValueConverter converter, JsonNode? value)
        => definition.Mandatory && converter.IsEmpty(value)
            ? throw new FormatException("It's empty, and a value is required.")
            : converter.FromEditor(value);

    /// <summary>Whether two property values are the same value: element-wise for key collections.</summary>
    internal static bool SameValue(object? a, object? b) => (a, b) switch
    {
        (IEnumerable<Guid> x, IEnumerable<Guid> y) => x.SequenceEqual(y),
        (string x, string y) when Guid.TryParse(x, out var gx) && Guid.TryParse(y, out var gy) => gx == gy,
        // A custom editor's own types (classes, lists) usually don't implement Equals: the same
        // JSON is the same value.
        (not null, not null) when !Equals(a, b) && a.GetType() == b.GetType() && !a.GetType().IsPrimitive && a is not string
            => SameJson(a, b),
        _ => Equals(a, b),
    };

    private static bool SameJson(object a, object b)
    {
        try
        {
            var options = ScheduledTaskPropertyEditors.JsonValueConverter.Options;
            return JsonSerializer.Serialize(a, a.GetType(), options) == JsonSerializer.Serialize(b, b.GetType(), options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// A properties instance: defaults from its initialisers, overwritten by the saved values that
    /// still fit. Saved values that don't are logged and skipped.
    /// </summary>
    public TProperties Bind<TProperties>(string taskId, string? propertiesJson)
        where TProperties : class, new()
    {
        var instance = new TProperties();
        if (Parse(propertiesJson) is not { } saved) return instance;

        foreach (var definition in ScheduledTaskPropertySchema.For(typeof(TProperties)))
        {
            if (definition.Converter is not { } converter || !saved.TryGetPropertyValue(definition.Alias, out var value)) continue;

            try
            {
                definition.Property.SetValue(instance, ReadSaved(definition, converter, value));
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(
                    "Scheduled task {TaskId}: the saved value of {Property} doesn't fit ({Reason}); using its default",
                    taskId, definition.Alias, ex.Message);
            }
        }

        return instance;
    }

    /// <summary>The properties class of a task, or null when it has none.</summary>
    public static Type? PropertiesTypeOf(object job) => ScheduledTaskPropertySchema.PropertiesTypeOf(job.GetType());

    private JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or ArgumentException)
        {
            _logger.LogWarning(ex, "Could not read stored scheduled task properties; using the defaults");
            return null;
        }
    }
}

/// <summary><see cref="IScheduledTaskProperties"/> over the settings store.</summary>
internal sealed class ScheduledTaskProperties : IScheduledTaskProperties
{
    private readonly IScheduledTaskSettingsStore _settings;
    private readonly ScheduledTaskPropertyValues _values;

    public ScheduledTaskProperties(IScheduledTaskSettingsStore settings, ScheduledTaskPropertyValues values)
    {
        _settings = settings;
        _values = values;
    }

    public TProperties Get<TProperties>(IScheduledTaskWithProperties<TProperties> task)
        where TProperties : class, new()
    {
        ArgumentNullException.ThrowIfNull(task);

        var taskId = ScheduledTaskIdentity.ForJob(task, "to have scheduled task properties");

        return _values.Bind<TProperties>(taskId, _settings.Get(taskId)?.PropertiesJson);
    }
}
