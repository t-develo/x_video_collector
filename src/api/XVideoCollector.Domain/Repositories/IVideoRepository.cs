using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Domain.Repositories;

public sealed record VideoSearchQuery(
    string? Keyword = null,
    VideoStatus? Status = null,
    IReadOnlyList<Guid>? TagIds = null,
    Guid? CategoryId = null);

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
