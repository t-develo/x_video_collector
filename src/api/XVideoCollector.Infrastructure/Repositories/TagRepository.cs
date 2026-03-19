using Microsoft.EntityFrameworkCore;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Infrastructure.Persistence;

namespace XVideoCollector.Infrastructure.Repositories;

internal sealed class TagRepository(AppDbContext db) : ITagRepository
{
    public async Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.Tags.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.Tags.OrderBy(t => t.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Tag>> GetByVideoIdAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
        => await db.Tags
            .Where(t => db.VideoTags.Any(vt => vt.VideoId == videoId && vt.TagId == t.Id))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        await db.Tags.AddAsync(tag, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        db.Tags.Update(tag);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tag = await db.Tags.FindAsync([id], cancellationToken);
        if (tag is not null)
        {
            db.Tags.Remove(tag);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
