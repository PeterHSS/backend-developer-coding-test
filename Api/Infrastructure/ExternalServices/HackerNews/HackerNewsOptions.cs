namespace Api.Infrastructure.ExternalServices.HackerNews;

public class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    public string BaseUrl { get; init; } = string.Empty;
    public int TimeoutInSeconds { get; init; }
    public int LockExpiryInSeconds { get; init; }
    public int LockWaitInSeconds { get; init; }
    public int LockRetryIntervalInMilliseconds { get; init; }
    public int MaxRetryAttempts { get; init; }
    public int CacheExpiryTimeInSeconds { get; init; }
    public int MaxDegreeOfParallelism { get; init; }
    public int DelayRetryInMilliseconds { get; init; }
}
