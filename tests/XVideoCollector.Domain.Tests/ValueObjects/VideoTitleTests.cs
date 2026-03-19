using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Domain.Tests.ValueObjects;

public class VideoTitleTests
{
    [Fact]
    public void Create_ValidTitle_ReturnsInstance()
    {
        var title = VideoTitle.Create("My Video");

        Assert.Equal("My Video", title.Value);
    }

    [Fact]
    public void Create_TitleWithWhitespace_IsTrimmed()
    {
        var title = VideoTitle.Create("  My Video  ");

        Assert.Equal("My Video", title.Value);
    }

    [Fact]
    public void Create_MaxLengthTitle_Succeeds()
    {
        var longTitle = new string('a', VideoTitle.MaxLength);
        var title = VideoTitle.Create(longTitle);

        Assert.Equal(longTitle, title.Value);
    }

    [Fact]
    public void Create_TitleExceedingMaxLength_ThrowsArgumentException()
    {
        var tooLong = new string('a', VideoTitle.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => VideoTitle.Create(tooLong));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_NullOrWhiteSpace_ThrowsArgumentException(string? title)
    {
        Assert.Throws<ArgumentException>(() => VideoTitle.Create(title!));
    }

    [Fact]
    public void VideoTitle_SameValue_AreEqual()
    {
        var t1 = VideoTitle.Create("Test");
        var t2 = VideoTitle.Create("Test");

        Assert.Equal(t1, t2);
    }
}
