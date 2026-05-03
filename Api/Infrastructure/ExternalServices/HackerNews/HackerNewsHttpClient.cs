namespace Api.Infrastructure.ExternalServices.HackerNews;

internal sealed class HackerNewsHttpClient(HttpClient httpClient)
{
    public async Task<IEnumerable<int>> GetBestStoriesIdsAsync(CancellationToken cancellationToken = default) 
        => await httpClient.GetFromJsonAsync<IEnumerable<int>>("beststories.json", cancellationToken: cancellationToken) ?? [];

    public async Task<HackerNewsItem?> GetStoryByIdAsync(int id, CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<HackerNewsItem>($"item/{id}.json", cancellationToken: cancellationToken);
}
