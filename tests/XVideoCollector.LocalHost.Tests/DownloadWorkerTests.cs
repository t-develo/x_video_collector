using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XVideoCollector.Application;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;
using XVideoCollector.Infrastructure.Services;
using XVideoCollector.LocalHost.Workers;

namespace XVideoCollector.LocalHost.Tests;

/// <summary>
/// 常駐ワーカーの復旧動作のテスト。
/// プロセス内キューは再起動で失われるため、DB 走査による拾い直しが復旧の要となる。
/// </summary>
public sealed class DownloadWorkerTests : IClassFixture<LocalHostFactory>
{
    private readonly LocalHostFactory _factory;

    public DownloadWorkerTests(LocalHostFactory factory) => _factory = factory;

    [Fact]
    public async Task SweepAsync_RequeuesPendingVideo()
    {
        var videoId = await AddVideoAsync(VideoStatus.Pending);

        var enqueued = await SweepAsync();

        Assert.Contains(videoId, enqueued);
    }

    [Fact]
    public async Task SweepAsync_OnStartup_ResetsInterruptedDownloadToPendingAndRequeues()
    {
        // サービス停止でダウンロード中のまま残ったケース
        var videoId = await AddVideoAsync(VideoStatus.Downloading);

        var enqueued = await SweepAsync();

        Assert.Contains(videoId, enqueued);
        Assert.Equal(VideoStatus.Pending, await GetStatusAsync(videoId));
    }

    [Fact]
    public async Task SweepAsync_OnStartup_ResetsInterruptedProcessingToPending()
    {
        var videoId = await AddVideoAsync(VideoStatus.Processing);

        await SweepAsync();

        Assert.Equal(VideoStatus.Pending, await GetStatusAsync(videoId));
    }

    [Fact]
    public async Task SweepAsync_LeavesReadyVideoUntouched()
    {
        var videoId = await AddVideoAsync(VideoStatus.Ready);

        var enqueued = await SweepAsync();

        Assert.DoesNotContain(videoId, enqueued);
        Assert.Equal(VideoStatus.Ready, await GetStatusAsync(videoId));
    }

    [Fact]
    public async Task SweepAsync_LeavesFailedVideoUntouched()
    {
        // 失敗した動画はユーザーが明示的に再試行するまで放置する
        var videoId = await AddVideoAsync(VideoStatus.Failed);

        var enqueued = await SweepAsync();

        Assert.DoesNotContain(videoId, enqueued);
        Assert.Equal(VideoStatus.Failed, await GetStatusAsync(videoId));
    }

    [Fact]
    public async Task SweepAsync_WhenNotStartup_KeepsRecentDownloadingVideoRunning()
    {
        // 起動直後以外は、滞留時間が閾値を超えていない実行中の動画に触れてはいけない
        var videoId = await AddVideoAsync(VideoStatus.Downloading);

        await SweepAsync(isFirstSweep: false);

        Assert.Equal(VideoStatus.Downloading, await GetStatusAsync(videoId));
    }

    /// <summary>
    /// ワーカーの走査だけを実行し、キューに積まれた動画 ID を取り出す。
    /// 消費ループは動かさないため、ダウンロード処理には進まない。
    /// </summary>
    private async Task<IReadOnlyList<Guid>> SweepAsync(bool isFirstSweep = true)
    {
        var channel = _factory.Services.GetRequiredService<DownloadQueueChannel>();
        DrainChannel(channel);

        await CreateWorker(channel).SweepAsync(isFirstSweep, CancellationToken.None);

        return DrainChannel(channel);
    }

    private DownloadWorker CreateWorker(DownloadQueueChannel channel)
        => new(
            channel,
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            _factory.Services.GetRequiredService<ILocalMediaAccessor>(),
            _factory.Services.GetRequiredService<TimeProvider>(),
            Options.Create(new DownloadWorkerOptions()),
            _factory.Services.GetRequiredService<ILogger<DownloadWorker>>());

    private static List<Guid> DrainChannel(DownloadQueueChannel channel)
    {
        var drained = new List<Guid>();
        while (channel.Reader.TryRead(out var id))
            drained.Add(id);

        return drained;
    }

    private async Task<Guid> AddVideoAsync(VideoStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var video = Video.Create(
            TweetUrl.Create($"https://x.com/worker/status/{Random.Shared.NextInt64(1_000_000, 9_999_999_999)}"),
            VideoTitle.Create($"ワーカーテスト-{status}"),
            timeProvider);

        MoveToStatus(video, status, timeProvider);

        await repository.AddAsync(video);
        await unitOfWork.SaveChangesAsync();

        return video.Id;
    }

    private static void MoveToStatus(Video video, VideoStatus status, TimeProvider timeProvider)
    {
        switch (status)
        {
            case VideoStatus.Pending:
                break;
            case VideoStatus.Downloading:
                video.StartDownloading(timeProvider);
                break;
            case VideoStatus.Processing:
                video.StartDownloading(timeProvider);
                video.StartProcessing(timeProvider);
                break;
            case VideoStatus.Ready:
                video.StartDownloading(timeProvider);
                video.StartProcessing(timeProvider);
                video.MarkReady(BlobPath.Create("videos/videos/ready.mp4"), null, 10, 1024, timeProvider);
                break;
            case VideoStatus.Failed:
                video.MarkFailed("テスト用の失敗", timeProvider);
                break;
        }
    }

    private async Task<VideoStatus> GetStatusAsync(Guid videoId)
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();

        var video = await repository.GetByIdAsync(videoId);

        return video!.Status;
    }
}
