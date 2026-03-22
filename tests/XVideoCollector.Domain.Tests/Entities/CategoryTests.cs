using XVideoCollector.Domain.Entities;

namespace XVideoCollector.Domain.Tests.Entities;

public sealed class CategoryTests
{
    [Fact]
    public void Create_ValidArgs_ReturnsCategory()
    {
        var category = Category.Create("Action", 1, TimeProvider.System);

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal("Action", category.Name);
        Assert.Equal(1, category.SortOrder);
    }

    [Fact]
    public void Create_NameWithWhitespace_IsTrimmed()
    {
        var category = Category.Create("  Action  ", 1, TimeProvider.System);

        Assert.Equal("Action", category.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_NullOrWhiteSpaceName_ThrowsArgumentException(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => Category.Create(name!, 1, TimeProvider.System));
    }

    [Fact]
    public void Create_NameAtMaxLength_Succeeds()
    {
        var maxName = new string('a', Category.MaxNameLength);

        var category = Category.Create(maxName, 1, TimeProvider.System);

        Assert.Equal(maxName, category.Name);
    }

    [Fact]
    public void Create_NameExceedsMaxLength_ThrowsArgumentException()
    {
        var longName = new string('a', Category.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => Category.Create(longName, 1, TimeProvider.System));
    }

    [Fact]
    public void Update_ChangesNameAndSortOrder()
    {
        var category = Category.Create("Action", 1, TimeProvider.System);

        category.Update("Comedy", 2, TimeProvider.System);

        Assert.Equal("Comedy", category.Name);
        Assert.Equal(2, category.SortOrder);
    }

    [Fact]
    public void Update_NameExceedsMaxLength_ThrowsArgumentException()
    {
        var category = Category.Create("Action", 1, TimeProvider.System);
        var longName = new string('a', Category.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => category.Update(longName, 1, TimeProvider.System));
    }
}
