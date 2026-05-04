namespace Api.Tests.Features.Stories.GetBestStories;

public class GetBestStoriesEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(501)]
    [InlineData(1000)]
    public async Task GetBestStories_WhenTotalIsOutOfRange_Returns400(int total)
    {
        var response = await _client.GetAsync($"/stories/best/{total}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task GetBestStories_WhenTotalIsValid_Returns200(int total)
    {
        var response = await _client.GetAsync($"/stories/best/{total}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBestStories_ReturnsJsonContentType()
    {
        var response = await _client.GetAsync("/stories/best/5");

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetBestStories_ReturnsBadRequest_WithProblemDetails_WhenTotalIsZero()
    {
        var response = await _client.GetAsync("/stories/best/0");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Invalid request");
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["HackerNews:BaseUrl"] = "https://hacker-news.firebaseio.com/v0/",
                ["HackerNews:TimeoutInSeconds"] = "10",
                ["HackerNews:LockExpiryInSeconds"] = "30",
                ["HackerNews:LockWaitInSeconds"] = "5",
                ["HackerNews:LockRetryIntervalInMilliseconds"] = "100",
                ["HackerNews:MaxRetryAttempts"] = "3",
                ["HackerNews:CacheExpiryTimeInSeconds"] = "60",
                ["HackerNews:MaxDegreeOfParallelism"] = "10",
                ["HackerNews:DelayRetryInMilliseconds"] = "500"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.RemoveAll<IDistributedLockFactory>();
            services.RemoveAll<IConnectionMultiplexer>();

            var redLock = Substitute.For<IRedLock>();
            redLock.IsAcquired.Returns(true);

            var lockFactory = Substitute.For<IDistributedLockFactory>();
            lockFactory
                .CreateLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(redLock));
            services.AddSingleton(lockFactory);

            services.RemoveAll<IGetBestStoriesUseCase>();
            var useCase = Substitute.For<IGetBestStoriesUseCase>();
            useCase.HandleAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult((IEnumerable<StoryResponse>)Array.Empty<StoryResponse>()));
            services.AddScoped(_ => useCase);
        });
    }
}
