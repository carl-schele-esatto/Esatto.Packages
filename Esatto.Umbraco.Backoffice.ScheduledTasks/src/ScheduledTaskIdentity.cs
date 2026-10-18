using System.Text;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Stable ids and human-readable names for scheduled tasks.
/// </summary>
/// <remarks>
/// Recurring jobs are identified by their CLR type (Umbraco hosts one service per job type);
/// distributed jobs by their <c>Name</c> (the key of their row in <c>umbracoDistributedJob</c>).
/// The kind prefix keeps the two id spaces apart.
/// </remarks>
public static class ScheduledTaskIdentity
{
    public const string RecurringPrefix = "recurring:";
    public const string DistributedPrefix = "distributed:";

    // Product names that PascalCase splitting would mangle ("OpenIddict" → "Open iddict").
    private static readonly string[] PreservedWords = ["OpenIddict"];

    public static string ForRecurring(Type jobType) => RecurringPrefix + (jobType.FullName ?? jobType.Name);

    public static string ForDistributed(string jobName) => DistributedPrefix + jobName;

    /// <summary>The id of a job instance: distributed jobs by name, recurring jobs by type.</summary>
    /// <param name="job">The job.</param>
    /// <param name="purpose">Ends the error message, e.g. "to report progress".</param>
    internal static string ForJob(object job, string purpose) => job switch
    {
        IDistributedBackgroundJob distributed => ForDistributed(distributed.Name),
        IRecurringBackgroundJob => ForRecurring(job.GetType()),
        _ => throw new ArgumentException(
            $"{job.GetType().Name} must be an IRecurringBackgroundJob or IDistributedBackgroundJob {purpose}.",
            nameof(job)),
    };

    public static bool IsRecurring(string id) => id.StartsWith(RecurringPrefix, StringComparison.Ordinal);

    public static bool IsDistributed(string id) => id.StartsWith(DistributedPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Turns a type or job name into a sentence-case label:
    /// <c>Umbraco.Cms.Infrastructure.BackgroundJobs.Jobs.TempFileCleanupJob</c> → <c>Temp file cleanup</c>.
    /// Acronyms survive (<c>SendHTMLReportJob</c> → <c>Send HTML report</c>). A trailing "Job" is
    /// dropped unless <paramref name="stripJobSuffix"/> is false (e.g. for property names).
    /// </summary>
    public static string Humanize(string name, bool stripJobSuffix = true)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        // Last segment of a namespace-qualified or nested type name, minus generic arity.
        var simple = name.Trim();
        var lastSeparator = simple.LastIndexOfAny(['.', '+']);
        if (lastSeparator >= 0 && lastSeparator < simple.Length - 1) simple = simple[(lastSeparator + 1)..];
        var tick = simple.IndexOf('`');
        if (tick > 0) simple = simple[..tick];

        if (stripJobSuffix && simple.Length > "Job".Length && simple.EndsWith("Job", StringComparison.Ordinal))
            simple = simple[..^"Job".Length];

        var words = SplitWords(simple);
        if (words.Count == 0) return simple;

        var builder = new StringBuilder();
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            if (i > 0)
            {
                builder.Append(' ');
                // Lower-case ordinary words after the first; keep acronyms (all caps, 2+ letters) and product names.
                if (!IsAcronym(word) && !PreservedWords.Contains(word)) word = word.ToLowerInvariant();
            }
            else if (!IsAcronym(word) && word.Length > 0)
            {
                word = char.ToUpperInvariant(word[0]) + word[1..];
            }

            builder.Append(word);
        }

        return builder.ToString();
    }

    private static List<string> SplitWords(string value)
    {
        var words = new List<string>();
        var current = new StringBuilder();

        for (var i = 0; i < value.Length; i++)
        {
            if ((current.Length == 0 || char.IsUpper(value[i]))
                && PreservedWords.FirstOrDefault(w => value.AsSpan(i).StartsWith(w, StringComparison.Ordinal)) is { } preserved)
            {
                Flush();
                words.Add(preserved);
                i += preserved.Length - 1;
                continue;
            }

            var c = value[i];
            if (c is '_' or '-' or ' ')
            {
                Flush();
                continue;
            }

            if (current.Length > 0 && char.IsUpper(c))
            {
                var previous = value[i - 1];
                var nextIsLower = i + 1 < value.Length && char.IsLower(value[i + 1]);
                // "fooBar" → foo|Bar ; "HTMLReport" → HTML|Report ; "Job2Run" stays digits-attached.
                if (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower))
                    Flush();
            }

            current.Append(c);
        }

        Flush();
        return words;

        void Flush()
        {
            if (current.Length == 0) return;
            words.Add(current.ToString());
            current.Clear();
        }
    }

    private static bool IsAcronym(string word) => word.Length > 1 && word.All(c => !char.IsLetter(c) || char.IsUpper(c));
}
