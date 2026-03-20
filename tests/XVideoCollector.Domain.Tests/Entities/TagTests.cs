using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Domain.Tests.Entities;

public class TagTests
{
    [Fact]
    public void Create_ValidArgs_ReturnsTag()
    {
        var tag = Tag.Create("anime", TagColor.Blue, TimeProvider.System);

        Assert.NotEqual(Guid.Empty, tag.Id);
        Assert.Equal("anime", tag.Name);
        Assert.Equal(TagColor.Blue, tag.Color);
    }

    [Fact]
    public void Create_NameWithWhitespace_IsTrimmed()
    {
        var tag = Tag.Create("  anime  ", TagColor.Red, TimeProvider.System);

        Assert.Equal("anime", tag.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_NullOrWhiteSpaceName_ThrowsArgumentException(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => Tag.Create(name!, TagColor.Blue, TimeProvider.System));
    }

    [Fact]
    public void Update_ChangesNameAndColor()
    {
        var tag = Tag.Create("anime", TagColor.Blue, TimeProvider.System);

        tag.Update("manga", TagColor.Red, TimeProvider.System);

        Assert.Equal("manga", tag.Name);
        Assert.Equal(TagColor.Red, tag.Color);
    }
}
