using XVideoCollector.Application.Dtos;

namespace XVideoCollector.Application.Interfaces;

public interface ISearchVideosUseCase
{
    Task<PaginatedResult<VideoListItemDto>> ExecuteAsync(SearchVideoRequest request, CancellationToken cancellationToken = default);
}
