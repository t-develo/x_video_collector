using System.Text.RegularExpressions;

namespace XVideoCollector.Domain.ValueObjects;

public sealed record TweetUrl
{
    private static readonly Regex UrlPattern = new(
        @"^https?://(twitter|x)\.com/(?<user>[A-Za-z0-9_]+)/status/(?<id>\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }
    public string TweetId { get; }
    public string UserName { get; }

    private TweetUrl(string value, string tweetId, string userName)
    {
        Value = value;
        TweetId = tweetId;
        UserName = userName;
    }

    public static TweetUrl Create(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var match = UrlPattern.Match(url.Trim());
        if (!match.Success)
            throw new ArgumentException($"Invalid X/Twitter URL: {url}", nameof(url));

        var tweetId = match.Groups["id"].Value;
        var userName = match.Groups["user"].Value;
        var normalized = $"https://x.com/{userName}/status/{tweetId}";

        return new TweetUrl(normalized, tweetId, userName);
    }

    public override string ToString() => Value;
}
