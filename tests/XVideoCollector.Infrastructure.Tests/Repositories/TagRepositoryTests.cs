using Microsoft.EntityFrameworkCore;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Infrastructure.Persistence;
using XVideoCollector.Infrastructure.Repositories;

namespace XVideoCollector.Infrastructure.Tests.Repositories;

public sealed class TagRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ITagRepository _sut;

    public TagRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _sut = new TagRepository(_db);
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsTag()
    {
        var tag = Tag.Create("TestTag", TagColor.Blue, TimeProvider.System);

        await _sut.AddAsync(tag);
        await _db.SaveChangesAsync();
        var result = await _sut.GetByIdAsync(tag.Id);

        Assert.NotNull(result);
        Assert.Equal("TestTag", result.Name);
        Assert.Equal(TagColor.Blue, result.Color);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSortedByName()
    {
        await _sut.AddAsync(Tag.Create("Zebra", TagColor.Red, TimeProvider.System));
        await _sut.AddAsync(Tag.Create("Alpha", TagColor.Green, TimeProvider.System));
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha", result[0].Name);
        Assert.Equal("Zebra", result[1].Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTag()
    {
        var tag = Tag.Create("ToDelete", TagColor.Gray, TimeProvider.System);
        await _sut.AddAsync(tag);
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync(tag.Id);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(tag.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var tag = Tag.Create("Original", TagColor.Red, TimeProvider.System);
        await _sut.AddAsync(tag);
        await _db.SaveChangesAsync();

        tag.Update("Updated", TagColor.Purple, TimeProvider.System);
        await _sut.UpdateAsync(tag);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(tag.Id);
        Assert.NotNull(result);
        Assert.Equal("Updated", result.Name);
        Assert.Equal(TagColor.Purple, result.Color);
    }

    public void Dispose() => _db.Dispose();
}
