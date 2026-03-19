using XVideoCollector.Application.Services;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.UseCases;

public sealed class DownloadVideoUseCase(
    IVideoRepository videoRepository,
    IVideoDownloadService downloadService,
    IBlobStorageService blobStorageService,
    IThumbnailService thumbnailService)
{
    public async Task ExecuteAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        var video = await videoRepository.GetByIdAsync(videoId, cancellationToken)
            ?? throw new InvalidOperationException($"Video '{videoId}' not found.");

        video.StartDownloading();
        await videoRepository.UpdateAsync(video, cancellationToken);

        try
        {
            var result = await downloadService.DownloadAsync(
                video.TweetUrl.Value, cancellationToken);

            video.StartProcessing();
            await videoRepository.UpdateAsync(video, cancellationToken);

            using var videoStream = File.OpenRead(result.FilePath);
            var blobName = $"videos/{video.Id}.mp4";
            var blobPath = await blobStorageService.UploadVideoAsync(
                videoStream, blobName, cancellationToken);

            string? thumbnailBlobPath = null;
            var thumbnailStream = await thumbnailService.GenerateFromVideoAsync(
                result.FilePath, cancellationToken);
            if (thumbnailStream is not null)
            {
                await using (thumbnailStream)
                {
                    var thumbBlobName = $"thumbnails/{video.Id}.jpg";
                    thumbnailBlobPath = await blobStorageService.UploadThumbnailAsync(
                        thumbnailStream, thumbBlobName, cancellationToken);
                }
            }

            video.MarkReady(
                BlobPath.Create(blobPath),
                thumbnailBlobPath is not null ? BlobPath.Create(thumbnailBlobPath) : null,
                result.DurationSeconds,
                result.FileSizeBytes);

            await videoRepository.UpdateAsync(video, cancellationToken);
        }
        catch
        {
            video.MarkFailed();
            await videoRepository.UpdateAsync(video, cancellationToken);
            throw;
        }
    }
}
