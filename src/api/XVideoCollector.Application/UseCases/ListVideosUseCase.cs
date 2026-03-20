using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public sealed class ListVideosUseCase(
    IVideoRepository videoRepository,
    ITagRepository tagRepository) : IListVideosUseCase
{
    public async Task<PaginatedResult<VideoListItemDto>> ExecuteAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var skip = (page - 1) * pageSize;
        var (videos, totalCount) = await videoRepository.GetPagedAsync(skip, pageSize, cancellationToken);

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
