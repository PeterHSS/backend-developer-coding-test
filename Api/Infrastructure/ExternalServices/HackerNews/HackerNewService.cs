using System.Collections.Concurrent;
using System.Text.Json;
using Api.Domain.Abstractions.Infrastructure;
using Api.Domain.Entities;
using Api.Exceptions;
using Microsoft.Extensions.Caching.Distributed;
using RedLockNet;

namespace Api.Infrastructure.ExternalServices.HackerNews;

internal sealed class HackerNewService(
    IDistributedCache distributedCache,
    IDistributedLockFactory lockFactory,
    HackerNewsHttpClient client,
    HackerNewsOptions options,
    ILogger<HackerNewService> logger
    ) : IHackerNewsService
{
    private readonly string _cacheKey = "hackernews:beststories";
    private readonly string _lockKey = "hackernews:beststories:lock";

    private readonly TimeSpan _expiryTimeInSeconds = TimeSpan.FromSeconds(options.LockExpiryInSeconds);
    private readonly TimeSpan _cacheExpiryTimeInSeconds = TimeSpan.FromSeconds(options.CacheExpiryTimeInSeconds);
    private readonly TimeSpan _lockWaitInSeconds = TimeSpan.FromSeconds(options.LockWaitInSeconds);
    private readonly TimeSpan _lockRetryTimeInMilliseconds = TimeSpan.FromMilliseconds(options.LockRetryIntervalInMilliseconds);

    public async Task<IEnumerable<Story>> GetBestStoriesAsync(int count, CancellationToken cancellationToken = default)
    {
        var stories = await GetOrCreateStoriesAsync(cancellationToken);

        return stories.Take(count);
    }

    private async Task<IEnumerable<Story>> GetOrCreateStoriesAsync(CancellationToken cancellationToken)
    {
        var cached = await distributedCache.GetStringAsync(_cacheKey, cancellationToken);

        if (!string.IsNullOrWhiteSpace(cached))
            return JsonSerializer.Deserialize<IEnumerable<Story>>(cached) ?? [];

        await using var redLock = await lockFactory.CreateLockAsync(_lockKey, _expiryTimeInSeconds, _lockWaitInSeconds, _lockRetryTimeInMilliseconds, cancellationToken);

        if (!redLock.IsAcquired)
        {
            logger.LogWarning("Failed to acquire cache lock for key {LockKey} after {WaitSeconds}s. Another instance is populating the cache.", _lockKey, _lockWaitInSeconds);
            
            throw new CacheLockNotAcquiredException(options.LockExpiryInSeconds);
        }

        cached = await distributedCache.GetStringAsync(_cacheKey, cancellationToken);

        if (!string.IsNullOrWhiteSpace(cached))
            return JsonSerializer.Deserialize<IEnumerable<Story>>(cached) ?? [];

        var stories = await GetBestStoriesFromHackerNewsAsync(cancellationToken);

        await distributedCache.SetStringAsync(_cacheKey, JsonSerializer.Serialize(stories), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheExpiryTimeInSeconds
        }, cancellationToken);

        return stories;
    }

    private async Task<IEnumerable<Story>> GetBestStoriesFromHackerNewsAsync(CancellationToken cancellationToken)
    {
        var storyIds = await client.GetBestStoriesIdsAsync(cancellationToken);

        var stories = new ConcurrentBag<HackerNewsItem>();

        await Parallel.ForEachAsync(
            storyIds,
            new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism, CancellationToken = cancellationToken },
            async (id, cancellationToken) =>
            {
                var story = await client.GetStoryByIdAsync(id, cancellationToken);

                if (story is not null)
                    stories.Add(story);
            });

        return stories.Select(s => new Story
        {
            Id = s.Id,
            CommentCount = s.Descendants,
            PostedBy = s.By,
            Url = s.Url ?? string.Empty,
            Score = s.Score,
            Time = s.Time,
            Title = s.Title,
        }).OrderByDescending(s => s.Score);
    }
}
