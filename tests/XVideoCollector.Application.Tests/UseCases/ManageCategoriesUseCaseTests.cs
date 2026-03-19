using Moq;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class ManageCategoriesUseCaseTests
{
    private readonly Mock<ICategoryRepository> _categoryRepoMock = new();
    private readonly ManageCategoriesUseCase _sut;

    public ManageCategoriesUseCaseTests()
    {
        _sut = new ManageCategoriesUseCase(_categoryRepoMock.Object);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMappedDtos()
    {
        var categories = new List<Category>
        {
            Category.Create("Music", 0),
            Category.Create("Gaming", 1),
        };
        _categoryRepoMock.Setup(r => r.GetAllAsync(default)).ReturnsAsync(categories);

        var result = await _sut.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Name == "Music");
    }

    [Fact]
    public async Task CreateAsync_ValidInput_ReturnsDto()
    {
        var result = await _sut.CreateAsync("Sports", 2);

        Assert.Equal("Sports", result.Name);
        Assert.Equal(2, result.SortOrder);
        _categoryRepoMock.Verify(r => r.AddAsync(It.IsAny<Category>(), default), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NonExistingCategory_ThrowsInvalidOperationException()
    {
        _categoryRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Category?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(Guid.NewGuid(), "Name", 0));
    }

    [Fact]
    public async Task DeleteAsync_ExistingCategory_CallsRepository()
    {
        var category = Category.Create("ToDelete", 0);
        _categoryRepoMock
            .Setup(r => r.GetByIdAsync(category.Id, default))
            .ReturnsAsync(category);

        await _sut.DeleteAsync(category.Id);

        _categoryRepoMock.Verify(r => r.DeleteAsync(category.Id, default), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingCategory_ThrowsInvalidOperationException()
    {
        _categoryRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Category?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(Guid.NewGuid()));
    }
}
