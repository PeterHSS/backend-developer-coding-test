namespace Api.Exceptions;

public sealed class CacheLockNotAcquiredException(int retryAfterSeconds)
    : Exception("Cache lock not acquired. Another instance is already populating the cache.")
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
