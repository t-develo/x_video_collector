using XVideoCollector.Application.Dtos;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public class GetVideoUseCase(
    IVideoRepository videoRepository,
    ITagRepository tagRepository)
{
    public virtual async Task<VideoDto?> ExecuteAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        var video = await videoRepository.GetByIdAsync(videoId, cancellationToken);
        if (video is null)
            return null;

        var tags = await tagRepository.GetByVideoIdAsync(videoId, cancellationToken);
        var tagDtos = tags.Select(VideoMapper.ToDto).ToList();

        return VideoMapper.ToDto(video, tagDtos);
    }
}
