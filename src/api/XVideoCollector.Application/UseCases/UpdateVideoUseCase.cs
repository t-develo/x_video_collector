using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.UseCases;

public sealed class UpdateVideoUseCase(
    IVideoRepository videoRepository,
    ITagRepository tagRepository,
    IVideoTagRepository videoTagRepository,
    TimeProvider timeProvider) : IUpdateVideoUseCase
{
    public async Task<VideoDto> ExecuteAsync(
        UpdateVideoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var video = await videoRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Video '{request.Id}' not found.");

        var title = VideoTitle.Create(request.Title);
        video.UpdateTitle(title, timeProvider);
        video.SetCategory(request.CategoryId, timeProvider);

        // タグの同期（削除＋追加）を1トランザクションで実行
        await videoTagRepository.SyncByVideoIdAsync(video.Id, request.TagIds, cancellationToken);
        await videoRepository.UpdateAsync(video, cancellationToken);

        var tags = await tagRepository.GetByVideoIdAsync(video.Id, cancellationToken);
        var tagDtos = tags.Select(VideoMapper.ToDto).ToList();

        return VideoMapper.ToDto(video, tagDtos);
    }
}
