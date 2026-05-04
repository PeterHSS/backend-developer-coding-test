namespace Api.Tests.Infrastructure.ExternalServices.HackerNews;

public class HackerNewsServiceTests
{
    private const string CacheKey = "hackernews:beststories";

    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();
    private readonly IDistributedLockFactory _lockFactory = Substitute.For<IDistributedLockFactory>();
    private readonly IRedLock _redLock = Substitute.For<IRedLock>();
    private readonly ILogger<HackerNewsService> _logger = Substitute.For<ILogger<HackerNewsService>>();

    private readonly HackerNewsOptions _options = new()
    {
        BaseUrl = "https://test.com/",
        TimeoutInSeconds = 10,
        LockExpiryInSeconds = 30,
        LockWaitInSeconds = 5,
        LockRetryIntervalInMilliseconds = 100,
        MaxRetryAttempts = 3,
        CacheExpiryTimeInSeconds = 60,
        MaxDegreeOfParallelism = 10,
        DelayRetryInMilliseconds = 500
    };

    public HackerNewsServiceTests()
    {
        _lockFactory
            .CreateLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_redLock));
    }

    private HackerNewsService CreateSut(Func<HttpRequestMessage, HttpResponseMessage>? httpHandler = null)
    {
        var handler = httpHandler ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(new FakeHttpMessageHandler(handler))
        {
            BaseAddress = new Uri(_options.BaseUrl)
        };
        return new HackerNewsService(_cache, _lockFactory, new HackerNewsHttpClient(httpClient), _options, _logger);
    }

    private static byte[] Serialize<T>(T value) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));

    private static HttpResponseMessage JsonResponse<T>(T data) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task GetBestStoriesAsync_WhenCacheHit_ReturnsCachedStoriesWithoutAcquiringLock()
    {
        var stories = new[] { new Story { Id = 1, Title = "Cached", Score = 100 } };
        _cache.GetAsync(CacheKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(Serialize(stories)));

        var result = await CreateSut().GetBestStoriesAsync(1);

        result.Single().Title.Should().Be("Cached");
        await _lockFactory.DidNotReceive()
            .CreateLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenCacheHit_ReturnsOnlyRequestedCount()
    {
        var stories = Enumerable.Range(1, 10)
            .Select(i => new Story { Id = i, Score = i })
            .ToArray();
        _cache.GetAsync(CacheKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(Serialize(stories)));

        var result = await CreateSut().GetBestStoriesAsync(3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenLockNotAcquired_ThrowsCacheLockNotAcquiredException()
    {
        _cache.GetAsync(CacheKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));
        _redLock.IsAcquired.Returns(false);

        await CreateSut()
            .Invoking(s => s.GetBestStoriesAsync(5))
            .Should().ThrowAsync<CacheLockNotAcquiredException>();
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenLockNotAcquired_ExceptionContainsConfiguredRetryAfterSeconds()
    {
        _cache.GetAsync(CacheKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));
        _redLock.IsAcquired.Returns(false);

        var ex = await Assert.ThrowsAsync<CacheLockNotAcquiredException>(
            () => CreateSut().GetBestStoriesAsync(5));

        ex.RetryAfterSeconds.Should().Be(_options.LockExpiryInSeconds);
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenCacheMissAndLockAcquired_FetchesFromApiAndCaches()
    {
        _cache.GetAsync(CacheKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));
        _redLock.IsAcquired.Returns(true);

        var ids = new[] { 1, 2 };
        var item1 = new HackerNewsItem("u1", 10, 1, [], 200, 1000L, "Story A", "story", "https://a.com");
        var item2 = new HackerNewsItem("u2", 5, 2, [], 100, 2000L, "Story B", "story", "https://b.com");

        var sut = CreateSut(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("beststories.json")) return JsonResponse(ids);
            if (req.RequestUri.PathAndQuery.Contains("item/1.json")) return JsonResponse(item1);
            if (req.RequestUri.PathAndQuery.Contains("item/2.json")) return JsonResponse(item2);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await sut.GetBestStoriesAsync(2);

        result.Should().HaveCount(2);
        await _cache.Received(1).SetAsync(
            CacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenFetchedFromApi_StoriesSortedByScoreDescending()
    {
        _cache.GetAsync(CacheKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));
        _redLock.IsAcquired.Returns(true);

        var ids = new[] { 1, 2, 3 };
        var items = new HackerNewsItem[]
        {
            new("u", 0, 1, [], 50, 0, "Low", "story", null!),
            new("u", 0, 2, [], 300, 0, "High", "story", null!),
            new("u", 0, 3, [], 150, 0, "Mid", "story", null!),
        };

        var sut = CreateSut(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("beststories.json")) return JsonResponse(ids);
            foreach (var item in items)
                if (req.RequestUri.PathAndQuery.Contains($"item/{item.Id}.json"))
                    return JsonResponse(item);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = (await sut.GetBestStoriesAsync(3)).ToList();

        result[0].Score.Should().Be(300);
        result[1].Score.Should().Be(150);
        result[2].Score.Should().Be(50);
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenSecondCacheCheckHitsAfterLock_DoesNotCallApi()
    {
        var stories = new[] { new Story { Id = 1, Title = "Populated by other instance" } };
        _cache.GetAsync(CacheKey, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<byte[]?>(null),
                Task.FromResult<byte[]?>(Serialize(stories)));
        _redLock.IsAcquired.Returns(true);

        var apiCalled = false;
        var sut = CreateSut(req =>
        {
            apiCalled = true;
            return JsonResponse(Array.Empty<int>());
        });

        var result = await sut.GetBestStoriesAsync(1);

        apiCalled.Should().BeFalse();
        result.Single().Title.Should().Be("Populated by other instance");
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenStoryByIdReturnsNull_ExcludesItFromResult()
    {
        _cache.GetAsync(CacheKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));
        _redLock.IsAcquired.Returns(true);

        var ids = new[] { 1, 2 };
        var validItem = new HackerNewsItem("u", 0, 1, [], 100, 0, "Valid", "story", "https://a.com");

        var sut = CreateSut(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("beststories.json")) return JsonResponse(ids);
            if (req.RequestUri.PathAndQuery.Contains("item/1.json")) return JsonResponse(validItem);
            if (req.RequestUri.PathAndQuery.Contains("item/2.json"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", Encoding.UTF8, "application/json")
                };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await sut.GetBestStoriesAsync(10);

        result.Should().HaveCount(1);
        result.Single().Title.Should().Be("Valid");
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenStoryHasNullUrl_SetsUrlToEmptyString()
    {
        _cache.GetAsync(CacheKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));
        _redLock.IsAcquired.Returns(true);

        var sut = CreateSut(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("beststories.json"))
                return JsonResponse(new[] { 1 });
            if (req.RequestUri.PathAndQuery.Contains("item/1.json"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"By":"u","Descendants":0,"Id":1,"Kids":[],"Score":100,"Time":0,"Title":"No URL","Type":"story","Url":null}""",
                        Encoding.UTF8, "application/json")
                };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = (await sut.GetBestStoriesAsync(1)).Single();

        result.Url.Should().BeEmpty();
    }
}
