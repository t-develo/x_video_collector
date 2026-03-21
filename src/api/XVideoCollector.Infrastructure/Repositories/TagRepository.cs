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

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Tag>>> GetByVideoIdsAsync(
        IReadOnlyList<Guid> videoIds,
        CancellationToken cancellationToken = default)
    {
        if (videoIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<Tag>>();

        var rows = await (
            from vt in db.VideoTags
            join t in db.Tags on vt.TagId equals t.Id
            where videoIds.Contains(vt.VideoId)
            orderby t.Name
            select new { vt.VideoId, Tag = t }
        ).ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.VideoId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Tag>)g.Select(r => r.Tag).ToList());
    }

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        await db.Tags.AddAsync(tag, cancellationToken);
    }

    public Task UpdateAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        db.Tags.Update(tag);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tag = await db.Tags.FindAsync([id], cancellationToken);
        if (tag is not null)
            db.Tags.Remove(tag);
    }
}
