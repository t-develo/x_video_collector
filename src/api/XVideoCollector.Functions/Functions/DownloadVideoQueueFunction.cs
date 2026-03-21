using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using XVideoCollector.Application.Interfaces;

namespace XVideoCollector.Functions.Functions;

public sealed class DownloadVideoQueueFunction(
    IDownloadVideoUseCase downloadVideo,
    ILogger<DownloadVideoQueueFunction> logger)
{
    [Function("DownloadVideoQueue")]
    public async Task RunAsync(
        [QueueTrigger("video-download-requests", Connection = "QueueStorage:ConnectionString")] string videoIdMessage,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(videoIdMessage, out var videoId))
        {
            logger.LogError("キューメッセージの Video ID が不正です: {Message}", videoIdMessage);
            return;
        }

        logger.LogInformation("動画ダウンロード開始: VideoId={VideoId}", videoId);

        try
        {
            await downloadVideo.ExecuteAsync(videoId, cancellationToken);
            logger.LogInformation("動画ダウンロード完了: VideoId={VideoId}", videoId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "動画ダウンロードでエラーが発生しました: VideoId={VideoId}", videoId);
            throw;
        }
    }
}
