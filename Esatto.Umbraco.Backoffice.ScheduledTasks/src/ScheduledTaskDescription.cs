namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// What a job does, shown when an editor expands the task in the dashboard:
/// <code>
/// [ScheduledTaskDescription("Imports the product feed.", Swedish = "Importerar produktflödet.")]
/// public class ProductFeedImportJob : RecurringBackgroundJobBase { … }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class ScheduledTaskDescriptionAttribute(string english) : Attribute
{
    /// <summary>The description in English, the fallback for every language.</summary>
    public string English { get; } = english;

    /// <summary>The description in Swedish; null shows the English text to Swedish editors.</summary>
    public string? Swedish { get; set; }
}

/// <summary>A job's description in both dashboard languages (the dashboard picks one).</summary>
public sealed record ScheduledTaskJobDescription(string English, string Swedish);

/// <summary>Resolves a job's description: its <see cref="ScheduledTaskDescriptionAttribute"/>, else a canned one for Umbraco's own jobs.</summary>
public static class ScheduledTaskJobDescriptions
{
    private const string Jobs = "Umbraco.Cms.Infrastructure.BackgroundJobs.Jobs.";

    // Keyed by the job type's full name; every type exists in Umbraco.Cms.Infrastructure 18.
    private static readonly IReadOnlyDictionary<string, ScheduledTaskJobDescription> Umbraco =
        new Dictionary<string, ScheduledTaskJobDescription>(StringComparer.Ordinal)
        {
            [Jobs + "DistributedJobs.ScheduledPublishingJob"] = new(
                "Publishes and unpublishes content at the dates set in the content's schedule.",
                "Publicerar och avpublicerar innehåll vid de datum som anges i innehållets schemaläggning."),
            [Jobs + "DistributedJobs.ContentVersionCleanupJob"] = new(
                "Deletes old versions of content that are past the retention period set in the content history cleanup settings.",
                "Raderar gamla versioner av innehåll som har passerat den lagringstid som anges i inställningarna för rensning av innehållshistorik."),
            [Jobs + "DistributedJobs.HealthCheckNotifierJob"] = new(
                "Runs the health checks and sends the results to the configured notification methods, such as email or webhooks.",
                "Kör hälsokontrollerna och skickar resultaten via de inställda aviseringssätten, till exempel e-post eller webhooks."),
            [Jobs + "DistributedJobs.LogScrubberJob"] = new(
                "Deletes audit log entries that are older than the configured maximum age.",
                "Raderar granskningsloggposter som är äldre än den inställda maxåldern."),
            [Jobs + "DistributedJobs.TemporaryFileCleanupJob"] = new(
                "Deletes temporary media files, such as uploads that were never saved, once they have expired.",
                "Raderar tillfälliga mediafiler, till exempel uppladdningar som aldrig sparades, när de har gått ut."),
            [Jobs + "TempFileCleanupJob"] = new(
                "Deletes old files from Umbraco's temporary folder.",
                "Raderar gamla filer från Umbracos tillfälliga mapp."),
            [Jobs + "DistributedJobs.CacheInstructionsPruningJob"] = new(
                "Deletes old cache instructions from the database. Servers use them to keep their caches in sync, and old ones are no longer needed.",
                "Raderar gamla cacheinstruktioner från databasen. Servrar använder dem för att hålla sina cacher synkade, och gamla behövs inte längre."),
            [Jobs + "ServerRegistration.InstructionProcessJob"] = new(
                "Reads cache instructions from other servers in the database and applies them, so this server's caches stay in sync.",
                "Läser cacheinstruktioner från andra servrar i databasen och tillämpar dem, så att den här serverns cacher hålls synkade."),
            [Jobs + "ServerRegistration.TouchServerJob"] = new(
                "Updates this server's registration in the database to show that it is active.",
                "Uppdaterar den här serverns registrering i databasen för att visa att den är aktiv."),
            [Jobs + "DistributedJobs.WebhookFiring"] = new(
                "Sends pending webhook requests to their endpoints and retries the ones that failed.",
                "Skickar väntande webhook-anrop till sina mottagare och försöker igen med de som misslyckades."),
            [Jobs + "DistributedJobs.WebhookLoggingCleanup"] = new(
                "Deletes webhook logs that are older than the configured retention period.",
                "Raderar webhook-loggar som är äldre än den inställda lagringstiden."),
            [Jobs + "ReportSiteJob"] = new(
                "Reports anonymous telemetry about this site to Umbraco, according to the telemetry level in the settings.",
                "Rapporterar anonym telemetri om webbplatsen till Umbraco, enligt telemetrinivån i inställningarna."),
            [Jobs + "DistributedJobs.LongRunningOperationsCleanupJob"] = new(
                "Deletes records of long-running operations, such as bulk actions, once they are old enough.",
                "Raderar poster om långvariga åtgärder, till exempel massåtgärder, när de är tillräckligt gamla."),
            [Jobs + "DistributedJobs.OpenIddictCleanupJob"] = new(
                "Deletes expired and orphaned backoffice authorizations and tokens.",
                "Raderar utgångna och föräldralösa behörigheter och token för backoffice."),
        };

    /// <summary>The job type's description, or null when neither the job nor Umbraco's list has one.</summary>
    public static ScheduledTaskJobDescription? For(Type jobType)
    {
        if (jobType.GetCustomAttributes(typeof(ScheduledTaskDescriptionAttribute), inherit: true).FirstOrDefault()
            is ScheduledTaskDescriptionAttribute attribute
            && !string.IsNullOrWhiteSpace(attribute.English))
        {
            return new ScheduledTaskJobDescription(
                attribute.English,
                string.IsNullOrWhiteSpace(attribute.Swedish) ? attribute.English : attribute.Swedish);
        }

        return jobType.FullName is { } name && Umbraco.TryGetValue(name, out var canned) ? canned : null;
    }

    internal static IReadOnlyCollection<string> CannedTypeNames => (IReadOnlyCollection<string>)Umbraco.Keys;
}
