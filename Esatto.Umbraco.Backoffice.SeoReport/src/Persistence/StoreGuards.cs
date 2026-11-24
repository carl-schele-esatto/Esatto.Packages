namespace Esatto.Umbraco.Backoffice.SeoReport.Persistence;

/// <summary>
/// The argument rules both stores enforce, so the in-memory double refuses what the database would. SQL Server rejects a
/// value longer than its column and SQLite silently keeps it; failing the same way everywhere keeps a bug from hiding in one.
/// </summary>
internal static class StoreGuards
{
    public static void DocumentKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        MaxLength(key, DocumentEntity.KeyLength, nameof(key));
    }

    public static void Note(string text) => MaxLength(text, NoteEntity.MaxLength, nameof(text));

    public static void Lock(string name, string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        MaxLength(name, LockEntity.NameLength, nameof(name));
        MaxLength(owner, LockEntity.OwnerLength, nameof(owner));
    }

    /// <summary>A lock that lives no time at all is a bug in the caller, not a lock.</summary>
    public static void Lifetime(TimeSpan ttl) => ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

    private static void MaxLength(string value, int max, string name)
    {
        if (value.Length > max) throw new ArgumentException($"At most {max} characters.", name);
    }
}
