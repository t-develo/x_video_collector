using XVideoCollector.Application.Interfaces;
using XVideoCollector.Application.Services;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.UseCases;

public sealed class DownloadVideoUseCase(
    IVideoRepository videoRepository,
    IVideoDownloadService downloadService,
    IBlobStorageService blobStorageService,
    IThumbnailService thumbnailService,
    IUnitOfWork unitOfWork,
    ITelemetryService telemetryService,
    TimeProvider timeProvider) : IDownloadVideoUseCase
{
    private static string ResolveVideoContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            "mp4" => "video/mp4",
            "webm" => "video/webm",
            "mov" => "video/quicktime",
            "mkv" => "video/x-matroska",
            _ => "video/mp4",
        };

    public async Task ExecuteAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        var video = await videoRepository.GetByIdAsync(videoId, cancellationToken)
            ?? throw new InvalidOperationException($"Video '{videoId}' not found.");

        video.StartDownloading(timeProvider);
        await videoRepository.UpdateAsync(video, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        string? tempDir = null;
        var downloadStarted = timeProvider.GetUtcNow();
        try
        {
            var result = await downloadService.DownloadAsync(video.TweetUrl.Value, cancellationToken);
            tempDir = Path.GetDirectoryName(result.FilePath);

            video.StartProcessing(timeProvider);
            await videoRepository.UpdateAsync(video, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var blobPath = await UploadVideoToBlobAsync(result.FilePath, video.Id, cancellationToken);
            var thumbnailBlobPath = await UploadThumbnailToBlobAsync(result.FilePath, video.Id, cancellationToken);

            video.MarkReady(
                BlobPath.Create(blobPath),
                thumbnailBlobPath is not null ? BlobPath.Create(thumbnailBlobPath) : null,
                result.DurationSeconds,
                result.FileSizeBytes,
                timeProvider);

            await videoRepository.UpdateAsync(video, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            telemetryService.TrackDownloadSuccess(videoId, timeProvider.GetUtcNow() - downloadStarted, result.FileSizeBytes);
        }
        catch (Exception ex)
        {
            video.MarkFailed(ex.Message, timeProvider);
            await videoRepository.UpdateAsync(video, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            telemetryService.TrackDownloadFailure(videoId, ex.Message, timeProvider.GetUtcNow() - downloadStarted);
            throw;
        }
        finally
        {
            if (tempDir is not null && Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private async Task<string> UploadVideoToBlobAsync(string filePath, Guid videoId, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        var ext = Path.GetExtension(filePath).TrimStart('.');
        var blobName = $"videos/{videoId}.{(string.IsNullOrEmpty(ext) ? "mp4" : ext)}";
        return await blobStorageService.UploadVideoAsync(stream, blobName, ResolveVideoContentType(ext), cancellationToken);
    }

    private async Task<string?> UploadThumbnailToBlobAsync(string filePath, Guid videoId, CancellationToken cancellationToken)
    {
        var stream = await thumbnailService.GenerateFromVideoAsync(filePath, cancellationToken);
        if (stream is null) return null;

        await using (stream)
        {
            return await blobStorageService.UploadThumbnailAsync(stream, $"thumbnails/{videoId}.jpg", cancellationToken);
        }
    }
}
