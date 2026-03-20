using XVideoCollector.Domain.Entities;

namespace XVideoCollector.Domain.Repositories;

public interface IVideoRepository
{
    Task<Video?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Video>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Video> Videos, int TotalCount)> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Video>> SearchAsync(VideoSearchQuery query, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Video> Videos, int TotalCount)> SearchPagedAsync(VideoSearchQuery query, int skip, int take, CancellationToken cancellationToken = default);
    Task AddAsync(Video video, CancellationToken cancellationToken = default);
    Task UpdateAsync(Video video, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
