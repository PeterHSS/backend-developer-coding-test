namespace Api.Features.Stories.GetBestStories;

internal interface IGetBestStoriesUseCase
{
    Task<IEnumerable<StoryResponse>> HandleAsync(int total, CancellationToken cancellationToken = default);
}