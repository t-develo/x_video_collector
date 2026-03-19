using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Application.Dtos;

public sealed record VideoDto(
    Guid Id,
    string TweetUrl,
    string TweetId,
    string UserName,
    string Title,
    VideoStatus Status,
    string? BlobPath,
    string? ThumbnailBlobPath,
    int? DurationSeconds,
    long? FileSizeBytes,
    Guid? CategoryId,
    IReadOnlyList<TagDto> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
