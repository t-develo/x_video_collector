using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public sealed class SearchVideosUseCase(
    IVideoRepository videoRepository,
    ITagRepository tagRepository) : ISearchVideosUseCase
{
    public async Task<PaginatedResult<VideoListItemDto>> ExecuteAsync(
        SearchVideoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new VideoSearchQuery(
            Keyword: request.Keyword,
            Status: request.Status,
            TagIds: request.TagIds,
            CategoryId: request.CategoryId);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Min(request.PageSize < 1 ? 20 : request.PageSize, 100);
        var skip = (page - 1) * pageSize;

        var (videos, totalCount) = await videoRepository.SearchPagedAsync(query, skip, pageSize, cancellationToken);

        var videoIds = videos.Select(v => v.Id).ToList();
        var tagsByVideoId = await tagRepository.GetByVideoIdsAsync(videoIds, cancellationToken);

        var items = videos.Select(video =>
        {
            var tagDtos = tagsByVideoId.TryGetValue(video.Id, out var tags)
                ? tags.Select(VideoMapper.ToDto).ToList()
                : [];
            return VideoMapper.ToListItemDto(video, tagDtos);
        }).ToList();

        return new PaginatedResult<VideoListItemDto>(items, totalCount, page, pageSize);
    }
}
