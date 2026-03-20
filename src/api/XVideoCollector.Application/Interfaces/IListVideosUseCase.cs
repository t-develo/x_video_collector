using XVideoCollector.Application.Dtos;

namespace XVideoCollector.Application.Interfaces;

public interface IListVideosUseCase
{
    Task<PaginatedResult<VideoListItemDto>> ExecuteAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}
