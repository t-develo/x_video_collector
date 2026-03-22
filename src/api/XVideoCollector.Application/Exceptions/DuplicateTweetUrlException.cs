namespace XVideoCollector.Application.Exceptions;

public sealed class DuplicateTweetUrlException(string tweetId)
    : Exception($"A video with TweetId '{tweetId}' is already registered.")
{
    public string TweetId { get; } = tweetId;
}
