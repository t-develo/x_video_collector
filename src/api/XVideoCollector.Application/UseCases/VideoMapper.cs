using XVideoCollector.Application.Dtos;
using XVideoCollector.Domain.Entities;

namespace XVideoCollector.Application.UseCases;

internal static class VideoMapper
{
    internal static VideoDto ToDto(Video video, IReadOnlyList<TagDto> tags) =>
        new(
            video.Id,
            video.TweetUrl.Value,
            video.TweetUrl.TweetId,
            video.TweetUrl.UserName,
            video.Title.Value,
            video.Status,
            video.BlobPath?.Value,
            video.ThumbnailBlobPath?.Value,
            video.DurationSeconds,
            video.FileSizeBytes,
            video.CategoryId,
            video.Notes,
            video.FailureReason,
            tags,
            video.CreatedAt,
            video.UpdatedAt);

    internal static VideoListItemDto ToListItemDto(Video video, IReadOnlyList<TagDto> tags) =>
        new(
            video.Id,
            video.TweetUrl.Value,
            video.Title.Value,
            video.Status,
            video.ThumbnailBlobPath?.Value,
            video.DurationSeconds,
            video.CategoryId,
            video.FailureReason,
            tags,
            video.CreatedAt);

    internal static TagDto ToDto(Tag tag) =>
        new(tag.Id, tag.Name, tag.Color, tag.CreatedAt, tag.UpdatedAt);

    internal static CategoryDto ToDto(Category category) =>
        new(category.Id, category.Name, category.SortOrder, category.CreatedAt, category.UpdatedAt);
}
