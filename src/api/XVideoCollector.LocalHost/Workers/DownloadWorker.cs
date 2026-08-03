using Microsoft.Extensions.Options;
using XVideoCollector.Application;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Infrastructure.Services;

namespace XVideoCollector.LocalHost.Workers;

/// <summary>
/// ダウンロード要求を逐次処理する常駐ワーカー。
///
/// Azure Consumption Plan では Queue Trigger が担っていた役割を、
/// 常駐ホストでは <see cref="BackgroundService"/> が担う。
/// プロセス内チャネルは再起動で失われるため、DB を定期走査して
/// 未処理 (Pending) の動画と、中断された (Downloading / Processing のまま滞留) 動画を拾い直す。
///
/// Raspberry Pi の CPU / メモリを保護するため、同時実行数は 1 に固定している。
/// </summary>
internal sealed class DownloadWorker(
    DownloadQueueChannel channel,
    IServiceScopeFactory scopeFactory,
    ILocalMediaAccessor mediaAccessor,
    TimeProvider timeProvider,
    IOptions<DownloadWorkerOptions> options,
    ILogger<DownloadWorker> logger) : BackgroundService
{
    private static readonly VideoStatus[] InterruptedStatuses =
        [VideoStatus.Downloading, VideoStatus.Processing];

    private readonly DownloadWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "ダウンロードワーカーを開始しました (走査間隔={SweepInterval}秒, 中断判定={StaleAfter}分)",
            _options.SweepIntervalSeconds,
            _options.StaleAfterMinutes);

        var sweeper = RunSweepLoopAsync(stoppingToken);
        var consumer = RunConsumeLoopAsync(stoppingToken);

        await Task.WhenAll(sweeper, consumer);
    }

    /// <summary>
    /// チャネルから動画 ID を 1 件ずつ取り出して処理する。
    /// </summary>
    private async Task RunConsumeLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var videoId in channel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(videoId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 停止要求による正常終了
        }
    }

    /// <summary>
    /// 起動直後と一定間隔で DB を走査し、取りこぼした動画をキューに戻す。
    /// </summary>
    private async Task RunSweepLoopAsync(CancellationToken stoppingToken)
    {
        // 起動直後は、実行中だったはずの動画がすべて中断された状態なので即座に走査する
        var isFirstSweep = true;

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(Math.Max(1, _options.SweepIntervalSeconds)), timeProvider);

        try
        {
            do
            {
                await SweepAsync(isFirstSweep, stoppingToken);
                isFirstSweep = false;
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 停止要求による正常終了
        }
    }

    internal async Task SweepAsync(bool isFirstSweep, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var videoRepository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var now = timeProvider.GetUtcNow();

            // 起動直後は滞留時間を問わず中断扱いにする（並行実行が無いため実行中の動画は存在しない）
            var staleBefore = isFirstSweep
                ? now
                : now.AddMinutes(-Math.Max(1, _options.StaleAfterMinutes));

            var interrupted = await videoRepository.GetByStatusesAsync(
                InterruptedStatuses, staleBefore, cancellationToken);

            // 中断された動画と元から未処理だった動画をまとめて再投入する。
            // 中断分は Pending に戻した直後で UpdatedAt が更新されるため、
            // 下の Pending 検索では拾えない。ここで明示的に集める。
            var requeue = new HashSet<Guid>();

            foreach (var video in interrupted)
            {
                logger.LogWarning(
                    "中断された動画を再キューします: VideoId={VideoId}, Status={Status}",
                    video.Id, video.Status);

                // ResetToPending は Failed からのみ許可されるため、一度失敗として記録してから戻す
                video.MarkFailed("処理が中断されたため再実行します。", timeProvider);
                video.ResetToPending(timeProvider);
                await videoRepository.UpdateAsync(video, cancellationToken);

                requeue.Add(video.Id);
            }

            if (interrupted.Count > 0)
                await unitOfWork.SaveChangesAsync(cancellationToken);

            var pending = await videoRepository.GetByStatusesAsync(
                [VideoStatus.Pending], now, cancellationToken);

            foreach (var video in pending)
                requeue.Add(video.Id);

            foreach (var videoId in requeue)
                channel.Writer.TryWrite(videoId);

            if (requeue.Count > 0)
                logger.LogInformation("未処理の動画を {Count} 件キューに投入しました", requeue.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "未処理動画の走査に失敗しました");
        }
    }

    private async Task ProcessAsync(Guid videoId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var videoRepository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();

        var video = await videoRepository.GetByIdAsync(videoId, cancellationToken);
        if (video is null)
        {
            logger.LogWarning("動画が見つかりません: VideoId={VideoId}", videoId);
            return;
        }

        // 同じ ID がキューに重複して入っていても二重ダウンロードしない
        // （消費は単一ループなので、この判定でレースは発生しない）
        if (video.Status != VideoStatus.Pending)
        {
            logger.LogDebug(
                "処理対象外のためスキップします: VideoId={VideoId}, Status={Status}", videoId, video.Status);
            return;
        }

        if (!mediaAccessor.HasSufficientFreeSpace())
        {
            var message =
                $"ディスクの空き容量が不足しています (空き {mediaAccessor.GetAvailableFreeSpaceBytes() / 1024 / 1024}MB / " +
                $"必要 {mediaAccessor.MinimumFreeSpaceBytes / 1024 / 1024}MB)。";

            logger.LogError("{Message} ダウンロードを中止します: VideoId={VideoId}", message, videoId);

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            video.MarkFailed(message, timeProvider);
            await videoRepository.UpdateAsync(video, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        logger.LogInformation("動画ダウンロード開始: VideoId={VideoId}", videoId);

        try
        {
            var downloadVideo = scope.ServiceProvider.GetRequiredService<IDownloadVideoUseCase>();
            await downloadVideo.ExecuteAsync(videoId, cancellationToken);

            logger.LogInformation("動画ダウンロード完了: VideoId={VideoId}", videoId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("停止要求によりダウンロードを中断しました: VideoId={VideoId}", videoId);
            throw;
        }
        catch (Exception ex)
        {
            // 失敗ステータスの記録は DownloadVideoUseCase 側で完了している
            logger.LogError(ex, "動画ダウンロードでエラーが発生しました: VideoId={VideoId}", videoId);
        }
    }
}
