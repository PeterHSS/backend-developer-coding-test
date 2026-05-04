namespace Api.Tests.Infrastructure.ExternalServices.HackerNews;

public class HackerNewsHttpClientTests
{
    private static HackerNewsHttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://test.com/")
        };
        return new HackerNewsHttpClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse<T>(T data) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task GetBestStoriesIdsAsync_ReturnsIdsFromResponse()
    {
        var expected = new[] { 1, 2, 3 };
        var sut = CreateClient(_ => JsonResponse(expected));

        var result = await sut.GetBestStoriesIdsAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetBestStoriesIdsAsync_ReturnsEmpty_WhenResponseIsNull()
    {
        var sut = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        });

        var result = await sut.GetBestStoriesIdsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBestStoriesIdsAsync_CallsCorrectEndpoint()
    {
        string? capturedPath = null;
        var sut = CreateClient(req =>
        {
            capturedPath = req.RequestUri?.PathAndQuery;
            return JsonResponse(Array.Empty<int>());
        });

        await sut.GetBestStoriesIdsAsync();

        capturedPath.Should().Contain("beststories.json");
    }

    [Fact]
    public async Task GetStoryByIdAsync_ReturnsDeserializedItem()
    {
        var item = new HackerNewsItem("author", 5, 42, [], 100, 1609459200L, "Test Title", "story", "https://example.com");
        var sut = CreateClient(_ => JsonResponse(item));

        var result = await sut.GetStoryByIdAsync(42);

        result.Should().NotBeNull();
        result!.Id.Should().Be(42);
        result.Title.Should().Be("Test Title");
        result.By.Should().Be("author");
        result.Score.Should().Be(100);
        result.Url.Should().Be("https://example.com");
    }

    [Fact]
    public async Task GetStoryByIdAsync_ReturnsNull_WhenResponseIsNull()
    {
        var sut = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        });

        var result = await sut.GetStoryByIdAsync(1);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStoryByIdAsync_CallsCorrectEndpointWithId()
    {
        string? capturedPath = null;
        var sut = CreateClient(req =>
        {
            capturedPath = req.RequestUri?.PathAndQuery;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            };
        });

        await sut.GetStoryByIdAsync(12345);

        capturedPath.Should().Contain("item/12345.json");
    }
}
