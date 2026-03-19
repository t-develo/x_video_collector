using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Domain.Tests.ValueObjects;

public class TweetUrlTests
{
    [Theory]
    [InlineData("https://x.com/user123/status/1234567890")]
    [InlineData("https://twitter.com/user123/status/1234567890")]
    [InlineData("http://x.com/user123/status/1234567890")]
    [InlineData("https://x.com/User_123/status/9876543210")]
    public void Create_ValidUrl_ReturnsInstance(string url)
    {
        var result = TweetUrl.Create(url);

        Assert.NotNull(result);
        Assert.StartsWith("https://x.com/", result.Value);
    }

    [Fact]
    public void Create_TwitterUrl_NormalizesToXCom()
    {
        var result = TweetUrl.Create("https://twitter.com/user123/status/1234567890");

        Assert.Equal("https://x.com/user123/status/1234567890", result.Value);
    }

    [Fact]
    public void Create_ValidUrl_ExtractsTweetId()
    {
        var result = TweetUrl.Create("https://x.com/user123/status/1234567890");

        Assert.Equal("1234567890", result.TweetId);
    }

    [Fact]
    public void Create_ValidUrl_ExtractsUserName()
    {
        var result = TweetUrl.Create("https://x.com/user123/status/1234567890");

        Assert.Equal("user123", result.UserName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_NullOrWhiteSpace_ThrowsArgumentException(string? url)
    {
        Assert.Throws<ArgumentException>(() => TweetUrl.Create(url!));
    }

    [Theory]
    [InlineData("https://example.com/user/status/123")]
    [InlineData("https://x.com/user/notStatus/123")]
    [InlineData("not a url")]
    [InlineData("https://x.com/user/status/abc")]
    public void Create_InvalidUrl_ThrowsArgumentException(string url)
    {
        Assert.Throws<ArgumentException>(() => TweetUrl.Create(url));
    }

    [Fact]
    public void TweetUrl_SameValues_AreEqual()
    {
        var url1 = TweetUrl.Create("https://x.com/user123/status/1234567890");
        var url2 = TweetUrl.Create("https://twitter.com/user123/status/1234567890");

        Assert.Equal(url1, url2);
    }

    [Fact]
    public void ToString_ReturnsNormalizedValue()
    {
        var url = TweetUrl.Create("https://twitter.com/user123/status/1234567890");

        Assert.Equal("https://x.com/user123/status/1234567890", url.ToString());
    }
}
