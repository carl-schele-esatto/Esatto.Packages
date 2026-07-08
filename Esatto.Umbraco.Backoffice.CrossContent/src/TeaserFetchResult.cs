namespace Esatto.Umbraco.Backoffice.CrossContent;

public enum TeaserFetchOutcome { Ok, Gone, Failed }

public sealed record TeaserFetchResult(TeaserFetchOutcome Outcome, CrossContentTeaser? Teaser)
{
    public static readonly TeaserFetchResult Gone = new(TeaserFetchOutcome.Gone, null);
    public static readonly TeaserFetchResult Failed = new(TeaserFetchOutcome.Failed, null);
    public static TeaserFetchResult Ok(CrossContentTeaser t) => new(TeaserFetchOutcome.Ok, t);
}
