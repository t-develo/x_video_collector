using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Domain.Tests.ValueObjects;

public class BlobPathTests
{
    [Fact]
    public void Create_ValidPath_ReturnsInstance()
    {
        var path = BlobPath.Create("videos/2024/01/myvideo.mp4");

        Assert.Equal("videos/2024/01/myvideo.mp4", path.Value);
    }

    [Fact]
    public void Create_PathWithLeadingSlash_IsNormalized()
    {
        var path = BlobPath.Create("/videos/myvideo.mp4");

        Assert.Equal("videos/myvideo.mp4", path.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_NullOrWhiteSpace_ThrowsArgumentException(string? path)
    {
        Assert.Throws<ArgumentException>(() => BlobPath.Create(path!));
    }

    [Fact]
    public void Create_OnlySlashes_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BlobPath.Create("///"));
    }

    [Fact]
    public void BlobPath_SameValue_AreEqual()
    {
        var p1 = BlobPath.Create("videos/test.mp4");
        var p2 = BlobPath.Create("videos/test.mp4");

        Assert.Equal(p1, p2);
    }
}
