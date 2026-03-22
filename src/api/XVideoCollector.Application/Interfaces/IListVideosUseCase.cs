using XVideoCollector.Application.Dtos;
using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Application.Interfaces;

public interface IListVideosUseCase
{
    Task<PaginatedResult<VideoListItemDto>> ExecuteAsync(
        int page = 1,
        int pageSize = 20,
        VideoSortOrder sortOrder = VideoSortOrder.CreatedAtDesc,
        CancellationToken cancellationToken = default);
}
