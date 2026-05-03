using Api.Domain.Abstractions.Infrastructure;

namespace Api.Features.Stories.GetBestStories;

internal sealed class GetBestStoriesUseCase(IHackerNewsService service) : IGetBestStoriesUseCase
{
    public async Task<IEnumerable<StoryResponse>> HandleAsync(int total, CancellationToken cancellationToken = default)
    {
        var stories = await service.GetBestStoriesAsync(total, cancellationToken);

        return stories.Select(s => new StoryResponse(
            s.Title,
            s.Url,
            s.PostedBy,
            DateTimeOffset.FromUnixTimeSeconds(s.Time),
            s.Score,
            s.CommentCount));
    }
}
