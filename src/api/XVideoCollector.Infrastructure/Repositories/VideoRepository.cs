using Microsoft.EntityFrameworkCore;
using XVideoCollector.Domain.Entities;
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
        CancellationToken cancellationToken = default)
    {
        var q = db.Videos.OrderByDescending(v => v.CreatedAt);
        var totalCount = await q.CountAsync(cancellationToken);
        var videos = await q.Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (videos, totalCount);
    }

    public async Task<IReadOnlyList<Video>> SearchAsync(
        VideoSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var q = db.Videos.AsQueryable();

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

        return await q.OrderByDescending(v => v.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Video> Videos, int TotalCount)> SearchPagedAsync(
        VideoSearchQuery query,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var q = db.Videos.AsQueryable();

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

        q = q.OrderByDescending(v => v.CreatedAt);
        var totalCount = await q.CountAsync(cancellationToken);
        var videos = await q.Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (videos, totalCount);
    }

    public async Task AddAsync(Video video, CancellationToken cancellationToken = default)
    {
        await db.Videos.AddAsync(video, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Video video, CancellationToken cancellationToken = default)
    {
        db.Videos.Update(video);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var video = await db.Videos.FindAsync([id], cancellationToken);
        if (video is not null)
        {
            db.Videos.Remove(video);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
