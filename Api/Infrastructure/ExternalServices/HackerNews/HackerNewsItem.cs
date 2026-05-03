namespace Api.Infrastructure.ExternalServices.HackerNews;

public record HackerNewsItem(string By, int Descendants, int Id, int[] Kids, int Score, long Time, string Title, string Type, string Url);