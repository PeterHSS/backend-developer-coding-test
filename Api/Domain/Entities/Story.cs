namespace Api.Domain.Entities;

public class Story
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PostedBy { get; set; } = string.Empty;
    public int Score { get; set; }
    public int CommentCount { get; set; }
    public string Url { get; set; } = string.Empty;
    public long Time { get; set; }
}
