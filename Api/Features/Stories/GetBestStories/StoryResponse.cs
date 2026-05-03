namespace Api.Features.Stories.GetBestStories;

public record StoryResponse(string Title, string Uri, string PostedBy, DateTimeOffset Time, int Score, int CommentCount);
