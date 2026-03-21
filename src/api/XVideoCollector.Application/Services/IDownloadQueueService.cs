namespace XVideoCollector.Application.Services;

/// <summary>
/// 動画ダウンロードリクエストをキューに送信するサービス
/// </summary>
public interface IDownloadQueueService
{
    /// <summary>
    /// 指定した Video ID のダウンロードリクエストをキューに追加する
    /// </summary>
    Task EnqueueAsync(Guid videoId, CancellationToken cancellationToken = default);
}
