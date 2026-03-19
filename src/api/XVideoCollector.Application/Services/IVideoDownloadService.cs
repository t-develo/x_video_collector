namespace XVideoCollector.Application.Services;

public sealed record VideoDownloadResult(
    string FilePath,
    int DurationSeconds,
    long FileSizeBytes);

public interface IVideoDownloadService
{
    Task<VideoDownloadResult> DownloadAsync(string tweetUrl, CancellationToken cancellationToken = default);
}
