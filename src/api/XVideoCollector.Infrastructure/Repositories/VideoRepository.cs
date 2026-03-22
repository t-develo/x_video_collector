using Microsoft.EntityFrameworkCore;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Infrastructure.Persistence;

namespace XVideoCollector.Infrastructure.Repositories;

internal sealed class VideoRepository(AppDbContext db) : IVideoRepository
{
    public async Task<Video?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.Videos.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Video>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.Videos
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Video> Videos, int TotalCount)> GetPagedAsync(
        int skip,
        int take,
        VideoSortOrder sortOrder = VideoSortOrder.CreatedAtDesc,
        CancellationToken cancellationToken = default)
    {
        var q = ApplySortOrder(db.Videos.AsQueryable(), sortOrder);
        var totalCount = await q.CountAsync(cancellationToken);
        var videos = await q.Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (videos, totalCount);
    }

    public async Task<IReadOnlyList<Video>> SearchAsync(
        VideoSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var q = ApplyFilters(db.Videos.AsQueryable(), query);
        return await ApplySortOrder(q, query.SortOrder).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Video> Videos, int TotalCount)> SearchPagedAsync(
        VideoSearchQuery query,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var q = ApplySortOrder(ApplyFilters(db.Videos.AsQueryable(), query), query.SortOrder);
        var totalCount = await q.CountAsync(cancellationToken);
        var videos = await q.Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (videos, totalCount);
    }

    public async Task<Video?> FindByTweetIdAsync(string tweetId, CancellationToken cancellationToken = default)
        => await db.Videos
            .FirstOrDefaultAsync(v => v.TweetUrl.Value.Contains($"/status/{tweetId}"), cancellationToken);

    public async Task<VideoStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var counts = await db.Videos
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalFileSizeBytes = await db.Videos
            .Where(v => v.FileSizeBytes.HasValue)
            .SumAsync(v => v.FileSizeBytes ?? 0L, cancellationToken);

        int Get(VideoStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;

        return new VideoStats(
            TotalCount: counts.Sum(c => c.Count),
            PendingCount: Get(VideoStatus.Pending),
            DownloadingCount: Get(VideoStatus.Downloading),
            ProcessingCount: Get(VideoStatus.Processing),
            ReadyCount: Get(VideoStatus.Ready),
            FailedCount: Get(VideoStatus.Failed),
            TotalFileSizeBytes: totalFileSizeBytes);
    }

    public async Task AddAsync(Video video, CancellationToken cancellationToken = default)
    {
        await db.Videos.AddAsync(video, cancellationToken);
    }

    public Task UpdateAsync(Video video, CancellationToken cancellationToken = default)
    {
        db.Videos.Update(video);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var video = await db.Videos.FindAsync([id], cancellationToken);
        if (video is not null)
            db.Videos.Remove(video);
    }

    private IQueryable<Video> ApplyFilters(IQueryable<Video> q, VideoSearchQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var escaped = query.Keyword.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
            q = q.Where(v => EF.Functions.Like(v.Title.Value, $"%{escaped}%"));
        }

        if (query.Status.HasValue)
            q = q.Where(v => v.Status == query.Status.Value);

        if (query.CategoryId.HasValue)
            q = q.Where(v => v.CategoryId == query.CategoryId.Value);

        if (query.TagIds is { Count: > 0 })
        {
            var tagIds = query.TagIds;
            q = q.Where(v =>
                db.VideoTags
                    .Where(vt => vt.VideoId == v.Id)
                    .Any(vt => tagIds.Contains(vt.TagId)));
        }

        return q;
    }

    private static IOrderedQueryable<Video> ApplySortOrder(IQueryable<Video> q, VideoSortOrder sortOrder)
        => sortOrder switch
        {
            VideoSortOrder.CreatedAtAsc => q.OrderBy(v => v.CreatedAt),
            VideoSortOrder.TitleAsc => q.OrderBy(v => v.Title.Value).ThenByDescending(v => v.CreatedAt),
            VideoSortOrder.TitleDesc => q.OrderByDescending(v => v.Title.Value).ThenByDescending(v => v.CreatedAt),
            VideoSortOrder.DurationDesc => q.OrderByDescending(v => v.DurationSeconds).ThenByDescending(v => v.CreatedAt),
            VideoSortOrder.FileSizeDesc => q.OrderByDescending(v => v.FileSizeBytes).ThenByDescending(v => v.CreatedAt),
            _ => q.OrderByDescending(v => v.CreatedAt),
        };
}
