using XVideoCollector.Application.Interfaces;
using XVideoCollector.Application.Services;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public sealed class DeleteVideoUseCase(
    IVideoRepository videoRepository,
    IVideoTagRepository videoTagRepository,
    IBlobStorageService blobStorageService) : IDeleteVideoUseCase
{
    public async Task ExecuteAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        var video = await videoRepository.GetByIdAsync(videoId, cancellationToken)
            ?? throw new InvalidOperationException($"Video '{videoId}' not found.");

        if (video.BlobPath is not null)
            await blobStorageService.DeleteAsync(video.BlobPath.Value, cancellationToken);

        if (video.ThumbnailBlobPath is not null)
            await blobStorageService.DeleteAsync(video.ThumbnailBlobPath.Value, cancellationToken);

        await videoTagRepository.DeleteByVideoIdAsync(videoId, cancellationToken);
        await videoRepository.DeleteAsync(videoId, cancellationToken);
    }
}
