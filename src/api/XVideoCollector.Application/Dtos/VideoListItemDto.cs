using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Application.Dtos;

public sealed record VideoListItemDto(
    Guid Id,
    string TweetUrl,
    string Title,
    VideoStatus Status,
    string? ThumbnailBlobPath,
    int? DurationSeconds,
    Guid? CategoryId,
    string? FailureReason,
    IReadOnlyList<TagDto> Tags,
    DateTimeOffset CreatedAt);
