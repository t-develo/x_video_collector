using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Domain.Repositories;

public sealed record VideoSearchQuery(
    string? Keyword = null,
    VideoStatus? Status = null,
    IReadOnlyList<Guid>? TagIds = null,
    Guid? CategoryId = null,
    VideoSortOrder SortOrder = VideoSortOrder.CreatedAtDesc);
