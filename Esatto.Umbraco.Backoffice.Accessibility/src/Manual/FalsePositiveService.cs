using Esatto.Umbraco.Backoffice.Accessibility.Persistence;

namespace Esatto.Umbraco.Backoffice.Accessibility.Manual;

/// <summary>
/// Marks findings as false positives. Kept per site, rule, page and (optionally) element, so the mark is
/// re-applied to later scans. A note is required: someone else will want to know why.
/// </summary>
public sealed class FalsePositiveService(IAccessibilityStore store, TimeProvider time)
{
    /// <summary>The column lengths.</summary>
    public const int MaxNoteLength = 1024, MaxSelectorLength = 1024, MaxRuleIdLength = 128;

    /// <param name="selector">The element's selector, or null for every occurrence of the rule on the page.</param>
    public Task<int> AddAsync(Guid siteKey, string ruleId, Guid contentKey, string? selector, string note, string userName)
    {
        if (string.IsNullOrWhiteSpace(note)) throw new ArgumentException("Say why this is a false positive.");
        if (note.Trim().Length > MaxNoteLength) throw new ArgumentException($"The note can be at most {MaxNoteLength} characters.");
        if (string.IsNullOrWhiteSpace(ruleId) || ruleId.Length > MaxRuleIdLength) throw new ArgumentException("A rule is required.");
        if (selector?.Length > MaxSelectorLength) throw new ArgumentException("The selector is too long.");

        return store.AddFalsePositiveAsync(new FalsePositiveEntity
        {
            SiteKey = siteKey,
            RuleId = ruleId,
            ContentKey = contentKey,
            Selector = string.IsNullOrWhiteSpace(selector) ? null : selector,
            Note = note.Trim(),
            UserName = userName,
            CreatedUtc = time.GetUtcNow().UtcDateTime,
        });
    }

    public Task RemoveAsync(int id) => store.DeleteFalsePositiveAsync(id);
}
