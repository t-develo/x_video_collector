using XVideoCollector.Application.Services;

namespace XVideoCollector.Infrastructure.Services;

/// <summary>
/// プロセス内チャネルにダウンロード要求を積む <see cref="IDownloadQueueService"/> 実装。
/// スタンドアロン運用（Raspberry Pi 等）で Azure Storage Queue の代わりに使用する。
/// </summary>
public sealed class InProcessDownloadQueueService(DownloadQueueChannel channel) : IDownloadQueueService
{
    public Task EnqueueAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        channel.Writer.TryWrite(videoId);
        return Task.CompletedTask;
    }
}
