using System.Text.Json;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Serialization;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Api.Common.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.Models;
using Umbraco.Cms.Infrastructure.Services;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public enum CleanupAction
{
    Unpublish,
    MoveToRecycleBin,
}

public sealed class CleanupProperties
{
    [ScheduledTaskProperty("Articles root", Description = "Only articles below this page.", Mandatory = true)]
    public Guid? ArticlesRoot { get; set; }

    public int OlderThanDays { get; set; } = 365;

    public bool DryRun { get; set; } = true;

    public CleanupAction Action { get; set; } = CleanupAction.MoveToRecycleBin;

    public decimal Threshold { get; set; } = 0.5m;

    public DateOnly? NotBefore { get; set; }

    public DateTime? RunUntil { get; set; }

    public string? Note { get; set; }

    [ScheduledTaskProperty(Editor = ScheduledTaskPropertyEditor.TextArea)]
    public string? Report { get; set; }

    [ScheduledTaskProperty(Editor = ScheduledTaskPropertyEditor.Dropdown, Options = ["daily", "weekly"])]
    public string Frequency { get; set; } = "daily";

    public IReadOnlyList<Guid> ExcludedPages { get; set; } = [];

    [ScheduledTaskProperty(Editor = ScheduledTaskPropertyEditor.MediaPicker)]
    public Guid? ReportTemplate { get; set; }

    public Uri? Unsupported { get; set; }

    [ScheduledTaskPropertyIgnore]
    public string Hidden { get; set; } = "not shown";

    public string ReadOnly => "not shown either";
}

public sealed class CleanupJob : RecurringBackgroundJobBase, IScheduledTaskWithProperties<CleanupProperties>
{
    public CleanupJob() : base(TimeSpan.FromDays(1)) { }

    public override Task RunJobAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class DistributedCleanupJob : IDistributedBackgroundJob, IScheduledTaskWithProperties<CleanupProperties>
{
    public string Name => "DistributedCleanup";

    public TimeSpan Period => TimeSpan.FromDays(1);

    public Task ExecuteAsync() => Task.CompletedTask;
}

public class ScheduledTaskPropertySchemaTests
{
    private static readonly IReadOnlyList<ScheduledTaskPropertyDefinition> Definitions =
        ScheduledTaskPropertySchema.For(typeof(CleanupProperties));

    private static ScheduledTaskPropertyDefinition Field(string alias) => Assert.Single(Definitions, d => d.Alias == alias);

    [Fact]
    public void Public_read_write_properties_become_fields_in_declaration_order()
        => Assert.Equal(
            ["articlesRoot", "olderThanDays", "dryRun", "action", "threshold", "notBefore", "runUntil", "note", "report",
             "frequency", "excludedPages", "reportTemplate", "unsupported"],
            Definitions.Select(d => d.Alias));

    [Theory]
    [InlineData("articlesRoot", "Umb.PropertyEditorUi.DocumentPicker")]
    [InlineData("olderThanDays", "Umb.PropertyEditorUi.Integer")]
    [InlineData("dryRun", "Umb.PropertyEditorUi.Toggle")]
    [InlineData("action", "Umb.PropertyEditorUi.Dropdown")]
    [InlineData("threshold", "Umb.PropertyEditorUi.Decimal")]
    [InlineData("notBefore", "Umb.PropertyEditorUi.DateOnlyPicker")]
    [InlineData("runUntil", "Umb.PropertyEditorUi.DateTimePicker")]
    [InlineData("note", "Umb.PropertyEditorUi.TextBox")]
    [InlineData("report", "Umb.PropertyEditorUi.TextArea")]
    [InlineData("frequency", "Umb.PropertyEditorUi.Dropdown")]
    [InlineData("excludedPages", "Umb.PropertyEditorUi.ContentPicker")]
    [InlineData("reportTemplate", "Umb.PropertyEditorUi.MediaPicker")]
    public void Each_type_gets_the_matching_Umbraco_editor(string alias, string propertyEditorUiAlias)
        => Assert.Equal(propertyEditorUiAlias, Field(alias).PropertyEditorUiAlias);

    [Fact]
    public void Labels_come_from_the_attribute_or_the_property_name()
    {
        Assert.Equal("Articles root", Field("articlesRoot").Label);
        Assert.Equal("Only articles below this page.", Field("articlesRoot").Description);
        Assert.Equal("Older than days", Field("olderThanDays").Label);
    }

    [Fact]
    public void A_mandatory_single_page_picker_requires_exactly_one_page()
    {
        var limit = Assert.Single(Field("articlesRoot").Config, c => c.Alias == "validationLimit");
        Assert.True(Field("articlesRoot").Mandatory);
        Assert.Equal(1, (int)limit.Value!["min"]!);
        Assert.Equal(1, (int)limit.Value!["max"]!);
    }

    [Fact]
    public void Enum_dropdowns_list_the_enum_values()
    {
        var items = (JsonArray)Assert.Single(Field("action").Config, c => c.Alias == "items").Value!;
        Assert.Equal(["Unpublish", "MoveToRecycleBin"], items.Select(i => (string)i!["value"]!));
        Assert.Equal("Move to recycle bin", (string)items[1]!["name"]!);
    }

    [Fact]
    public void Unsupported_types_are_listed_with_a_reason_instead_of_failing()
    {
        var unsupported = Field("unsupported");
        Assert.Null(unsupported.PropertyEditorUiAlias);
        Assert.Contains("Uri", unsupported.Error);
    }

    [Fact]
    public void A_job_declares_its_properties_class_through_the_marker_interface()
    {
        Assert.Equal(typeof(CleanupProperties), ScheduledTaskPropertySchema.PropertiesTypeOf(typeof(CleanupJob)));
        Assert.Null(ScheduledTaskPropertySchema.PropertiesTypeOf(typeof(ScriptedJob)));
    }
}

public class ScheduledTaskPropertyValuesTests
{
    private static readonly Guid Root = Guid.Parse("8b1c4a3e-2f7d-4c55-9a0e-1d2f3e4a5b6c");
    private static readonly Guid Excluded = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly ScheduledTaskPropertyValues _values = new(NullLogger<ScheduledTaskPropertyValues>.Instance);

    private static Dictionary<string, JsonNode?> EditorValues(string json)
        => JsonNode.Parse(json)!.AsObject().ToDictionary(p => p.Key, p => p.Value?.DeepClone());

    [Fact]
    public void Without_saved_values_the_form_shows_the_defaults_in_editor_shapes()
    {
        var form = _values.Form(typeof(CleanupProperties), null);

        Assert.False(form.IsCustomised);
        Assert.Equal(365, (int)form.Values["olderThanDays"]!);
        Assert.True((bool)form.Values["dryRun"]!);
        Assert.Equal("""["MoveToRecycleBin"]""", form.Values["action"]!.ToJsonString());
        Assert.Equal("""["daily"]""", form.Values["frequency"]!.ToJsonString());
        Assert.Null(form.Values["articlesRoot"]);
        Assert.False(form.Values.ContainsKey("unsupported"));
    }

    [Fact]
    public void Saved_values_are_merged_over_the_defaults_per_property()
    {
        var form = _values.Form(typeof(CleanupProperties), $$"""{"articlesRoot":"{{Root}}","olderThanDays":30}""");

        Assert.True(form.IsCustomised);
        Assert.Equal(Root.ToString(), (string)form.Values["articlesRoot"]!);
        Assert.Equal(30, (int)form.Values["olderThanDays"]!);
        Assert.True((bool)form.Values["dryRun"]!); // not saved: default
    }

    [Fact]
    public void A_saved_value_that_no_longer_fits_shows_the_default_with_a_warning()
    {
        var form = _values.Form(typeof(CleanupProperties), """{"olderThanDays":"many"}""");

        Assert.Equal(365, (int)form.Values["olderThanDays"]!);
        Assert.Contains(form.Warnings, w => w.Contains("Older than days"));
    }

    [Fact]
    public void Editor_values_bind_to_typed_properties()
    {
        var json = $$"""
            {
              "articlesRoot": "{{Root}}",
              "olderThanDays": 30,
              "dryRun": false,
              "action": ["Unpublish"],
              "threshold": 0.75,
              "notBefore": { "date": "2026-11-01", "timeZone": null },
              "runUntil": { "date": "2026-12-24T18:30:00", "timeZone": null },
              "note": "Hello",
              "frequency": ["weekly"],
              "excludedPages": [{ "type": "document", "unique": "{{Excluded}}" }],
              "reportTemplate": [{ "key": "{{Guid.NewGuid()}}", "mediaKey": "{{Excluded}}", "mediaTypeAlias": "", "crops": [], "focalPoint": null }]
            }
            """;

        var bound = _values.Bind<CleanupProperties>("recurring:X", json);

        Assert.Equal(Root, bound.ArticlesRoot);
        Assert.Equal(30, bound.OlderThanDays);
        Assert.False(bound.DryRun);
        Assert.Equal(CleanupAction.Unpublish, bound.Action);
        Assert.Equal(0.75m, bound.Threshold);
        Assert.Equal(new DateOnly(2026, 11, 1), bound.NotBefore);
        Assert.Equal(new DateTime(2026, 12, 24, 18, 30, 0), bound.RunUntil);
        Assert.Equal("Hello", bound.Note);
        Assert.Equal("weekly", bound.Frequency);
        Assert.Equal([Excluded], bound.ExcludedPages);
        Assert.Equal(Excluded, bound.ReportTemplate);
        Assert.Equal("not shown", bound.Hidden);
    }

    [Fact]
    public void A_saved_value_that_no_longer_fits_keeps_the_default_when_binding()
    {
        var bound = _values.Bind<CleanupProperties>("recurring:X", """{"olderThanDays":"many","dryRun":false}""");

        Assert.Equal(365, bound.OlderThanDays);
        Assert.False(bound.DryRun);
    }

    [Fact]
    public void Unreadable_stored_json_means_all_defaults()
        => Assert.Equal(365, _values.Bind<CleanupProperties>("recurring:X", "{not json").OlderThanDays);

    [Fact]
    public void Saving_reports_missing_and_invalid_values_by_alias()
    {
        var (json, errors) = _values.Prepare(
            typeof(CleanupProperties),
            EditorValues("""{"articlesRoot":null,"olderThanDays":2.5,"action":["Delete"]}"""));

        Assert.Null(json);
        Assert.Equal(["“Articles root” is required."], errors!["articlesRoot"]);
        Assert.Equal(["“Older than days”: Enter a whole number."], errors["olderThanDays"]);
        Assert.Equal(["“Action”: “Delete” isn't one of the choices."], errors["action"]);
    }

    [Fact]
    public void Saving_valid_values_stores_them_as_the_editors_emitted_them()
    {
        var (json, errors) = _values.Prepare(
            typeof(CleanupProperties),
            EditorValues($$"""{"articlesRoot":"{{Root}}","olderThanDays":30,"dryRun":true}"""));

        Assert.Null(errors);
        var stored = JsonNode.Parse(json!)!.AsObject();
        Assert.Equal(Root.ToString(), (string)stored["articlesRoot"]!);
        Assert.Equal(30, (int)stored["olderThanDays"]!);
        Assert.False(stored.ContainsKey("unsupported"));
    }

    public sealed class OptionalProperties
    {
        public int Days { get; set; } = 7;

        public string? Label { get; set; }
    }

    [Fact]
    public void Saving_only_defaults_stores_nothing()
    {
        var (json, errors) = _values.Prepare(typeof(OptionalProperties), EditorValues("""{"days":7,"label":""}"""));

        Assert.Null(errors);
        Assert.Null(json);
    }
}

public class ScheduledTaskPropertiesAccessorTests
{
    private readonly IScheduledTaskSettingsStore _settings = Substitute.For<IScheduledTaskSettingsStore>();

    private ScheduledTaskProperties CreateAccessor()
        => new(_settings, new ScheduledTaskPropertyValues(NullLogger<ScheduledTaskPropertyValues>.Instance));

    [Fact]
    public void A_recurring_job_reads_the_values_saved_for_its_type()
    {
        var id = ScheduledTaskIdentity.ForRecurring(typeof(CleanupJob));
        _settings.Get(id).Returns(TestSupport.Setting(id, propertiesJson: """{"olderThanDays":10}"""));

        Assert.Equal(10, CreateAccessor().Get(new CleanupJob()).OlderThanDays);
    }

    [Fact]
    public void A_distributed_job_reads_the_values_saved_for_its_name()
    {
        var id = ScheduledTaskIdentity.ForDistributed("DistributedCleanup");
        _settings.Get(id).Returns(TestSupport.Setting(id, propertiesJson: """{"olderThanDays":20}"""));

        Assert.Equal(20, CreateAccessor().Get(new DistributedCleanupJob()).OlderThanDays);
    }

    [Fact]
    public void Without_saved_values_a_job_gets_fresh_defaults()
    {
        var first = CreateAccessor().Get(new CleanupJob());
        var second = CreateAccessor().Get(new CleanupJob());

        Assert.Equal(365, first.OlderThanDays);
        Assert.NotSame(first, second);
    }
}

public class ScheduledTaskServicePropertiesTests
{
    private static readonly string Id = ScheduledTaskIdentity.ForRecurring(typeof(CleanupJob));

    private readonly IScheduledTaskSettingsStore _settings = Substitute.For<IScheduledTaskSettingsStore>();

    private ScheduledTaskService CreateService()
    {
        var table = Substitute.For<IDistributedJobTable>();
        table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel>());
        _settings.GetAll().Returns(new Dictionary<string, ScheduledTaskSetting>());
        var serverRole = Substitute.For<IServerRoleAccessor>();
        serverRole.CurrentServerRole.Returns(ServerRole.Single);

        return new ScheduledTaskService(
            [new CleanupJob(), new ScriptedJob(TimeSpan.FromMinutes(5))],
            [],
            new RecurringJobHostRegistry(),
            Substitute.For<IDistributedJobService>(),
            table,
            _settings,
            new ScheduledTaskRunLog(TimeProvider.System, TestSupport.Options()),
            serverRole,
            Substitute.For<IHostApplicationLifetime>(),
            new ManualDistributedRunTracker(NullLogger<ManualDistributedRunTracker>.Instance),
            new ScheduledTaskPropertyValues(NullLogger<ScheduledTaskPropertyValues>.Instance),
            TestSupport.Calendars,
            TestSupport.Options(),
            NullLogger<ScheduledTaskService>.Instance);
    }

    [Fact]
    public void The_overview_counts_a_tasks_properties()
    {
        var tasks = CreateService().GetAll();

        Assert.Equal(13, tasks.Single(t => t.Id == Id).PropertyCount);
        Assert.Equal(0, tasks.Single(t => t.Id == ScheduledTaskIdentity.ForRecurring(typeof(ScriptedJob))).PropertyCount);
    }

    [Fact]
    public void A_task_without_properties_has_an_empty_form()
    {
        var form = CreateService().GetProperties(ScheduledTaskIdentity.ForRecurring(typeof(ScriptedJob)));

        Assert.NotNull(form);
        Assert.Empty(form.Definitions);
    }

    [Fact]
    public void Invalid_values_are_rejected_per_property_and_not_saved()
    {
        var result = CreateService().SetProperties(Id, new Dictionary<string, JsonNode?> { ["olderThanDays"] = "soon" }, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Invalid, result.Status);
        Assert.Contains("articlesRoot", result.Errors!.Keys);
        Assert.Contains("olderThanDays", result.Errors.Keys);
        _settings.DidNotReceiveWithAnyArgs().Update(default!, default!, default);
    }

    [Fact]
    public void Valid_values_are_saved_without_touching_pause_or_interval()
    {
        var root = Guid.NewGuid();
        var result = CreateService().SetProperties(
            Id,
            new Dictionary<string, JsonNode?> { ["articlesRoot"] = root.ToString(), ["olderThanDays"] = 30 },
            "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        var before = new ScheduledTaskSettingState(true, TimeSpan.FromHours(1), null);
        var after = _settings.ChangeSavedFor(Id)(before);
        Assert.True(after.IsPaused);
        Assert.Equal(TimeSpan.FromHours(1), after.PeriodOverride);
        Assert.Contains(root.ToString(), after.PropertiesJson);
    }

    [Fact]
    public void Null_values_restore_the_defaults()
    {
        CreateService().SetProperties(Id, null, "Carl");

        Assert.Null(_settings.ChangeSavedFor(Id)(new ScheduledTaskSettingState(false, null, "{}")).PropertiesJson);
    }

    [Fact]
    public void Properties_of_unknown_tasks_are_not_found()
    {
        var service = CreateService();

        Assert.Null(service.GetProperties("recurring:Nope"));
        Assert.Equal(ScheduledTaskCommandStatus.NotFound, service.SetProperties("recurring:Nope", null, "Carl").Status);
    }

    [Fact]
    public void Invalid_properties_reach_the_backoffice_as_validation_problem_details()
    {
        var service = Substitute.For<IScheduledTaskService>();
        service.SetProperties(Id, Arg.Any<IReadOnlyDictionary<string, JsonNode?>?>(), Arg.Any<string?>())
            .Returns(ScheduledTaskCommandResult.Invalid(
                "Some properties need attention.",
                new Dictionary<string, string[]> { ["articlesRoot"] = ["“Articles root” is required."] }));
        var controller = new ScheduledTasksController(service, Substitute.For<IBackOfficeSecurityAccessor>(), TestSupport.Options());

        var result = Assert.IsType<ObjectResult>(controller.SetProperties(
            new SetScheduledTaskPropertiesRequest(Id, [new ScheduledTaskPropertyValueDto("articlesRoot", null)])));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Error", problem.Type);
        var errors = Assert.IsAssignableFrom<IDictionary<string, string[]>>(problem.Extensions["errors"]);
        Assert.Equal(["“Articles root” is required."], errors["articlesRoot"]);
    }

    [Fact]
    public void Property_errors_are_serialized_by_alias_under_the_backoffice_json_options()
    {
        // The modal reads problemDetails.errors as { alias: [messages] }. Umbraco's backoffice options
        // write a ValidationProblemDetails' errors as [{ "$.alias": [...] }], which it can't map.
        var service = Substitute.For<IScheduledTaskService>();
        service.SetProperties(Id, Arg.Any<IReadOnlyDictionary<string, JsonNode?>?>(), Arg.Any<string?>())
            .Returns(ScheduledTaskCommandResult.Invalid(
                "Some properties need attention.",
                new Dictionary<string, string[]> { ["olderThanDays"] = ["“Older than days”: The number is too large."] }));
        var controller = new ScheduledTasksController(service, Substitute.For<IBackOfficeSecurityAccessor>(), TestSupport.Options());
        var result = Assert.IsType<ObjectResult>(controller.SetProperties(
            new SetScheduledTaskPropertiesRequest(Id, [new ScheduledTaskPropertyValueDto("olderThanDays", 3000000000)])));

        var jsonOptions = new JsonOptions();
        new ConfigureUmbracoBackofficeJsonOptions(new UmbracoJsonTypeInfoResolver(Substitute.For<ITypeFinder>()))
            .Configure(Constants.JsonOptionsNames.BackOffice, jsonOptions);
        var body = JsonNode.Parse(JsonSerializer.Serialize(result.Value, result.Value!.GetType(), jsonOptions.JsonSerializerOptions))!;

        Assert.Equal("Some properties need attention.", (string)body["title"]!);
        Assert.Equal(400, (int)body["status"]!);
        var errors = Assert.IsType<JsonObject>(body["errors"]);
        Assert.Equal("""["“Older than days”: The number is too large."]""", errors["olderThanDays"]!.ToJsonString(
            new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    [Fact]
    public void Null_or_aliasless_value_entries_are_a_400_not_a_crash()
    {
        var service = Substitute.For<IScheduledTaskService>();
        var controller = new ScheduledTasksController(service, Substitute.For<IBackOfficeSecurityAccessor>(), TestSupport.Options());

        foreach (var values in new[] { new ScheduledTaskPropertyValueDto[] { null! }, [new ScheduledTaskPropertyValueDto(null!, 1)] })
        {
            var result = Assert.IsType<ObjectResult>(controller.SetProperties(new SetScheduledTaskPropertiesRequest(Id, values)));
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("alias", Assert.IsType<ProblemDetails>(result.Value).Title);
        }

        service.DidNotReceiveWithAnyArgs().SetProperties(default!, default, default);
    }
}

public enum Priority
{
    Low = 1,
    Normal = 2,
    High = 3,
}

[Flags]
public enum Weekdays
{
    None = 0,
    Mon = 1,
    Tue = 2,
}

public class ScheduledTaskPropertyRegressionTests
{
    private static readonly Guid Media = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ScheduledTaskPropertyValues _values = new(NullLogger<ScheduledTaskPropertyValues>.Instance);

    private static Dictionary<string, JsonNode?> EditorValues(string json)
        => JsonNode.Parse(json)!.AsObject().ToDictionary(p => p.Key, p => p.Value?.DeepClone());

    private static ScheduledTaskPropertyDefinition Field(Type type, string alias)
        => Assert.Single(ScheduledTaskPropertySchema.For(type), d => d.Alias == alias);

    public sealed class ValueTypeProperties
    {
        public int OlderThanDays { get; set; } = 365;

        public int? MaxItems { get; set; } = 10;

        public CleanupAction Action { get; set; } = CleanupAction.Unpublish;

        public DateOnly NotBefore { get; set; } = new(2026, 1, 1);

        public Guid Template { get; set; }

        public bool DryRun { get; set; } = true;

        [ScheduledTaskProperty(Editor = ScheduledTaskPropertyEditor.Dropdown, Options = ["daily", "weekly"])]
        public string Frequency { get; set; } = "weekly";

        [ScheduledTaskProperty(Editor = ScheduledTaskPropertyEditor.Dropdown, Options = ["daily", "weekly"])]
        public string? OptionalFrequency { get; set; }
    }

    [Fact]
    public void Non_nullable_value_types_and_string_dropdowns_are_required()
    {
        var type = typeof(ValueTypeProperties);
        Assert.True(Field(type, "olderThanDays").Mandatory);
        Assert.True(Field(type, "action").Mandatory);
        Assert.True(Field(type, "notBefore").Mandatory);
        Assert.True(Field(type, "template").Mandatory);
        Assert.True(Field(type, "frequency").Mandatory);
        Assert.False(Field(type, "maxItems").Mandatory);
        Assert.False(Field(type, "optionalFrequency").Mandatory);
        Assert.False(Field(type, "dryRun").Mandatory);

        // The dropdown then has no blank option, and the picker needs one page.
        Assert.Contains(Field(type, "action").Config, c => c.Alias == "placeholder");
        Assert.Equal(1, (int)Field(type, "template").Config.Single(c => c.Alias == "validationLimit").Value!["min"]!);
    }

    [Fact]
    public void Clearing_a_non_nullable_value_is_rejected_instead_of_binding_default_T()
    {
        var (json, errors) = _values.Prepare(
            typeof(ValueTypeProperties),
            EditorValues("""{"olderThanDays":null,"action":[],"notBefore":{"date":"","timeZone":null},"template":null,"frequency":[""],"maxItems":null}"""));

        Assert.Null(json);
        Assert.Equal(["action", "frequency", "notBefore", "olderThanDays", "template"], errors!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["“Older than days” is required."], errors["olderThanDays"]);
        Assert.False(errors.ContainsKey("maxItems")); // nullable: blank means no value
    }

    [Fact]
    public void A_nullable_value_can_be_cleared()
    {
        var (json, errors) = _values.Prepare(
            typeof(ValueTypeProperties),
            EditorValues($$"""{"maxItems":null,"template":"{{Media}}"}"""));

        Assert.Null(errors);
        var bound = _values.Bind<ValueTypeProperties>("recurring:X", json);
        Assert.Null(bound.MaxItems);
        Assert.Equal(365, bound.OlderThanDays);
    }

    [Fact]
    public void Properties_left_out_keep_their_defaults()
    {
        var (json, errors) = _values.Prepare(typeof(ValueTypeProperties), EditorValues($$"""{"template":"{{Media}}"}"""));

        Assert.Null(errors);
        var bound = _values.Bind<ValueTypeProperties>("recurring:X", json);
        Assert.Equal(365, bound.OlderThanDays);
        Assert.Equal(CleanupAction.Unpublish, bound.Action);
        Assert.True(bound.DryRun);
        Assert.Equal("weekly", bound.Frequency);
    }

    [Fact]
    public void An_empty_saved_value_for_a_required_property_binds_the_default()
    {
        const string saved = """{"olderThanDays":null,"action":[],"notBefore":null}""";

        var bound = _values.Bind<ValueTypeProperties>("recurring:X", saved);
        Assert.Equal(365, bound.OlderThanDays);
        Assert.Equal(CleanupAction.Unpublish, bound.Action);
        Assert.Equal(new DateOnly(2026, 1, 1), bound.NotBefore);

        var form = _values.Form(typeof(ValueTypeProperties), saved);
        Assert.Equal(365, (int)form.Values["olderThanDays"]!);
        Assert.Contains(form.Warnings, w => w.Contains("Older than days"));
    }

    public sealed class ToggleProperties
    {
        [ScheduledTaskProperty("Dry run", Mandatory = true)]
        public bool DryRun { get; set; } = true;
    }

    [Fact]
    public void Mandatory_has_no_effect_on_a_toggle()
    {
        var definition = Field(typeof(ToggleProperties), "dryRun");
        Assert.False(definition.Mandatory);

        var form = _values.Form(typeof(ToggleProperties), null).ToDto();
        Assert.False(Assert.Single(form.Definitions).Validation.Mandatory);

        var (json, errors) = _values.Prepare(typeof(ToggleProperties), EditorValues("""{"dryRun":false}"""));
        Assert.Null(errors);
        Assert.False(_values.Bind<ToggleProperties>("recurring:X", json).DryRun);
    }

    public sealed class MediaProperties
    {
        [ScheduledTaskProperty(Editor = ScheduledTaskPropertyEditor.MediaPicker)]
        public Guid? Template { get; set; } = Media;

        public string? Note { get; set; }
    }

    [Fact]
    public void An_untouched_media_default_is_not_saved_as_a_customisation()
    {
        var form = _values.Form(typeof(MediaProperties), null);
        var (json, errors) = _values.Prepare(typeof(MediaProperties), form.Values);
        Assert.Null(errors);
        Assert.Null(json);

        // The picker gives each pick its own key; only the media key counts.
        var picked = EditorValues($$"""{"template":[{"key":"{{Guid.NewGuid()}}","mediaKey":"{{Media}}","mediaTypeAlias":"Image","crops":[],"focalPoint":null}]}""");
        Assert.Null(_values.Prepare(typeof(MediaProperties), picked).Json);
    }

    public sealed class SnapshotPropertiesV1
    {
        public string? Note { get; set; }

        public int BatchSize { get; set; } = 10000;

        public bool DryRun { get; set; } = true;
    }

    public sealed class SnapshotPropertiesV2
    {
        public string? Note { get; set; }

        public int BatchSize { get; set; } = 500;

        public bool DryRun { get; set; } = true;
    }

    [Fact]
    public void Only_values_that_differ_from_their_defaults_are_stored()
    {
        var form = _values.Form(typeof(SnapshotPropertiesV1), null);
        var values = form.Values.ToDictionary(v => v.Key, v => v.Value?.DeepClone());
        values["note"] = "Hello";

        var (json, errors) = _values.Prepare(typeof(SnapshotPropertiesV1), values);

        Assert.Null(errors);
        Assert.Equal("""{"note":"Hello"}""", json);
        // A later default reaches the job, because nobody set it.
        Assert.Equal(500, _values.Bind<SnapshotPropertiesV2>("recurring:X", json).BatchSize);
    }

    public sealed class UnfitDefaultProperties
    {
        public Priority Level { get; set; }

        public Priority? OptionalLevel { get; set; }

        [ScheduledTaskProperty(Editor = ScheduledTaskPropertyEditor.Dropdown, Options = ["daily", "weekly"])]
        public string Frequency { get; set; } = "monthly";

        public Weekdays Days { get; set; } = Weekdays.Mon | Weekdays.Tue;

        public DateTimeOffset At { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));

        [ScheduledTaskProperty(Editor = ScheduledTaskPropertyEditor.Date)]
        public DateTime Day { get; set; } = new(2026, 1, 1, 12, 30, 0);

        public string? Note { get; set; }
    }

    [Theory]
    [InlineData("level", "Priority.0")]
    [InlineData("frequency", "monthly")]
    [InlineData("days", "Flags")]
    [InlineData("day", "would change")]
    public void Defaults_the_editor_cant_round_trip_are_reported_as_definition_errors(string alias, string expected)
    {
        var definition = Field(typeof(UnfitDefaultProperties), alias);

        Assert.Contains(expected, definition.Error);
        Assert.Null(definition.Converter);
        Assert.Null(definition.PropertyEditorUiAlias);
    }

    [Fact]
    public void A_form_with_unfit_defaults_still_saves_untouched_and_the_job_keeps_those_defaults()
    {
        Assert.Null(Field(typeof(UnfitDefaultProperties), "optionalLevel").Error);
        Assert.Null(Field(typeof(UnfitDefaultProperties), "at").Error);

        var form = _values.Form(typeof(UnfitDefaultProperties), null);
        var values = form.Values.ToDictionary(v => v.Key, v => v.Value?.DeepClone());
        values["note"] = "x";
        var (json, errors) = _values.Prepare(typeof(UnfitDefaultProperties), values);

        Assert.Null(errors);
        Assert.Equal("""{"note":"x"}""", json);
        var bound = _values.Bind<UnfitDefaultProperties>("recurring:X", json);
        Assert.Equal("monthly", bound.Frequency);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), bound.At);
    }

    public sealed class OrdinaryDefaults
    {
        public double Ratio { get; set; } = 0.1;

        public double Third { get; set; } = 1.0 / 3;

        public float Factor { get; set; } = 0.1f;

        public decimal Price { get; set; } = 1.50m;

        public long Big { get; set; } = long.MaxValue;

        public DateTime Start { get; set; } = new(2026, 1, 1, 8, 30, 0, DateTimeKind.Utc);

        public List<Guid> Pages { get; set; } = [Media];

        [ScheduledTaskProperty(Editor = ScheduledTaskPropertyEditor.ContentPicker)]
        public string? PageKey { get; set; } = Media.ToString("B").ToUpperInvariant();
    }

    [Fact]
    public void Ordinary_defaults_round_trip_and_stay_editable()
    {
        Assert.All(ScheduledTaskPropertySchema.For(typeof(OrdinaryDefaults)), d => Assert.Null(d.Error));

        var form = _values.Form(typeof(OrdinaryDefaults), null);
        var (json, errors) = _values.Prepare(typeof(OrdinaryDefaults), form.Values);
        Assert.Null(errors);
        Assert.Null(json);
    }

    public sealed class MandatoryShapes
    {
        [ScheduledTaskProperty(Mandatory = true)]
        public Guid? Root { get; set; }

        [ScheduledTaskProperty(Mandatory = true)]
        public string? Title { get; set; }

        [ScheduledTaskProperty(Mandatory = true)]
        public DateOnly? From { get; set; }
    }

    [Fact]
    public void Values_of_the_wrong_shape_for_mandatory_properties_are_field_errors_not_exceptions()
    {
        var (json, errors) = _values.Prepare(
            typeof(MandatoryShapes),
            EditorValues("""{"root":[{"type":"document","unique":"MEDIA"}],"title":["a"],"from":{"date":{"x":1}}}""".Replace("MEDIA", Media.ToString())));

        Assert.Null(json);
        Assert.Equal(["from", "root", "title"], errors!.Keys.Order(StringComparer.Ordinal));
        Assert.Contains("Expected a single value.", errors["title"][0]);
    }
}
