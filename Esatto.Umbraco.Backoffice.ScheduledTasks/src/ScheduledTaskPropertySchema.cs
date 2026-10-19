using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>One field of a task's properties form.</summary>
/// <param name="Alias">The property name in camelCase; the key of its stored value.</param>
/// <param name="Error">Why this property can't be edited (e.g. an unsupported type), or null.</param>
public sealed record ScheduledTaskPropertyDefinition(
    string Alias,
    string Label,
    string? Description,
    ScheduledTaskPropertyEditor Editor,
    string? PropertyEditorUiAlias,
    IReadOnlyList<ScheduledTaskPropertyConfig> Config,
    bool Mandatory,
    string? Error)
{
    internal PropertyInfo Property { get; init; } = null!;

    internal IScheduledTaskValueConverter? Converter { get; init; }
}

/// <summary>
/// Reads the form definition from a properties class (see
/// <see cref="IScheduledTaskWithProperties{TProperties}"/>): its public read/write properties in
/// declaration order, base class first. Cached per type.
/// </summary>
internal static class ScheduledTaskPropertySchema
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<ScheduledTaskPropertyDefinition>> Cache = new();

    /// <summary>The properties class a job declares through <see cref="IScheduledTaskWithProperties{TProperties}"/>, or null.</summary>
    public static Type? PropertiesTypeOf(Type jobType)
        => jobType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IScheduledTaskWithProperties<>))
            ?.GetGenericArguments()[0];

    public static IReadOnlyList<ScheduledTaskPropertyDefinition> For(Type propertiesType)
        => Cache.GetOrAdd(propertiesType, Build);

    private static IReadOnlyList<ScheduledTaskPropertyDefinition> Build(Type propertiesType)
    {
        var defaults = TryCreate(propertiesType);
        return propertiesType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetMethod!.IsPublic
                        && p.CanWrite && p.SetMethod!.IsPublic
                        && p.GetIndexParameters().Length == 0
                        && p.GetCustomAttribute<ScheduledTaskPropertyIgnoreAttribute>() is null)
            .OrderBy(p => InheritanceDepth(p.DeclaringType!))
            .ThenBy(p => p.MetadataToken)
            .Select(p => Describe(p, defaults))
            .ToList();
    }

    private static ScheduledTaskPropertyDefinition Describe(PropertyInfo property, object? defaults)
    {
        var attribute = property.GetCustomAttribute<ScheduledTaskPropertyAttribute>();
        var editor = attribute?.Editor ?? ScheduledTaskPropertyEditor.Auto;
        if (!string.IsNullOrWhiteSpace(attribute?.EditorUiAlias)) editor = ScheduledTaskPropertyEditor.Custom;
        else if (editor == ScheduledTaskPropertyEditor.Auto)
            editor = ScheduledTaskPropertyEditors.Infer(property.PropertyType);

        var mandatory = IsMandatory(property, editor, attribute);
        var binding = editor == ScheduledTaskPropertyEditor.Custom
            ? ScheduledTaskPropertyEditors.BindCustom(attribute?.EditorUiAlias, attribute?.ConfigJson, property.PropertyType, out var error)
            : ScheduledTaskPropertyEditors.Bind(editor, property.PropertyType, mandatory, attribute?.Options, out error);
        if (binding is not null && defaults is not null)
        {
            error = DefaultProblem(property, binding.Converter, defaults);
            if (error is not null) binding = null;
        }

        return new ScheduledTaskPropertyDefinition(
            JsonNamingPolicy.CamelCase.ConvertName(property.Name),
            attribute?.Label ?? ScheduledTaskIdentity.Humanize(property.Name, stripJobSuffix: false),
            attribute?.Description,
            editor,
            binding?.PropertyEditorUiAlias,
            binding?.Config ?? [],
            mandatory,
            error)
        {
            Property = property,
            Converter = binding?.Converter,
        };
    }

    /// <summary>
    /// Whether the field must be filled in. Besides <see cref="ScheduledTaskPropertyAttribute.Mandatory"/>:
    /// a non-nullable value type (or a non-nullable string dropdown) can't hold "no value", and a
    /// blank would bind to <c>default(T)</c>, so those are required too. A toggle is always on or
    /// off, so Mandatory means nothing there (Umbraco's toggle would read it as "must be on").
    /// </summary>
    private static bool IsMandatory(PropertyInfo property, ScheduledTaskPropertyEditor editor, ScheduledTaskPropertyAttribute? attribute)
    {
        var type = property.PropertyType;
        if (editor == ScheduledTaskPropertyEditor.Toggle || (Nullable.GetUnderlyingType(type) ?? type) == typeof(bool)) return false;
        if (attribute?.Mandatory == true) return true;
        if (type.IsValueType) return Nullable.GetUnderlyingType(type) is null;
        return type == typeof(string)
               && editor == ScheduledTaskPropertyEditor.Dropdown
               && new NullabilityInfoContext().Create(property).WriteState == NullabilityState.NotNull;
    }

    /// <summary>
    /// Why the property's default can't go through its editor and come back unchanged (e.g. an enum
    /// default that isn't a declared member, or a dropdown default that isn't one of the Options),
    /// or null when it can. Such a form could never be saved untouched, so the property is listed as
    /// not editable instead, and the job keeps the default.
    /// </summary>
    private static string? DefaultProblem(PropertyInfo property, IScheduledTaskValueConverter converter, object defaults)
    {
        object? value;
        try
        {
            value = property.GetValue(defaults);
        }
        catch (TargetInvocationException)
        {
            return null; // A throwing getter shows up when the form reads the defaults.
        }

        var editorValue = converter.ToEditor(value);
        if (converter.IsEmpty(editorValue)) return null; // No value yet; a mandatory field asks for one.

        try
        {
            if (ScheduledTaskPropertyValues.SameValue(converter.FromEditor(editorValue), value)) return null;
        }
        catch (FormatException ex)
        {
            return $"Its default ({Show(value)}) doesn't fit the editor: {ex.Message} Change the default or the editor.";
        }

        return $"Its default ({Show(value)}) would change when saved from the editor. Change the default or the editor.";
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        Enum e => $"{e.GetType().Name}.{e}",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    // The defaults, for checking each property's default against its editor; null when the class
    // can't be created here (the form reports that when it needs the defaults).
    private static object? TryCreate(Type propertiesType)
    {
        try
        {
            return Activator.CreateInstance(propertiesType);
        }
        catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException or MemberAccessException
                                       or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static int InheritanceDepth(Type type)
    {
        var depth = 0;
        for (var t = type.BaseType; t is not null; t = t.BaseType) depth++;
        return depth;
    }
}
