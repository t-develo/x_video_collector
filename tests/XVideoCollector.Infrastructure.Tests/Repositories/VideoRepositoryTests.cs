using Microsoft.EntityFrameworkCore;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;
using XVideoCollector.Infrastructure.Persistence;
using XVideoCollector.Infrastructure.Repositories;

namespace XVideoCollector.Infrastructure.Tests.Repositories;

public sealed class VideoRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IVideoRepository _sut;

    public VideoRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _sut = new VideoRepository(_db);
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsVideo()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/user/status/123456"),
            VideoTitle.Create("Test Video"),
            TimeProvider.System);

        await _sut.AddAsync(video);
        await _db.SaveChangesAsync();
        var result = await _sut.GetByIdAsync(video.Id);

        Assert.NotNull(result);
        Assert.Equal(video.Id, result.Id);
        Assert.Equal("https://x.com/user/status/123456", result.TweetUrl.Value);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllVideos()
    {
        var v1 = Video.Create(TweetUrl.Create("https://x.com/user/status/111"), VideoTitle.Create("V1"), TimeProvider.System);
        var v2 = Video.Create(TweetUrl.Create("https://x.com/user/status/222"), VideoTitle.Create("V2"), TimeProvider.System);

        await _sut.AddAsync(v1);
        await _sut.AddAsync(v2);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/user/status/123"),
            VideoTitle.Create("Original Title"),
            TimeProvider.System);
        await _sut.AddAsync(video);
        await _db.SaveChangesAsync();

        video.UpdateTitle(VideoTitle.Create("Updated Title"), TimeProvider.System);
        await _sut.UpdateAsync(video);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(video.Id);
        Assert.NotNull(result);
        Assert.Equal("Updated Title", result.Title.Value);
    }

    [Fact]
    public async Task DeleteAsync_RemovesVideo()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/user/status/999"),
            VideoTitle.Create("To Delete"),
            TimeProvider.System);
        await _sut.AddAsync(video);
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync(video.Id);
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(video.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task SearchAsync_ByStatus_FiltersCorrectly()
    {
        var pending = Video.Create(TweetUrl.Create("https://x.com/u/status/1"), VideoTitle.Create("Pending"), TimeProvider.System);
        var failing = Video.Create(TweetUrl.Create("https://x.com/u/status/2"), VideoTitle.Create("Failing"), TimeProvider.System);
        failing.StartDownloading(TimeProvider.System);
        failing.MarkFailed(TimeProvider.System);

        await _sut.AddAsync(pending);
        await _sut.AddAsync(failing);
        await _db.SaveChangesAsync();

        var result = await _sut.SearchAsync(new VideoSearchQuery(Status: Domain.Enums.VideoStatus.Pending));

        Assert.Single(result);
        Assert.Equal(pending.Id, result[0].Id);
    }

    [Fact]
    public async Task FindByTweetIdAsync_WhenExists_ReturnsVideo()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/user/status/987654321"),
            VideoTitle.Create("Find By TweetId"),
            TimeProvider.System);
        await _sut.AddAsync(video);
        await _db.SaveChangesAsync();

        var result = await _sut.FindByTweetIdAsync("987654321");

        Assert.NotNull(result);
        Assert.Equal(video.Id, result.Id);
    }

    [Fact]
    public async Task FindByTweetIdAsync_WhenNotExists_ReturnsNull()
    {
        var result = await _sut.FindByTweetIdAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByTweetIdAsync_WhenDifferentId_ReturnsNull()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/user/status/111111111"),
            VideoTitle.Create("Some Video"),
            TimeProvider.System);
        await _sut.AddAsync(video);
        await _db.SaveChangesAsync();

        var result = await _sut.FindByTweetIdAsync("222222222");

        Assert.Null(result);
    }

    public void Dispose() => _db.Dispose();
}
