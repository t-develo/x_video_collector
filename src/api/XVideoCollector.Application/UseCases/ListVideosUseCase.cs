using XVideoCollector.Application.Dtos;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public class ListVideosUseCase(
    IVideoRepository videoRepository,
    ITagRepository tagRepository)
{
    public virtual async Task<PaginatedResult<VideoListItemDto>> ExecuteAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var allVideos = await videoRepository.GetAllAsync(cancellationToken);
        var totalCount = allVideos.Count;

        var paged = allVideos
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
