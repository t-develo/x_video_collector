using Microsoft.EntityFrameworkCore;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Infrastructure.Persistence;
using XVideoCollector.Infrastructure.Repositories;

namespace XVideoCollector.Infrastructure.Tests.Repositories;

public sealed class CategoryRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ICategoryRepository _sut;

    public CategoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _sut = new CategoryRepository(_db);
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsCategory()
    {
        var category = Category.Create("Gaming", 1, TimeProvider.System);

        await _sut.AddAsync(category);
        await _db.SaveChangesAsync();
        var result = await _sut.GetByIdAsync(category.Id);

        Assert.NotNull(result);
        Assert.Equal("Gaming", result.Name);
        Assert.Equal(1, result.SortOrder);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSortedBySortOrder()
    {
        await _sut.AddAsync(Category.Create("ZCategory", 2, TimeProvider.System));
        await _sut.AddAsync(Category.Create("ACategory", 1, TimeProvider.System));
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("ACategory", result[0].Name);
        Assert.Equal("ZCategory", result[1].Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCategory()
    {
        var category = Category.Create("ToDelete", 0, TimeProvider.System);
        await _sut.AddAsync(category);
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync(category.Id);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(category.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var category = Category.Create("Original", 0, TimeProvider.System);
        await _sut.AddAsync(category);
        await _db.SaveChangesAsync();

        category.Update("Updated", 5, TimeProvider.System);
        await _sut.UpdateAsync(category);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(category.Id);
        Assert.NotNull(result);
        Assert.Equal("Updated", result.Name);
        Assert.Equal(5, result.SortOrder);
    }

    public void Dispose() => _db.Dispose();
}
