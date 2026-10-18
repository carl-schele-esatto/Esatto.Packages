using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>A config entry handed to an Umbraco property editor UI (<c>{ alias, value }</c>).</summary>
public sealed record ScheduledTaskPropertyConfig(string Alias, JsonNode? Value);

/// <summary>
/// Converts between the value an Umbraco property editor UI emits (kept as JSON, exactly as
/// emitted, so it round-trips into the form) and the CLR value the job reads.
/// </summary>
internal interface IScheduledTaskValueConverter
{
    /// <summary>The CLR value for the editor's value. Throws <see cref="FormatException"/> with an editor-facing message when it doesn't fit.</summary>
    object? FromEditor(JsonNode? value);

    /// <summary>The editor value for a CLR value (used for defaults).</summary>
    JsonNode? ToEditor(object? value);

    /// <summary>
    /// Whether the editor value counts as "not filled in" (for mandatory properties). Never throws:
    /// a value of the wrong shape isn't empty, and <see cref="FromEditor"/> reports it.
    /// </summary>
    bool IsEmpty(JsonNode? value);
}

/// <summary>How one property is edited: the Umbraco property editor UI, its config and the value converter.</summary>
internal sealed record ScheduledTaskEditorBinding(
    string PropertyEditorUiAlias,
    IReadOnlyList<ScheduledTaskPropertyConfig> Config,
    IScheduledTaskValueConverter Converter);

/// <summary>
/// Maps <see cref="ScheduledTaskPropertyEditor"/>s and CLR types to Umbraco 18 property editor UIs.
/// Value shapes follow what each UI emits in 18.x (e.g. the document picker emits a key string, the
/// multi-node tree picker an array of <c>{ type, unique }</c>, the dropdown an array of strings).
/// </summary>
internal static class ScheduledTaskPropertyEditors
{
    public const string TextBoxUi = "Umb.PropertyEditorUi.TextBox";
    public const string TextAreaUi = "Umb.PropertyEditorUi.TextArea";
    public const string IntegerUi = "Umb.PropertyEditorUi.Integer";
    public const string DecimalUi = "Umb.PropertyEditorUi.Decimal";
    public const string ToggleUi = "Umb.PropertyEditorUi.Toggle";
    public const string DateUi = "Umb.PropertyEditorUi.DateOnlyPicker";
    public const string DateTimeUi = "Umb.PropertyEditorUi.DateTimePicker";
    public const string DropdownUi = "Umb.PropertyEditorUi.Dropdown";
    public const string DocumentPickerUi = "Umb.PropertyEditorUi.DocumentPicker";
    public const string MultiNodeTreePickerUi = "Umb.PropertyEditorUi.ContentPicker";
    public const string MediaPickerUi = "Umb.PropertyEditorUi.MediaPicker";

    /// <summary>The editor <see cref="ScheduledTaskPropertyEditor.Auto"/> picks for a type, or Auto when none fits.</summary>
    public static ScheduledTaskPropertyEditor Infer(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string)) return ScheduledTaskPropertyEditor.TextBox;
        if (t == typeof(bool)) return ScheduledTaskPropertyEditor.Toggle;
        if (IsInteger(t)) return ScheduledTaskPropertyEditor.Integer;
        if (IsDecimal(t)) return ScheduledTaskPropertyEditor.Decimal;
        if (t == typeof(DateOnly)) return ScheduledTaskPropertyEditor.Date;
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return ScheduledTaskPropertyEditor.DateTime;
        if (t.IsEnum) return ScheduledTaskPropertyEditor.Dropdown;
        if (t == typeof(Guid) || GuidCollection.Is(type)) return ScheduledTaskPropertyEditor.ContentPicker;
        return ScheduledTaskPropertyEditor.Auto;
    }

    /// <summary>The binding for an editor on a property type, or null with an explanation when they don't fit.</summary>
    public static ScheduledTaskEditorBinding? Bind(
        ScheduledTaskPropertyEditor editor,
        Type type,
        bool mandatory,
        string[]? options,
        out string? error)
    {
        error = null;
        var t = Nullable.GetUnderlyingType(type) ?? type;

        switch (editor)
        {
            case ScheduledTaskPropertyEditor.TextBox when t == typeof(string):
                return new(TextBoxUi, [], new TextConverter());
            case ScheduledTaskPropertyEditor.TextArea when t == typeof(string):
                return new(TextAreaUi, [], new TextConverter());
            case ScheduledTaskPropertyEditor.Integer when IsInteger(t):
                return new(IntegerUi, IntegerRange(t), new NumberConverter(type, wholeNumbers: true));
            case ScheduledTaskPropertyEditor.Decimal when IsDecimal(t) || IsInteger(t):
                return new(DecimalUi, [], new NumberConverter(type, wholeNumbers: IsInteger(t)));
            case ScheduledTaskPropertyEditor.Toggle when t == typeof(bool):
                return new(ToggleUi, [], new ToggleConverter(type));
            case ScheduledTaskPropertyEditor.Date when t == typeof(DateOnly) || t == typeof(DateTime):
                return new(DateUi, [], new DateConverter(type, withTime: false));
            case ScheduledTaskPropertyEditor.DateTime when t == typeof(DateTime) || t == typeof(DateTimeOffset):
                return new(DateTimeUi, [], new DateConverter(type, withTime: true));
            case ScheduledTaskPropertyEditor.Dropdown when t.IsEnum && t.IsDefined(typeof(FlagsAttribute), false):
                error = $"{TypeName(type)} is a [Flags] enum; the dropdown picks one value, so it can't hold a combination.";
                return null;
            case ScheduledTaskPropertyEditor.Dropdown when t.IsEnum:
                return Dropdown(DropdownConverter.ForEnum(type), mandatory);
            case ScheduledTaskPropertyEditor.Dropdown when t == typeof(string) && options is { Length: > 0 }:
                return Dropdown(DropdownConverter.ForStrings(options), mandatory);
            case ScheduledTaskPropertyEditor.Dropdown when t == typeof(string):
                error = "A string dropdown needs Options on its [ScheduledTaskProperty] attribute.";
                return null;
            case ScheduledTaskPropertyEditor.ContentPicker when t == typeof(Guid) || t == typeof(string):
                return new(
                    DocumentPickerUi,
                    [new("validationLimit", new JsonObject { ["min"] = mandatory ? 1 : 0, ["max"] = 1 })],
                    new DocumentConverter(type));
            case ScheduledTaskPropertyEditor.ContentPicker when GuidCollection.Is(type):
                return new(
                    MultiNodeTreePickerUi,
                    [
                        new("startNode", new JsonObject { ["type"] = "content" }),
                        new("minNumber", mandatory ? 1 : 0),
                        new("maxNumber", 0),
                    ],
                    new DocumentsConverter(type));
            case ScheduledTaskPropertyEditor.MediaPicker when t == typeof(Guid) || GuidCollection.Is(type):
                var multiple = GuidCollection.Is(type);
                return new(
                    MediaPickerUi,
                    [
                        new("multiple", multiple),
                        new("validationLimit", new JsonObject { ["min"] = mandatory ? 1 : 0, ["max"] = multiple ? 0 : 1 }),
                    ],
                    new MediaConverter(type));
        }

        error = editor == ScheduledTaskPropertyEditor.Auto
            ? $"There is no editor for properties of type {TypeName(type)}; choose one with [ScheduledTaskProperty(Editor = ...)]."
            : $"The {editor} editor can't edit a property of type {TypeName(type)}.";
        return null;
    }

    /// <summary>
    /// The binding for a property editor UI chosen by alias (<see cref="ScheduledTaskPropertyEditor.Custom"/>):
    /// its config from <paramref name="configJson"/> and a JSON converter for the property's type.
    /// </summary>
    public static ScheduledTaskEditorBinding? BindCustom(string? editorUiAlias, string? configJson, Type type, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(editorUiAlias))
        {
            error = "The Custom editor needs EditorUiAlias on its [ScheduledTaskProperty] attribute.";
            return null;
        }

        List<ScheduledTaskPropertyConfig> config = [];
        if (!string.IsNullOrWhiteSpace(configJson))
        {
            JsonNode? parsed;
            try
            {
                parsed = JsonNode.Parse(configJson);
            }
            catch (JsonException ex)
            {
                error = $"Its ConfigJson isn't valid JSON: {ex.Message}";
                return null;
            }

            if (parsed is not JsonObject members)
            {
                error = "Its ConfigJson must be a JSON object, one member per config alias, e.g. { \"maxChars\": 200 }.";
                return null;
            }

            config.AddRange(members.Select(m => new ScheduledTaskPropertyConfig(m.Key, m.Value?.DeepClone())));
        }

        return new(editorUiAlias.Trim(), config, new JsonValueConverter(type));
    }

    private static ScheduledTaskEditorBinding Dropdown(DropdownConverter converter, bool mandatory)
    {
        var items = new JsonArray(converter.Items
            .Select(item => (JsonNode)new JsonObject { ["name"] = item.Name, ["value"] = item.Value })
            .ToArray());

        List<ScheduledTaskPropertyConfig> config = [new("items", items), new("multiple", false)];
        // A mandatory dropdown has no empty option; without a placeholder it looks blank until chosen.
        if (mandatory) config.Add(new("placeholder", "Choose…"));
        return new(DropdownUi, config, converter);
    }

    // The Integer UI checks min/max in the form, so an out-of-range number doesn't wait for the
    // server. (A long's range isn't exact in JavaScript; the server checks that one.)
    private static List<ScheduledTaskPropertyConfig> IntegerRange(Type t)
    {
        if (t == typeof(int)) return [new("min", int.MinValue), new("max", int.MaxValue)];
        if (t == typeof(short)) return [new("min", (int)short.MinValue), new("max", (int)short.MaxValue)];
        if (t == typeof(byte)) return [new("min", 0), new("max", (int)byte.MaxValue)];
        return [];
    }

    internal static bool IsInteger(Type t)
        => t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte);

    internal static bool IsDecimal(Type t)
        => t == typeof(decimal) || t == typeof(double) || t == typeof(float);

    internal static string TypeName(Type type)
        => Nullable.GetUnderlyingType(type) is { } inner ? TypeName(inner) + "?"
            : type.IsGenericType ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>"
            : type.Name;

    // ---- converters ----

    private static FormatException Invalid(string message) => new(message);

    private static string? AsString(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.TryGetValue(out string? s) => s,
        JsonValue value => value.ToJsonString(),
        _ => throw Invalid("Expected a single value."),
    };

    // Not filled in: nothing, or blank text. Arrays and objects aren't empty, just the wrong shape.
    private static bool IsBlank(JsonNode? node) => node switch
    {
        null => true,
        JsonValue value when value.TryGetValue(out string? s) => string.IsNullOrWhiteSpace(s),
        _ => false,
    };

    private sealed class TextConverter : IScheduledTaskValueConverter
    {
        public object? FromEditor(JsonNode? value) => AsString(value);

        public JsonNode? ToEditor(object? value) => value is string s ? JsonValue.Create(s) : null;

        public bool IsEmpty(JsonNode? value) => IsBlank(value);
    }

    private sealed class NumberConverter(Type type, bool wholeNumbers) : IScheduledTaskValueConverter
    {
        private readonly Type _target = Nullable.GetUnderlyingType(type) ?? type;
        private readonly bool _nullable = Nullable.GetUnderlyingType(type) is not null;

        public object? FromEditor(JsonNode? value)
        {
            var text = AsString(value);
            if (string.IsNullOrWhiteSpace(text)) return _nullable ? null : Activator.CreateInstance(_target);

            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                throw Invalid("Enter a number.");
            if (wholeNumbers && number != decimal.Truncate(number))
                throw Invalid("Enter a whole number.");

            try
            {
                return _target == typeof(double) ? (double)number
                    : _target == typeof(float) ? (float)number
                    : Convert.ChangeType(number, _target, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                throw Invalid("The number is too large.");
            }
        }

        public JsonNode? ToEditor(object? value) => value switch
        {
            int number => JsonValue.Create(number),
            long number => JsonValue.Create(number),
            short number => JsonValue.Create(number),
            byte number => JsonValue.Create(number),
            decimal number => JsonValue.Create(number),
            double number => JsonValue.Create(number),
            float number => JsonValue.Create(number),
            _ => null,
        };

        public bool IsEmpty(JsonNode? value) => IsBlank(value);
    }

    private sealed class ToggleConverter(Type type) : IScheduledTaskValueConverter
    {
        private readonly bool _nullable = Nullable.GetUnderlyingType(type) is not null;

        public object? FromEditor(JsonNode? value)
        {
            if (value is JsonValue v && v.TryGetValue(out bool b)) return b;
            return AsString(value)?.Trim().ToLowerInvariant() switch
            {
                null or "" => _nullable ? null : false,
                "true" or "1" => true,
                "false" or "0" => false,
                _ => throw Invalid("Expected on or off."),
            };
        }

        public JsonNode? ToEditor(object? value) => value is bool b ? JsonValue.Create(b) : null;

        // An unset toggle is "off", which is a valid answer.
        public bool IsEmpty(JsonNode? value) => false;
    }

    /// <summary>The 18.x date pickers emit <c>{ date: 'yyyy-MM-dd[THH:mm:ss]', timeZone }</c>.</summary>
    private sealed class DateConverter(Type type, bool withTime) : IScheduledTaskValueConverter
    {
        private readonly Type _target = Nullable.GetUnderlyingType(type) ?? type;

        public object? FromEditor(JsonNode? value)
        {
            var text = value is JsonObject o ? AsString(o["date"]) : AsString(value);
            if (string.IsNullOrWhiteSpace(text)) return null;

            if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                throw Invalid("Enter a valid date.");

            if (_target == typeof(DateOnly)) return DateOnly.FromDateTime(parsed);
            if (_target == typeof(DateTimeOffset)) return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified), TimeSpan.Zero);
            return withTime ? parsed : parsed.Date;
        }

        public JsonNode? ToEditor(object? value)
        {
            var text = value switch
            {
                DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTime d when withTime => d.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                // In UTC: the picker has no offset, and FromEditor reads its value back as +00:00.
                DateTimeOffset d => d.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                _ => null,
            };

            return text is null ? null : new JsonObject { ["date"] = text, ["timeZone"] = null };
        }

        public bool IsEmpty(JsonNode? value) => IsBlank(value is JsonObject o ? o["date"] : value);
    }

    /// <summary>The dropdown emits an array of strings, even for a single choice.</summary>
    private sealed class DropdownConverter : IScheduledTaskValueConverter
    {
        private readonly Type? _enumType;
        private readonly bool _nullable;

        private DropdownConverter(IReadOnlyList<(string Name, string Value)> items, Type? enumType, bool nullable)
        {
            Items = items;
            _enumType = enumType;
            _nullable = nullable;
        }

        public IReadOnlyList<(string Name, string Value)> Items { get; }

        public static DropdownConverter ForEnum(Type type)
        {
            var enumType = Nullable.GetUnderlyingType(type) ?? type;
            var items = Enum.GetNames(enumType)
                .Select(name => (ScheduledTaskIdentity.Humanize(name, stripJobSuffix: false), name))
                .ToList();
            return new DropdownConverter(items, enumType, Nullable.GetUnderlyingType(type) is not null);
        }

        public static DropdownConverter ForStrings(IEnumerable<string> options)
            => new(options.Select(o => (o, o)).ToList(), null, nullable: true);

        public object? FromEditor(JsonNode? value)
        {
            var chosen = value is JsonArray array ? AsString(array.FirstOrDefault()) : AsString(value);
            if (string.IsNullOrWhiteSpace(chosen))
                return _enumType is null ? null : _nullable ? null : Activator.CreateInstance(_enumType);

            var match = Items.FirstOrDefault(i => string.Equals(i.Value, chosen, StringComparison.OrdinalIgnoreCase));
            if (match.Value is null) throw Invalid($"“{chosen}” isn't one of the choices.");

            return _enumType is null ? match.Value : Enum.Parse(_enumType, match.Value);
        }

        public JsonNode? ToEditor(object? value) => value switch
        {
            null => null,
            Enum e => new JsonArray(JsonValue.Create(e.ToString())),
            string s => new JsonArray(JsonValue.Create(s)),
            _ => null,
        };

        public bool IsEmpty(JsonNode? value) => IsBlank(value is JsonArray array ? array.FirstOrDefault() : value);
    }

    /// <summary>The document picker emits the picked key as a string (comma-separated when several are allowed).</summary>
    private sealed class DocumentConverter(Type type) : IScheduledTaskValueConverter
    {
        private readonly bool _asString = (Nullable.GetUnderlyingType(type) ?? type) == typeof(string);

        public object? FromEditor(JsonNode? value)
        {
            var first = AsString(value)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (first is null) return null;
            if (!Guid.TryParse(first, out var key)) throw Invalid("Pick a page from the content tree.");
            return _asString ? key.ToString() : key;
        }

        public JsonNode? ToEditor(object? value) => value switch
        {
            Guid key when key != Guid.Empty => JsonValue.Create(key.ToString()),
            string s when Guid.TryParse(s, out var key) => JsonValue.Create(key.ToString()),
            _ => null,
        };

        public bool IsEmpty(JsonNode? value) => IsBlank(value);
    }

    /// <summary>The multi-node tree picker emits <c>[{ type: 'document', unique: key }]</c>.</summary>
    private sealed class DocumentsConverter(Type type) : IScheduledTaskValueConverter
    {
        public object? FromEditor(JsonNode? value)
            => GuidCollection.Create(type, Keys(value, "unique", "Pick pages from the content tree."));

        public JsonNode? ToEditor(object? value)
            => value is IEnumerable<Guid> keys
                ? new JsonArray(keys.Select(k => (JsonNode)new JsonObject { ["type"] = "document", ["unique"] = k.ToString() }).ToArray())
                : null;

        public bool IsEmpty(JsonNode? value) => value is not JsonArray { Count: > 0 };
    }

    /// <summary>The media picker emits <c>[{ key, mediaKey, mediaTypeAlias, crops, focalPoint }]</c>.</summary>
    private sealed class MediaConverter(Type type) : IScheduledTaskValueConverter
    {
        private readonly bool _multiple = GuidCollection.Is(type);

        public object? FromEditor(JsonNode? value)
        {
            var keys = Keys(value, "mediaKey", "Pick media from the media library.");
            return _multiple ? GuidCollection.Create(type, keys) : keys.Count > 0 ? keys[0] : null;
        }

        public JsonNode? ToEditor(object? value)
        {
            IEnumerable<Guid> keys = value switch
            {
                Guid key when key != Guid.Empty => [key],
                IEnumerable<Guid> many => many,
                _ => [],
            };

            var items = keys.Select(k => (JsonNode)new JsonObject
            {
                // The picked item's own key. Derived from the media key, so a default is the same value every time.
                ["key"] = k.ToString(),
                ["mediaKey"] = k.ToString(),
                ["mediaTypeAlias"] = string.Empty,
                ["crops"] = new JsonArray(),
                ["focalPoint"] = null,
            }).ToArray();

            return items.Length == 0 ? null : new JsonArray(items);
        }

        public bool IsEmpty(JsonNode? value) => value is not JsonArray { Count: > 0 };
    }

    private static List<Guid> Keys(JsonNode? value, string keyProperty, string message)
    {
        if (value is null) return [];
        if (value is not JsonArray array) throw Invalid(message);

        var keys = new List<Guid>();
        foreach (var item in array)
        {
            var text = item is JsonObject o ? AsString(o[keyProperty]) : AsString(item);
            if (!Guid.TryParse(text, out var key)) throw Invalid(message);
            keys.Add(key);
        }

        return keys;
    }

    /// <summary>Guid collections a property may be declared as: arrays, lists and their read-only interfaces.</summary>
    /// <summary>
    /// For a custom property editor UI: the emitted JSON is read into the property's type with
    /// System.Text.Json, and defaults are written the same way. null, "" and [] count as empty.
    /// </summary>
    internal sealed class JsonValueConverter(Type type) : IScheduledTaskValueConverter
    {
        internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        public object? FromEditor(JsonNode? value)
        {
            if (IsEmpty(value))
            {
                if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
                    throw new FormatException($"A value is required ({TypeName(type)} can't be empty).");
                return null;
            }

            try
            {
                return value.Deserialize(type, Options);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                throw new FormatException($"The editor's value doesn't fit a {TypeName(type)}: {ex.Message}");
            }
        }

        public JsonNode? ToEditor(object? value)
            => value is null ? null : JsonSerializer.SerializeToNode(value, type, Options);

        public bool IsEmpty(JsonNode? value) => value switch
        {
            null => true,
            JsonValue v when v.TryGetValue<string>(out var text) => string.IsNullOrEmpty(text),
            JsonArray { Count: 0 } => true,
            _ => false,
        };
    }

    internal static class GuidCollection
    {
        private static readonly Type[] Interfaces =
        [
            typeof(IEnumerable<Guid>), typeof(IReadOnlyCollection<Guid>), typeof(IReadOnlyList<Guid>),
            typeof(ICollection<Guid>), typeof(IList<Guid>),
        ];

        public static bool Is(Type type)
            => type == typeof(Guid[]) || type == typeof(List<Guid>) || Interfaces.Contains(type);

        public static object Create(Type type, List<Guid> keys) => type == typeof(List<Guid>) ? keys : keys.ToArray();
    }
}
