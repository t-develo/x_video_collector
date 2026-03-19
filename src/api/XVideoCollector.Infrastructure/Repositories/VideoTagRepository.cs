using Microsoft.EntityFrameworkCore;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Infrastructure.Persistence;

namespace XVideoCollector.Infrastructure.Repositories;

internal sealed class VideoTagRepository(AppDbContext db) : IVideoTagRepository
{
    public async Task<IReadOnlyList<VideoTag>> GetByVideoIdAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
        => await db.VideoTags
            .Where(vt => vt.VideoId == videoId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(VideoTag videoTag, CancellationToken cancellationToken = default)
    {
        await db.VideoTags.AddAsync(videoTag, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid videoId, Guid tagId, CancellationToken cancellationToken = default)
    {
        var entity = await db.VideoTags.FindAsync([videoId, tagId], cancellationToken);
        if (entity is not null)
        {
            db.VideoTags.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteByVideoIdAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var entities = await db.VideoTags
            .Where(vt => vt.VideoId == videoId)
            .ToListAsync(cancellationToken);

        if (entities.Count > 0)
        {
            db.VideoTags.RemoveRange(entities);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
