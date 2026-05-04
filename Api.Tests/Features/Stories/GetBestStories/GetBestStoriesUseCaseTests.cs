namespace Api.Tests.Features.Stories.GetBestStories;

public class GetBestStoriesUseCaseTests
{
    private readonly IHackerNewsService _service = Substitute.For<IHackerNewsService>();
    private readonly GetBestStoriesUseCase _sut;

    public GetBestStoriesUseCaseTests()
    {
        _sut = new GetBestStoriesUseCase(_service);
    }

    [Fact]
    public async Task HandleAsync_MapsAllFieldsCorrectly()
    {
        var story = new Story
        {
            Id = 1,
            Title = "Test Story",
            PostedBy = "author",
            Score = 250,
            CommentCount = 42,
            Url = "https://example.com",
            Time = 1609459200
        };
        _service.GetBestStoriesAsync(1, Arg.Any<CancellationToken>())
            .Returns(new[] { story });

        var result = (await _sut.HandleAsync(1)).Single();

        result.Title.Should().Be("Test Story");
        result.Uri.Should().Be("https://example.com");
        result.PostedBy.Should().Be("author");
        result.Score.Should().Be(250);
        result.CommentCount.Should().Be(42);
        result.Time.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1609459200));
    }

    [Fact]
    public async Task HandleAsync_ConvertsUnixTimestampToDateTimeOffset()
    {
        const long unixTimestamp = 1700000000L;
        _service.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new Story { Time = unixTimestamp } });

        var result = (await _sut.HandleAsync(1)).Single();

        result.Time.Should().Be(DateTimeOffset.FromUnixTimeSeconds(unixTimestamp));
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenServiceReturnsNoStories()
    {
        _service.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Story>());

        var result = await _sut.HandleAsync(5);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ForwardsTotalAndCancellationTokenToService()
    {
        using var cts = new CancellationTokenSource();
        _service.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Story>());

        await _sut.HandleAsync(42, cts.Token);

        await _service.Received(1).GetBestStoriesAsync(42, cts.Token);
    }

    [Fact]
    public async Task HandleAsync_MapsMultipleStories()
    {
        var stories = Enumerable.Range(1, 5)
            .Select(i => new Story { Id = i, Title = $"Story {i}" })
            .ToArray();
        _service.GetBestStoriesAsync(5, Arg.Any<CancellationToken>())
            .Returns(stories);

        var result = await _sut.HandleAsync(5);

        result.Should().HaveCount(5);
    }
}
