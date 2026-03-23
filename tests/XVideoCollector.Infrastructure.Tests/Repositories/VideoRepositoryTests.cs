using Microsoft.EntityFrameworkCore;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
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
        failing.MarkFailed(null, TimeProvider.System);

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

    [Fact]
    public async Task GetPagedAsync_WithTitleAscSort_ReturnsTitleOrdered()
    {
        var v1 = Video.Create(TweetUrl.Create("https://x.com/u/status/1"), VideoTitle.Create("Zebra"), TimeProvider.System);
        var v2 = Video.Create(TweetUrl.Create("https://x.com/u/status/2"), VideoTitle.Create("Apple"), TimeProvider.System);
        var v3 = Video.Create(TweetUrl.Create("https://x.com/u/status/3"), VideoTitle.Create("Mango"), TimeProvider.System);

        await _sut.AddAsync(v1);
        await _sut.AddAsync(v2);
        await _sut.AddAsync(v3);
        await _db.SaveChangesAsync();

        var (videos, total) = await _sut.GetPagedAsync(0, 10, VideoSortOrder.TitleAsc);

        Assert.Equal(3, total);
        Assert.Equal("Apple", videos[0].Title.Value);
        Assert.Equal("Mango", videos[1].Title.Value);
        Assert.Equal("Zebra", videos[2].Title.Value);
    }

    [Fact]
    public async Task GetPagedAsync_WithCreatedAtAscSort_ReturnsOldestFirst()
    {
        var tp = new FakeTimeProvider();

        tp.SetTime(DateTimeOffset.UtcNow.AddDays(-2));
        var old = Video.Create(TweetUrl.Create("https://x.com/u/status/10"), VideoTitle.Create("Old"), tp);

        tp.SetTime(DateTimeOffset.UtcNow);
        var newest = Video.Create(TweetUrl.Create("https://x.com/u/status/11"), VideoTitle.Create("New"), tp);

        await _sut.AddAsync(old);
        await _sut.AddAsync(newest);
        await _db.SaveChangesAsync();

        var (videos, _) = await _sut.GetPagedAsync(0, 10, VideoSortOrder.CreatedAtAsc);

        Assert.Equal(old.Id, videos[0].Id);
        Assert.Equal(newest.Id, videos[1].Id);
    }

    [Fact]
    public async Task GetStatsAsync_WithMixedStatuses_ReturnsCorrectCounts()
    {
        var tp = TimeProvider.System;
        var v1 = Video.Create(TweetUrl.Create("https://x.com/u/status/21"), VideoTitle.Create("V1"), tp);
        var v2 = Video.Create(TweetUrl.Create("https://x.com/u/status/22"), VideoTitle.Create("V2"), tp);
        v2.StartDownloading(tp);
        v2.MarkFailed(null, tp);

        await _sut.AddAsync(v1);
        await _sut.AddAsync(v2);
        await _db.SaveChangesAsync();

        var stats = await _sut.GetStatsAsync();

        Assert.Equal(2, stats.TotalCount);
        Assert.Equal(1, stats.PendingCount);
        Assert.Equal(1, stats.FailedCount);
        Assert.Equal(0, stats.ReadyCount);
    }

    [Fact]
    public async Task GetStatsAsync_WhenNoVideos_ReturnsZeroCounts()
    {
        var stats = await _sut.GetStatsAsync();

        Assert.Equal(0, stats.TotalCount);
        Assert.Equal(0, stats.TotalFileSizeBytes);
    }

    public void Dispose() => _db.Dispose();
}

/// <summary>
/// テスト用の時刻プロバイダー（CreatedAt の制御用）
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public void SetTime(DateTimeOffset time) => _utcNow = time;

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
