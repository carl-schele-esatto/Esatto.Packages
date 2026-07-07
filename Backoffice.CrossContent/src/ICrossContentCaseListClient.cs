namespace Backoffice.CrossContent;

public interface ICrossContentCaseListClient
{
    Task<IReadOnlyList<CrossContentCaseListItem>> ListCasesAsync(CancellationToken ct);
}
