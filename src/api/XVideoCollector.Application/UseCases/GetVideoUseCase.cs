using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public sealed class GetVideoUseCase(
    IVideoRepository videoRepository,
    ITagRepository tagRepository) : IGetVideoUseCase
{
    public async Task<VideoDto?> ExecuteAsync(
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
