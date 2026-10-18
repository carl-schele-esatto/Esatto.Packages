namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Marks a background job (<c>IRecurringBackgroundJob</c> or <c>IDistributedBackgroundJob</c>) as
/// having properties that editors set in <b>Settings → Scheduled Tasks</b>, e.g. the page under
/// which a clean-up job works instead of the whole content tree.
/// </summary>
/// <typeparam name="TProperties">
/// A plain class with a parameterless constructor. Its public read/write properties become the form
/// fields (see <see cref="ScheduledTaskPropertyAttribute"/> for labels and editors) and its property
/// initialisers are the defaults. Read the values when the job runs with
/// <see cref="IScheduledTaskProperties.Get{TProperties}"/>.
/// </typeparam>
/// <example>
/// <code>
/// public sealed class RemoveOutdatedArticlesProperties
/// {
///     [ScheduledTaskProperty("Articles root", Description = "Only articles below this page are checked.")]
///     public Guid? ArticlesRoot { get; set; }
///
///     [ScheduledTaskProperty("Older than (days)")]
///     public int OlderThanDays { get; set; } = 365;
/// }
///
/// public sealed class RemoveOutdatedArticlesJob(IScheduledTaskProperties properties)
///     : RecurringBackgroundJobBase(TimeSpan.FromDays(1)), IScheduledTaskWithProperties&lt;RemoveOutdatedArticlesProperties&gt;
/// {
///     public override Task RunJobAsync(CancellationToken cancellationToken)
///     {
///         var settings = properties.Get(this);
///         // ... use settings.ArticlesRoot and settings.OlderThanDays
///     }
/// }
/// </code>
/// </example>
#pragma warning disable CA1040 // Marker interface: the type argument is the payload.
public interface IScheduledTaskWithProperties<TProperties>
    where TProperties : class, new()
{
}
#pragma warning restore CA1040

/// <summary>Reads the property values editors have set for a task.</summary>
public interface IScheduledTaskProperties
{
    /// <summary>
    /// The task's properties as currently set in the dashboard. Properties nobody has set keep the
    /// defaults from <typeparamref name="TProperties"/>'s initialisers. A new instance per call.
    /// </summary>
    /// <param name="task">The job itself; pass <c>this</c>.</param>
    TProperties Get<TProperties>(IScheduledTaskWithProperties<TProperties> task)
        where TProperties : class, new();
}

/// <summary>Which editor the dashboard shows for a task property.</summary>
public enum ScheduledTaskPropertyEditor
{
    /// <summary>Chosen from the property's type: text, number, toggle, date, dropdown (enums) or content picker (Guid).</summary>
    Auto,
    TextBox,
    TextArea,
    Integer,
    Decimal,
    Toggle,
    Date,
    DateTime,
    Dropdown,

    /// <summary>Pick from the content tree. Bind to <c>Guid?</c>, or a Guid list for several.</summary>
    ContentPicker,

    /// <summary>Pick from the media library. Bind to <c>Guid?</c>, or a Guid list for several.</summary>
    MediaPicker,

    /// <summary>
    /// Any property editor UI registered in the backoffice, your own included: set
    /// <see cref="ScheduledTaskPropertyAttribute.EditorUiAlias"/> (which implies this editor). Its
    /// value is stored as the JSON it emits and read into the property's type with System.Text.Json.
    /// </summary>
    Custom,
}

/// <summary>
/// Optional: label, description and editor for a property of a
/// <see cref="IScheduledTaskWithProperties{TProperties}"/> properties class. Without it the label is
/// the property name in sentence case and the editor is chosen from the type.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ScheduledTaskPropertyAttribute : Attribute
{
    public ScheduledTaskPropertyAttribute()
    {
    }

    public ScheduledTaskPropertyAttribute(string label) => Label = label;

    /// <summary>Field label. Defaults to the property name in sentence case.</summary>
    public string? Label { get; }

    /// <summary>Help text under the label.</summary>
    public string? Description { get; set; }

    /// <summary>The editor to show. <see cref="ScheduledTaskPropertyEditor.Auto"/> picks one from the type.</summary>
    public ScheduledTaskPropertyEditor Editor { get; set; } = ScheduledTaskPropertyEditor.Auto;

    /// <summary>
    /// The alias of a property editor UI to edit the property with, e.g. your own
    /// <c>"My.PropertyEditorUi.FeedSource"</c> registered in an <c>umbraco-package.json</c>, or one of
    /// Umbraco's (<c>"Umb.PropertyEditorUi.Tags"</c>). Setting it selects
    /// <see cref="ScheduledTaskPropertyEditor.Custom"/>. The value the editor emits is stored as JSON
    /// and read into the property's type with System.Text.Json (web defaults: camelCase, enums as
    /// names or numbers), so a string, a number, a list or your own class all work.
    /// </summary>
    public string? EditorUiAlias { get; set; }

    /// <summary>
    /// The editor's configuration as a JSON object, one member per config alias, e.g.
    /// <c>{ "maxChars": 200 }</c>. Only used with <see cref="EditorUiAlias"/>.
    /// </summary>
    public string? ConfigJson { get; set; }

    /// <summary>The value must be filled in before the properties can be saved.</summary>
    public bool Mandatory { get; set; }

    /// <summary>
    /// The choices for a <c>string</c> property shown as a <see cref="ScheduledTaskPropertyEditor.Dropdown"/>.
    /// (An enum property lists its own values.)
    /// </summary>
    public string[]? Options { get; set; }
}

/// <summary>Leaves a public property of a properties class out of the dashboard form.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ScheduledTaskPropertyIgnoreAttribute : Attribute
{
}
