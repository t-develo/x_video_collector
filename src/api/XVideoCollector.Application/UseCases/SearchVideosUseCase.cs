using XVideoCollector.Application.Dtos;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public sealed class SearchVideosUseCase(
    IVideoRepository videoRepository,
    ITagRepository tagRepository)
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

        var results = await videoRepository.SearchAsync(query, cancellationToken);
        var totalCount = results.Count;

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var paged = results
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var items = new List<VideoListItemDto>(paged.Count);
        foreach (var video in paged)
        {
            var tags = await tagRepository.GetByVideoIdAsync(video.Id, cancellationToken);
            var tagDtos = tags.Select(VideoMapper.ToDto).ToList();
            items.Add(VideoMapper.ToListItemDto(video, tagDtos));
        }

        return new PaginatedResult<VideoListItemDto>(items, totalCount, page, pageSize);
    }
}
