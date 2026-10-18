# Esatto.Umbraco.Backoffice.ScheduledTasks

A backoffice GUI for Umbraco 18's [scheduled background jobs](https://docs.umbraco.com/umbraco-cms/extend-your-project/server-side-extensions/scheduling).
A Settings-section dashboard lists every recurring and distributed task (Umbraco's own and your site's) and lets you manage them:

- **See** each task's schedule, status, last run (outcome, duration, error) and next run
- **Run now**: trigger any task immediately, even a paused one
- **Stop**: ask a running task to stop, on whichever server runs it
- **Pause / resume**: stop a task's scheduled runs without a deploy
- **Change the schedule**: an interval (every 30 minutes, every 2 weeks) or calendar times (every Monday at 07:00, the 1st of every month, every year on 15 March, or any cron expression) in your time zone, with a preview of the next runs
- **Retries and time limits**: try a failed run again (so many times, so long apart) and fail a run that takes too long; defaults in code, editable in the dashboard
- **Properties**: let editors set what a job works on, such as the page a clean-up job starts from, with Umbraco's own editors (content tree picker, dropdowns, toggles…)
- **Progress**: a job can report how far it has got, shown as a progress bar while it runs
- **History**: every server's runs, saved in the database, with the exception and stack trace of failures and **the log lines each run wrote**
- **Change log**: who paused, rescheduled, ran or stopped a task, and what they changed
- **Export / import**: move every task's settings from one environment to another, with a preview of what changes
- **English and Swedish**, following each user's backoffice language
- Works with **existing jobs unchanged**: no base class or registration change required. Properties and retries are opt-in per job.
- Pause, schedule, property values and run history are **stored in the database** and survive restarts

## How it works

Open **Settings → Scheduled Tasks**.

![Scheduled Tasks dashboard](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.ScheduledTasks/docs/scheduled-tasks-dashboard.png)

Click a task's schedule to change it. **Interval** runs it every so many seconds, minutes, hours, days or weeks. **Daily**, **Weekly** (pick the weekdays), **Monthly** (pick the day) and **Yearly** (pick the date) run it at a time of day, and **Cron** takes any five-field cron expression. Times are in the time zone you choose (your browser's by default), and the next five runs are previewed before you save. **Use the task's own schedule** puts it back.

![Changing a task's schedule](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.ScheduledTasks/docs/scheduled-tasks-schedule.png)

A task that declares properties (see [Task properties](#task-properties)) has a **Properties** button. It opens a form with Umbraco's own property editors; the job reads the saved values each time it runs.

![A task's properties](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.ScheduledTasks/docs/scheduled-tasks-properties.png)

**Retries & time limit** sets what happens when a run fails or takes too long (see [Retries and time limits](#retries-and-time-limits)). A task waiting for its retry shows **Retry pending**; a running task has a **Stop** button.

![Retries and time limit](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.ScheduledTasks/docs/scheduled-tasks-run-policy.png)

**History** opens the task's recent runs, from every server: who started a manual run, which runs were triggered from code or were retries (attempt 2 of 3), why a run failed, was skipped or stopped, and what the job logged while it ran.

![Run history with a run's log](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.ScheduledTasks/docs/scheduled-tasks-history.png)

Its **Changes** tab is the task's change log: who changed what, and when.

![A task's change log](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.ScheduledTasks/docs/scheduled-tasks-changes.png)

**Export** downloads every task's settings as a JSON file; **Import** on another environment (staging → production) shows what the file would change before anything is saved. See [Export and import](#export-and-import).

![Importing settings](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.ScheduledTasks/docs/scheduled-tasks-import.png)

Pausing one of Umbraco's own tasks asks for confirmation first, because some of them (cache instruction processing, scheduled publishing, clean-up) keep the site healthy.

The dashboard follows each user's backoffice language: English, or Swedish for users set to Svenska.

![The dashboard in Swedish](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.ScheduledTasks/docs/scheduled-tasks-swedish.png)

## Getting started

### 1. Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.ScheduledTasks
```

**Nothing to wire up.** The package registers itself through its composer and creates its table at startup. Every background job the site already has shows up in **Settings → Scheduled Tasks**, Umbraco's own included. The steps below are only needed for new jobs, or to use [task properties](#task-properties) and [progress](#progress).

### 2. Write a job

Jobs are plain Umbraco background jobs. A **recurring** job runs in-process on every server whose role matches (by default the single server, or the scheduling publisher in a load-balanced setup):

```csharp
using Umbraco.Cms.Infrastructure.BackgroundJobs;

public sealed class NewsletterDigestJob(ILogger<NewsletterDigestJob> logger)
    : RecurringBackgroundJobBase(TimeSpan.FromHours(1))   // the task's own interval; editors can change it
{
    // First run after the site starts (Umbraco's default is 3 minutes).
    public override TimeSpan Delay => TimeSpan.FromMinutes(5);

    public override async Task RunJobAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Sending the newsletter digest");
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);  // … the real work …
    }
}
```

A **distributed** job runs on one server at a time, coordinated through the database (Umbraco polls for due jobs every 5 seconds):

```csharp
using Umbraco.Cms.Infrastructure.BackgroundJobs;

public sealed class SitemapPingJob(IHttpClientFactory httpClientFactory) : IDistributedBackgroundJob
{
    public string Name => "SitemapPing";                  // its id in the dashboard: keep it stable
    public TimeSpan Period => TimeSpan.FromMinutes(30);

    public Task ExecuteAsync() => ExecuteAsync(CancellationToken.None);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient();
        await client.GetAsync("https://www.example.com/ping?sitemap=…", cancellationToken);
    }
}
```

### 3. Register it

With Umbraco's standard registrations, in a composer:

```csharp
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Extensions;

public sealed class JobsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddRecurringBackgroundJob<NewsletterDigestJob>();
        builder.Services.AddSingleton<IDistributedBackgroundJob, SitemapPingJob>();
    }
}
```

### 4. Manage it

Start the site and open **Settings → Scheduled Tasks**. From there an editor can run the job now, pause it, set its schedule (every 15 minutes, every Monday at 07:00, the 1st of every month…), edit its properties and follow its runs. Changes are stored in the database and survive restarts and deploys; the code's period is the default that **Use the task's own schedule** goes back to.

### 5. Optional extras

- **Properties** editors set for the job, such as which part of the content tree it works on: see [Task properties](#task-properties).
- **A progress bar** while the job runs: see [Progress](#progress).
- **Retries and a time limit**: `[ScheduledTaskRunPolicy(Retries = 3, RetryDelaySeconds = 60, TimeoutSeconds = 600)]` on the job: see [Retries and time limits](#retries-and-time-limits).
- **Stoppable runs**: pass the `CancellationToken` your job gets on to what it awaits (or check it in loops), and **Stop** and the time limit can end it early. See [Stopping a run](#stopping-a-run).
- **Logs in the history**: nothing to do. Whatever the job logs through `ILogger` while it runs is kept with the run. See [Run logs](#run-logs).
- **Run it from code**, for example right after content is published, with Umbraco's trigger. The run shows in the history as *Triggered from code* and respects a pause:

  ```csharp
  using Umbraco.Cms.Core.Events;
  using Umbraco.Cms.Core.Notifications;
  using Umbraco.Cms.Infrastructure.BackgroundJobs;
  using Umbraco.Cms.Infrastructure.HostedServices; // NextExecutionStrategy

  // The job opts in to triggering by implementing the marker interface.
  public sealed class SitemapRebuildJob() : RecurringBackgroundJobBase(TimeSpan.FromHours(6)), ITriggerableRecurringBackgroundJob
  {
      public override Task RunJobAsync(CancellationToken cancellationToken) => Task.CompletedTask; // …
  }

  public sealed class RebuildSitemapOnPublish(IRecurringBackgroundJobTrigger<SitemapRebuildJob> trigger)
      : INotificationHandler<ContentPublishedNotification>
  {
      // Run now; NextExecutionStrategy.Reset then waits a whole interval before the next scheduled run.
      public void Handle(ContentPublishedNotification notification) => trigger.TriggerExecution(NextExecutionStrategy.Reset);
  }

  // In the composer:
  // builder.AddNotificationHandler<ContentPublishedNotification, RebuildSitemapOnPublish>();
  ```

- **Configuration** for the dashboard itself (history size, refresh interval, allowed interval range): see [Configuration](#configuration).

## Task properties

Tools such as Hangfire schedule jobs but leave their input to code or config. Here a job can declare **properties** that editors set in the dashboard: a job that removes outdated articles can work below a page an editor picks in the content tree, instead of walking the whole site.

1. Write a plain class whose public read/write properties are the settings. Property initialisers are the defaults.
2. Mark the job with `IScheduledTaskWithProperties<TProperties>`.
3. Read the values when the job runs with `IScheduledTaskProperties.Get(this)`.

```csharp
using Esatto.Umbraco.Backoffice.ScheduledTasks;

public enum OutdatedArticleAction { MoveToRecycleBin, Unpublish }

public sealed class RemoveOutdatedArticlesProperties
{
    [ScheduledTaskProperty("Articles root", Description = "Only articles **below** this page are checked.", Mandatory = true)]
    public Guid? ArticlesRoot { get; set; }                       // content tree picker

    [ScheduledTaskProperty("Older than (days)")]
    public int OlderThanDays { get; set; } = 365;                 // number

    public OutdatedArticleAction Action { get; set; }             // dropdown

    [ScheduledTaskProperty("Dry run", Description = "Only log what would happen.")]
    public bool DryRun { get; set; } = true;                      // toggle
}

public sealed class RemoveOutdatedArticlesJob(IScheduledTaskProperties properties, IContentService contentService /* … */)
    : RecurringBackgroundJobBase(TimeSpan.FromDays(1)), IScheduledTaskWithProperties<RemoveOutdatedArticlesProperties>
{
    public override async Task RunJobAsync(CancellationToken cancellationToken)
    {
        var settings = properties.Get(this);
        if (settings.ArticlesRoot is not { } rootKey)
            throw new InvalidOperationException("Pick an articles root under Properties in Settings → Scheduled Tasks.");

        var root = contentService.GetById(rootKey);
        // … only the articles below root, older than settings.OlderThanDays …
    }
}
```

Register the job as usual (`builder.Services.AddRecurringBackgroundJob<RemoveOutdatedArticlesJob>();`); `IScheduledTaskProperties` is registered by the package. The task then gets a **Properties** button in the dashboard.

It works the same for `IDistributedBackgroundJob`s. `Get` returns a new instance on every call, with the saved values over the defaults, so read it at the start of each run.

**Editors by type.** The editor is chosen from the property's type; `[ScheduledTaskProperty(Editor = …)]` picks another one.

| Property type | Editor | Value |
|---------------|--------|-------|
| `string` | Text box (`Editor = TextArea` for a text area; `Editor = Dropdown` with `Options = [...]` for a fixed list) | the text |
| `int`, `long`, `short`, `byte` | Number | whole numbers only |
| `decimal`, `double`, `float` | Decimal | |
| `bool` | Toggle | |
| `DateOnly` / `DateTime`, `DateTimeOffset` | Date picker / date and time picker | as entered, no time zone |
| any `enum` | Dropdown of the enum's values | |
| `Guid` / `Guid?` | Content tree picker (one page) | the page's key |
| `Guid[]`, `List<Guid>`, `IReadOnlyList<Guid>`, … | Multi-node tree picker (several pages) | the pages' keys |
| `Guid?` or a Guid list with `Editor = MediaPicker` | Media picker | the media items' keys |
| anything, with `EditorUiAlias = "…"` | Any property editor UI, your own included (see [below](#your-own-property-editors)) | the editor's JSON, read into the type |

- `[ScheduledTaskProperty("Label", Description = "…", Mandatory = true)]` sets the label (default: the property name in sentence case), the help text (Markdown) and whether the field must be filled in. Mandatory fields are checked both in the form and on the server. Non-nullable value types (`int`, `DateOnly`, an enum, `Guid`…) and non-nullable `string` dropdowns are always required, since a blank would otherwise become `0`, the first enum member or `Guid.Empty`; make the property nullable (`int?`) to allow "no value". `Mandatory` has no effect on a `bool`: a toggle is always on or off.
- `[ScheduledTaskPropertyIgnore]` leaves a property out of the form.
- A property of an unsupported type is listed in the form as not editable, with the reason; the rest of the form still works. So is a property whose default its editor can't show and save back unchanged (an enum default that isn't a declared member, a dropdown default that isn't one of the `Options`, a `[Flags]` enum); the job keeps that default.
- Only values that differ from their defaults are stored, so a property nobody changed follows the initialiser, including after you change it in code. Values are stored as the editors emit them. If you later change a property's type and a saved value no longer fits, the job gets the default for that property (and a warning is logged), and the form shows the default with a note.

### Your own property editors

Any property editor UI registered in the backoffice can edit a property: one you built, a third-party one, or another of Umbraco's (tags, colour picker, …). Set `EditorUiAlias` to its alias and, if it takes settings, `ConfigJson` to its configuration (one member per config alias). The value the editor emits is stored as JSON and read into the property's type with `System.Text.Json` (camelCase names, enums by name or number), so it can be a `string`, a number, a list or your own class.

```csharp
public enum FeedFormat { Xml, Json }

public sealed class FeedSource
{
    public string Url { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public FeedFormat Format { get; set; } = FeedFormat.Xml;
}

public sealed class ProductFeedImportProperties
{
    [ScheduledTaskProperty("Feed", Description = "Where the products come from.",
        EditorUiAlias = "My.PropertyEditorUi.FeedSource",
        ConfigJson = """{ "allowHttp": false, "formats": ["xml", "json"] }""")]
    public FeedSource Feed { get; set; } = new() { Url = "https://feeds.example.com/products.xml" };

    // One of Umbraco's own editors works the same way.
    [ScheduledTaskProperty("Tags", EditorUiAlias = "Umb.PropertyEditorUi.Tags", ConfigJson = """{ "group": "feeds" }""")]
    public List<string> Tags { get; set; } = [];
}
```

The editor is an ordinary Umbraco property editor UI, registered in your site's `wwwroot/App_Plugins/<YourFolder>/umbraco-package.json`:

```json
{
  "id": "MySite.Editors",
  "name": "My site's editors",
  "version": "1.0.0",
  "extensions": [
    {
      "type": "propertyEditorUi",
      "alias": "My.PropertyEditorUi.FeedSource",
      "name": "Feed source",
      "element": "/App_Plugins/MySite.Editors/feed-source-editor.js",
      "meta": { "label": "Feed source", "icon": "icon-rss", "group": "common", "propertyEditorSchemaAlias": "Umbraco.Plain.Json" }
    }
  ]
}
```

The element gets the saved value in `value` and the settings in `config` (read them with `config.getValueByAlias('allowHttp')`), and dispatches `UmbChangeEvent` when the value changes:

```js
import { LitElement, html } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';

class FeedSourceEditor extends UmbElementMixin(LitElement) {
    static properties = { value: { type: Object } };

    set config(config) {
        this.allowHttp = config?.getValueByAlias('allowHttp') ?? true;
    }

    #update(changes) {
        this.value = { url: '', timeoutSeconds: 30, format: 'xml', ...this.value, ...changes };
        this.dispatchEvent(new UmbChangeEvent());
    }

    render() {
        return html`
            <uui-input label="Feed URL" .value=${this.value?.url ?? ''}
                @input=${(e) => this.#update({ url: e.target.value })}></uui-input>
            <uui-input label="Timeout (seconds)" type="number" .value=${String(this.value?.timeoutSeconds ?? 30)}
                @input=${(e) => this.#update({ timeoutSeconds: Number(e.target.value) })}></uui-input>`;
    }
}

customElements.define('my-feed-source-editor', FeedSourceEditor);
export default FeedSourceEditor;
```

- A value that doesn't fit the property's type (e.g. text where a number is expected) is rejected on save, with the reason under the field.
- `null`, `""` and `[]` count as empty. A non-nullable value type is required; a nullable or reference type can be cleared (the job then gets `null`).
- Checking the value's meaning (a valid URL, a sensible timeout) is up to the editor and the job, since the package can't know your format.
- If the alias isn't registered (a typo, or the editor's package isn't installed), Umbraco shows its own "missing editor" placeholder for that field.

## Progress

Like Hangfire's progress bars, a job can show how far it has got. Inject `IScheduledTaskProgress` and report a percentage (0–100) and/or a message as the work goes along. While the job runs, the dashboard shows a progress bar and the message under its status, updating every few seconds; the run history keeps the last progress of each run, so a failed run shows where it stopped.

![A run's progress in its history](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.ScheduledTasks/docs/scheduled-tasks-progress.png)

```csharp
using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

public sealed class SearchIndexRebuildJob(IScheduledTaskProgress progress, IMyIndexer indexer)
    : RecurringBackgroundJobBase(TimeSpan.FromHours(6))
{
    public override async Task RunJobAsync(CancellationToken cancellationToken)
    {
        var pages = await indexer.GetPagesAsync(cancellationToken);
        for (var i = 0; i < pages.Count; i++)
        {
            await indexer.IndexAsync(pages[i], cancellationToken);
            progress.Report(this, 100.0 * (i + 1) / pages.Count, $"Indexed {i + 1} of {pages.Count} pages");
        }
    }
}
```

- `Report(this, percent, message)`: a `null` percent only changes the message (for steps you can't count), and a `null` message keeps the previous one. Values outside 0–100 are clamped.
- It works the same for `IDistributedBackgroundJob`s and combines with properties.
- Reporting is an in-memory update, cheap enough for every item. The latest progress is saved with the run every few seconds (`RunHistoryFlushInterval`), so other servers' dashboards see it too, a little behind.
- Reports while no run of the job is in progress (e.g. from a constructor) are ignored.

## Retries and time limits

A failed run can be tried again, and a run that takes too long can be failed. Set the job's defaults with an attribute; editors can change them per task under **Retries & time limit** (and **Use the task's defaults** goes back to the attribute's values).

```csharp
using Esatto.Umbraco.Backoffice.ScheduledTasks;

// A failed import is tried 3 more times, 2 minutes apart; a run over 10 minutes is stopped and counts as failed.
[ScheduledTaskRunPolicy(Retries = 3, RetryDelaySeconds = 120, TimeoutSeconds = 600)]
public sealed class ProductFeedImportJob(IProductFeed feed) : RecurringBackgroundJobBase(TimeSpan.FromHours(1))
{
    public override async Task RunJobAsync(CancellationToken cancellationToken)
        => await feed.ImportAsync(cancellationToken);   // pass the token on: it's how a stop or the time limit reaches the work
}
```

| Setting | Default | Range |
|---------|---------|-------|
| `Retries` | `0` (no retries) | 0–10 |
| `RetryDelaySeconds` | `60` | 1 second – 1 day |
| `TimeoutSeconds` | `0` (no time limit) | 1 second – 7 days |

- Every attempt is its own entry in the history (*Retry · attempt 2 of 4*), with its own error and log. A run that fails all its attempts publishes Umbraco's `RecurringBackgroundJobFailedNotification` once, at the end.
- **Recurring tasks** wait for the retry in-process: the task shows as running (**Retry pending**) and its next scheduled run waits too. Pausing the task ends a scheduled run's retries; a run started with **Run now** keeps retrying.
- **Distributed tasks** don't wait: Umbraco's poller runs one distributed job at a time, so waiting would hold up all of them (scheduled publishing included). Instead the failed run records when it's tried again and the job's row in `umbracoDistributedJob` gets the period that makes it due then. Any server can pick the retry up; it runs on the first poll after the delay that has no other job due (Umbraco takes one due job per 5-second poll), so it may start a few seconds late. Pausing the task drops a pending retry.
- **Stop** (or **Cancel retry** on a distributed task that's waiting) ends a run's retries. A stopped run isn't retried.
- A time limit cancels the job's `CancellationToken` and records the run as *Timed out after 10 minutes*, which is retried like any failure. A job that ignores the token can't be stopped early: it runs on, and the run is recorded as finishing after its time limit.

## Stopping a run

A running task has a **Stop** button. It asks the run to stop through the `CancellationToken` the job got, on whichever server is running it: the server you're on stops it at once, other servers within a few seconds (they check for stop requests every `RunHistoryFlushInterval`). The run is recorded as *Canceled: Stopped by Carl*. Stopping only works for a job that observes the token: pass it on to what it awaits (`HttpClient`, EF Core, `Task.Delay`…) or check it in loops:

```cs
foreach (var page in pages)
{
    cancellationToken.ThrowIfCancellationRequested();
    await indexer.IndexAsync(page, cancellationToken);
}
```

A job that doesn't check it runs on until it finishes (the run is then recorded as finishing although it was asked to stop). Runs of Umbraco's own jobs can be stopped too, as far as they observe their token.

## Run logs

Everything a job logs through `ILogger` while it runs (and everything the code it calls logs, Umbraco's services included) is kept with the run and shown in its history, in the user's time zone. Nothing to set up: the package adds a Serilog sink that only picks up events logged inside a run.

- From `Information` up, at most 500 lines per run (`RunLogMinimumLevel`, `RunLogMaxLines`; `RunLogMinimumLevel: "None"` turns it off). Serilog's own minimum levels still apply first.
- The log goes wherever it went before too (files, the Log Viewer…): the run's copy is extra.
- Work a job starts but doesn't await (fire-and-forget) runs outside the run, so its logging isn't captured.

## Export and import

**Export** on the dashboard downloads every task's editor settings (pause, interval or calendar schedule, property values, retries and time limit) as JSON. Tasks on their defaults aren't in the file. **Import** on another environment reads such a file and first shows what it would change, task by task:

- **Changes**: what's different, e.g. *Schedule: Every hour → Every Monday at 07:00 (Europe/Stockholm)*
- **Not on this site**: tasks the file has that this site doesn't (skipped)
- **Can't be imported**: settings this site refuses (an interval outside the allowed range, an invalid schedule, a property value that no longer fits). Then nothing is imported until the file is fixed.
- Optionally, tasks the file doesn't mention are put back on their defaults.

The import is recorded in each changed task's change log. A calendar schedule that's the same as the current one keeps its bookkeeping, so no occurrence runs twice or goes missing. Property values are imported as they were saved: content picked in a content picker is identified by its key, which matches between environments kept in step by Umbraco Deploy or uSync, but not between unrelated sites.

## Change log

Every change made from the dashboard is recorded per task: pause and resume, interval and schedule changes (*Every hour → Every day at 07:00*), property changes (which properties), retries and time limit, **Run now**, **Stop**, cancelled retries, cleared history, reset running flags and imports, with who did it, when and on which server. It's in the **Changes** tab of the task's history, kept for a year (`AuditRetention`), and also written to the log at `Information` level.

## Languages

The dashboard and its sidebars are in English and Swedish, following each user's backoffice UI language (set per user under Users). Users with any other language see English. The package's own messages from the server (validation errors, a skipped run's reason) are in English.

Adding a language is a `localization` extension in your site's `umbraco-package.json` whose `esattoScheduledTasks` keys mirror the package's `lang/en.js` (in `wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.ScheduledTasks/lang/`); keys you leave out fall back to English.

## What it hooks into

The package doesn't run its own scheduler. It plugs into Umbraco's through DI registrations:

| Job kind | Hook | What you get |
|----------|------|--------------|
| `IRecurringBackgroundJob` (runs in-process on every server whose role matches) | Replaces the `Func<IRecurringBackgroundJob, IHostedService>` factory Umbraco's job runner uses, so each job is hosted by a subclass of Umbraco's `RecurringBackgroundJobHostedService<TJob>`; also replaces `IRecurringBackgroundJobTrigger<TJob>` | Pause, interval override (applied through the host's own `ChangePeriod`), calendar schedule, run now (through `TriggerExecution`), run log with exceptions. Umbraco's runtime-level, server-role and MainDom checks and its `RecurringBackgroundJob*Notification`s keep working. Triggers from your code (`IRecurringBackgroundJobTrigger<TJob>.TriggerExecution(...)`) still work: they show in the history and respect the pause. |
| `IDistributedBackgroundJob` (runs on one server at a time, scheduled through `umbracoDistributedJob`) | Decorates `IDistributedJobService` | Pause, run log, interval override or calendar schedule written to the job's row as its period (re-applied after Umbraco re-syncs the table at startup), run now (takes the job under Umbraco's `DistributedJobs` lock, so no other server runs it concurrently; shutdown waits for it like Umbraco's own poller). |

**Run now** respects Umbraco's rules: a recurring task can only be started from a server whose role it runs on, and a task that is already running isn't started twice.

**Possibly stuck.** If the site stops in the middle of a distributed task's run, its row stays marked as running, and Umbraco won't run the task again until the period plus `MaximumExecutionTime` has passed. A task marked as running for longer than `MaximumExecutionTime` (5 minutes by default) shows as **Possibly stuck**, with a **Reset** button that clears the flag after a confirmation. The reset is refused if the task was picked up again since the dashboard showed it (a fresh run isn't stuck).

### Calendar schedules

A calendar schedule ("every Monday at 07:00") replaces the task's interval; switching back to an interval removes it. Under the hood it is a cron expression evaluated with Umbraco's own parser (`ICronTabParser`) in the schedule's time zone.

- **Recurring tasks** tick every 30 seconds and run once an occurrence has passed, so they start at most about 30 seconds late. There's no extra run after a restart, and the job's `Delay` doesn't hold the calendar up: a job with `Delay => Timeout.InfiniteTimeSpan` (one that otherwise only runs when triggered) runs on its calendar too. Each server stores the last occurrence it handled in the database (a recurring job runs on every server whose role matches, and each runs it), so a restart doesn't repeat it, and an occurrence that fell while the site was down runs once when it comes back (once, however many were missed). A server that starts after the schedule was saved and has no record of it (a new server, or a new machine name, e.g. a new container) counts from when it started, so it doesn't run a catch-up for times before it existed. Switching back to an interval counts the interval from the switch. Until the settings have been read from the database (e.g. before the package's migration has run), only **Run now** runs a recurring task: whether it is paused isn't known yet.
- **Distributed tasks** get the period that makes Umbraco's own poller run them at the next occurrence (within about 5 seconds), recomputed after every run, so it works the same across load-balanced servers. The occurrence each scheduled run serves is stored in the database (one record for all servers), and the next one is counted from it rather than from when the run finished; a scheduled pick-up before the pending occurrence (e.g. while a restarting server re-registers the jobs) is skipped. While a calendar run is going, the task's period in Umbraco's job table is at least its own period (or the time to the following occurrence, if longer), so Umbraco doesn't consider a long run stale and start it again on another server any sooner than it would on an interval. A job that aligns its runs to the clock (`AlignToClock`, Umbraco 18.1+) can't use a calendar schedule, as Umbraco reads its period as clock boundaries; give it an interval instead.
- **Daylight saving.** A time that doesn't exist (the hour skipped in spring) runs when the clock resumes; a time that happens twice (the hour repeated in autumn) runs once, at its first instance, also when a run ends (or the site comes back) inside the repeated hour. Monthly schedules on the 29th–31st skip months without that day, and 29 February runs in leap years.
- **Pausing** skips the occurrences that fall while paused; resuming doesn't run them afterwards. **Run now** runs the task once without moving the schedule; if it is still running at an occurrence, that occurrence runs as soon as it finishes.
- Saving a schedule never triggers a run for a time that has already passed.
- **Cron** expressions have five fields (minute hour day-of-month month day-of-week, Sunday = 0) and at most 200 characters. As in standard cron, when both day fields are restricted a day matches either: `0 9 1 * 1` runs on the 1st of every month and every Monday. A schedule that doesn't run in the next five years is refused.
- **Time zones** are IANA (`Europe/Stockholm`, `UTC`, `Etc/UTC`) or Windows ids the server knows. A few zones newer than the server's time zone data (e.g. `America/Ciudad_Juarez`, `Asia/Urumqi`) use an older zone with the same clock times; any other zone the server doesn't know is refused with a message naming it.
- A stored schedule this version can't read (edited by hand, or saved by a newer version) is logged as a warning and the task runs as if it had none; the stored value is kept until a new schedule is saved for the task.

### Long intervals

- **Distributed tasks** keep their last run in the database, so a task every few weeks runs on schedule across restarts.
- **Recurring tasks** on an interval run in-process. Like any `IRecurringBackgroundJob`, they also run once `Delay` after every start of the site (a job that should only run when triggered needs `Delay => Timeout.InfiniteTimeSpan` as well as an infinite period); for long intervals that usually matters more than the interval, so the schedule editor says so. A calendar schedule doesn't have this extra run.
- Umbraco's recurring-job loop waits on a .NET timer, which can't wait longer than about 49.7 days (it throws, and the job silently stops running until the site restarts). For intervals above 24 days the package therefore ticks the loop hourly and skips ticks until the interval has passed since the last run. Skipped ticks publish no notifications and leave no history. A task that couldn't run when it was due (paused, or not yet allowed on this server) runs within the hour once it can; **Run now** on such an overdue task counts as that run.
- Code triggers that push the next run further out (`NextExecutionStrategy.Replace`, or a next delay over 24 days) are reached in steps the timer can wait, so the run still happens when it's due. `TriggerExecution(Timeout.InfiniteTimeSpan)` leaves the task to triggers alone (no next run shown) until a `Reset` or next-delay trigger, or a change of its interval. Should the loop fault anyway, it is logged as an error and restarted.

## Load balancing

- Pause, schedule and property values live in the database. The server that handled the change applies it immediately; every other server re-reads them every `SettingsRefreshInterval` (30 s by default) and applies schedule changes to its timer then.
- Distributed tasks are coordinated through the database, so run now, pause and schedule apply to the whole cluster. After every run the job's period is rewritten from the stored setting, so a server with an outdated copy can't leave a wrong schedule behind. Their interval can't be shorter than Umbraco's poll period (`Umbraco:CMS:DistributedJobs:Period`, 5 s by default): Umbraco takes one due job per poll, so a shorter one would keep the others from running.
- Run history is **saved in the database** by every server, so the history shows each run with the server it ran on, whichever server you're connected to. Each server writes its new and changed runs in one batch every `RunHistoryFlushInterval` (5 s), keeps the latest `RunHistoryLimit` per task and deletes runs older than `RunHistoryRetention`. What's running right now (with its live progress and log) is shown from the server's memory until it's written. Runs of tasks that run more often than `PersistAllRunsFromPeriod` (a minute; e.g. Umbraco's cache instruction processing every 5 seconds) are kept in memory only, unless they fail, are stopped or retried, so they don't write to the database all the time. The *runs and failures since startup* counters are per server.
- A run that was in progress when its server stopped is closed as *Interrupted* when that server starts again — or by any other server within about 15 minutes, for a server that never comes back under that name (a replaced container, a scaled-in instance). Each server marks its running rows as alive every flush.
- **Stop** reaches runs on other servers through the database (within `RunHistoryFlushInterval`). A distributed task's retry runs on whichever server picks it up.

## Configuration

All optional, under `Esatto:ScheduledTasks`:

```json
{
  "Esatto": {
    "ScheduledTasks": {
      "RunHistoryLimit": 50,
      "PersistRunHistory": true,
      "PersistAllRunsFromPeriod": "00:01:00",
      "RunHistoryRetention": "30.00:00:00",
      "RunHistoryFlushInterval": "00:00:05",
      "RunLogMinimumLevel": "Information",
      "RunLogMaxLines": 500,
      "AuditRetention": "365.00:00:00",
      "SettingsRefreshInterval": "00:00:30",
      "MinimumPeriod": "00:00:01",
      "MaximumPeriod": "3650.00:00:00"
    }
  }
}
```

| Setting | Default | Meaning |
|---------|---------|---------|
| `RunHistoryLimit` | `50` | Runs kept per task, in memory and in the database |
| `PersistRunHistory` | `true` | Save runs in the database; `false` keeps them in memory on each server only (reset on restart) |
| `PersistAllRunsFromPeriod` | 1 minute | Tasks running more often than this only save failed, stopped and retried runs |
| `RunHistoryRetention` | 30 days | Saved runs older than this are deleted (hourly) |
| `RunHistoryFlushInterval` | 5 seconds | How often each server writes its runs and picks up stop requests (1 second – 1 hour) |
| `RunLogMinimumLevel` | `Information` | Lowest level of the log lines kept with a run; `None` turns run logs off |
| `RunLogMaxLines` | `500` | Log lines kept per run; the rest are counted |
| `AuditRetention` | 365 days | Change log entries older than this are deleted |
| `SettingsRefreshInterval` | 30 seconds | How often each server re-reads pause / schedule / property values (kept between 1 second and 24 days) |
| `MinimumPeriod` / `MaximumPeriod` | 1 second / 10 years | Range an editor may set an interval to |

## Endpoints

The Management API is at:

```
GET    /umbraco/management/api/v1/backoffice/scheduled-tasks
GET    /umbraco/management/api/v1/backoffice/scheduled-tasks/runs?id={taskId}
DELETE /umbraco/management/api/v1/backoffice/scheduled-tasks/runs?id={taskId}
POST   /umbraco/management/api/v1/backoffice/scheduled-tasks/run      { "id": "…" }
POST   /umbraco/management/api/v1/backoffice/scheduled-tasks/pause    { "id": "…" }
POST   /umbraco/management/api/v1/backoffice/scheduled-tasks/resume   { "id": "…" }
POST   /umbraco/management/api/v1/backoffice/scheduled-tasks/reset    { "id": "…", "runningSinceUtc": "…" }
PUT    /umbraco/management/api/v1/backoffice/scheduled-tasks/period   { "id": "…", "periodSeconds": 300 }
PUT    /umbraco/management/api/v1/backoffice/scheduled-tasks/schedule { "id": "…", "schedule": { "kind": "weekly", "time": "07:00", "weekdays": ["Monday"], "timeZone": "Europe/Stockholm" } }
POST   /umbraco/management/api/v1/backoffice/scheduled-tasks/schedule/preview { "schedule": { … }, "count": 5 }
GET    /umbraco/management/api/v1/backoffice/scheduled-tasks/properties?id={taskId}
PUT    /umbraco/management/api/v1/backoffice/scheduled-tasks/properties { "id": "…", "values": [{ "alias": "…", "value": … }] }
POST   /umbraco/management/api/v1/backoffice/scheduled-tasks/stop     { "id": "…" }
PUT    /umbraco/management/api/v1/backoffice/scheduled-tasks/run-policy { "id": "…", "runPolicy": { "retries": 3, "retryDelaySeconds": 60, "timeoutSeconds": 600 } }
GET    /umbraco/management/api/v1/backoffice/scheduled-tasks/changes?id={taskId}
GET    /umbraco/management/api/v1/backoffice/scheduled-tasks/export
POST   /umbraco/management/api/v1/backoffice/scheduled-tasks/import   { "document": { …export… }, "apply": false, "resetMissing": false }
```

Task ids are `recurring:<job type full name>` or `distributed:<job name>`. A `periodSeconds` of `null` restores the task's own schedule (dropping an interval override or calendar), `schedule: null` goes back to an interval, and `values: null` restores all property defaults. In a run policy a value left out (or `null`) follows the job's default, `timeoutSeconds: 0` means no time limit, and `runPolicy: null` restores all defaults. An import with `apply: false` only compares. Schedule kinds are `daily`, `weekly`, `monthly` (with `dayOfMonth`), `yearly` (with `dayOfMonth` and `month`) and `cron` (with `cron`).
Errors come back as ProblemDetails; invalid property values as a 400 ProblemDetails whose `errors` lists the problems by property alias (`{ "olderThanDays": ["…"] }`). Property values left out of `values` keep their defaults.
All endpoints require `AuthorizationPolicies.SectionAccessSettings`.

## Database

- Table: `ScheduledTaskSettings`, with one row per task an editor changed (pause, interval or calendar schedule, property values as JSON, retries and time limit as JSON, and the last calendar occurrence a distributed task served). Resetting a task to its defaults deletes the row.
- Table: `ScheduledTaskRuns`, the run history (one row per run and server: outcome, error, attempt, pending retry, stop request, captured log). Indexes `IX_ScheduledTaskRuns_runId` (unique) and `IX_ScheduledTaskRuns_taskId_startedUtc`.
- Table: `ScheduledTaskAudit`, the change log (index `IX_ScheduledTaskAudit_taskId_utc`).
- Unique index: `IX_ScheduledTaskSettings_taskId`
- Table: `ScheduledTaskOccurrences`, with the last calendar occurrence each server handled for each recurring task (unique index `IX_ScheduledTaskOccurrences_taskId_server`; the server is its machine name, as in the run history).
- Created and extended by idempotent migrations at startup (SQL Server and SQLite). Until they have run, no settings are read: distributed tasks wait for the next poll rather than ignore a pause, and stored distributed schedules are applied once the settings can be read.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 18.x    | Verified (18.2.1) |
| 17.x    | Not supported (the package targets Umbraco 18) |

## License

MIT.
