namespace XVideoCollector.Application.Dtos;

public sealed record RegisterVideoRequest(
    string TweetUrl,
    string Title);
