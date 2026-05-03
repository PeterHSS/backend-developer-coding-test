using Api.Domain.Entities;

namespace Api.Domain.Abstractions.Infrastructure;

public interface IHackerNewsService
{
    Task<IEnumerable<Story>> GetBestStoriesAsync(int count, CancellationToken cancellationToken = default);
}