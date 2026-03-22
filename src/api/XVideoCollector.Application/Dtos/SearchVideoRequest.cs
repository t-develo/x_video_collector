using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Application.Dtos;

public sealed record SearchVideoRequest(
    string? Keyword = null,
    VideoStatus? Status = null,
    IReadOnlyList<Guid>? TagIds = null,
    Guid? CategoryId = null,
    int Page = 1,
    int PageSize = 20,
    VideoSortOrder SortOrder = VideoSortOrder.CreatedAtDesc);
