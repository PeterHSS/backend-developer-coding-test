namespace Api.Tests.Middleware;

public class GlobalExceptionHandlerTests
{
    private readonly ILogger<GlobalExceptionHandler> _logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
    private GlobalExceptionHandler CreateSut() => new(_logger);

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task TryHandleAsync_CacheLockNotAcquiredException_Returns503()
    {
        var context = CreateHttpContext();

        await CreateSut().TryHandleAsync(context, new CacheLockNotAcquiredException(30), CancellationToken.None);

        context.Response.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task TryHandleAsync_CacheLockNotAcquiredException_SetsRetryAfterHeader()
    {
        var context = CreateHttpContext();

        await CreateSut().TryHandleAsync(context, new CacheLockNotAcquiredException(30), CancellationToken.None);

        context.Response.Headers.RetryAfter.ToString().Should().Be("30");
    }

    [Fact]
    public async Task TryHandleAsync_CacheLockNotAcquiredException_UsesRetryAfterFromException()
    {
        var context = CreateHttpContext();

        await CreateSut().TryHandleAsync(context, new CacheLockNotAcquiredException(60), CancellationToken.None);

        context.Response.Headers.RetryAfter.ToString().Should().Be("60");
    }

    [Fact]
    public async Task TryHandleAsync_UnhandledException_Returns500()
    {
        var context = CreateHttpContext();

        await CreateSut().TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        context.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task TryHandleAsync_CacheLockNotAcquiredException_ReturnsTrue()
    {
        var context = CreateHttpContext();

        var result = await CreateSut().TryHandleAsync(context, new CacheLockNotAcquiredException(30), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryHandleAsync_UnhandledException_ReturnsTrue()
    {
        var context = CreateHttpContext();

        var result = await CreateSut().TryHandleAsync(context, new Exception("any"), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryHandleAsync_UnhandledException_DoesNotSetRetryAfterHeader()
    {
        var context = CreateHttpContext();

        await CreateSut().TryHandleAsync(context, new Exception("any"), CancellationToken.None);

        context.Response.Headers.ContainsKey("Retry-After").Should().BeFalse();
    }
}
