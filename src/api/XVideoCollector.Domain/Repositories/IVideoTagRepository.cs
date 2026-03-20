using XVideoCollector.Domain.Entities;

namespace XVideoCollector.Domain.Repositories;

public interface IVideoTagRepository
{
    Task<IReadOnlyList<VideoTag>> GetByVideoIdAsync(Guid videoId, CancellationToken cancellationToken = default);
    Task AddAsync(VideoTag videoTag, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid videoId, Guid tagId, CancellationToken cancellationToken = default);
    Task DeleteByVideoIdAsync(Guid videoId, CancellationToken cancellationToken = default);
    /// <summary>既存のタグを全削除して新しいタグを一括追加し、1回の SaveChanges で確定する</summary>
    Task SyncByVideoIdAsync(Guid videoId, IReadOnlyList<Guid> tagIds, CancellationToken cancellationToken = default);
}
