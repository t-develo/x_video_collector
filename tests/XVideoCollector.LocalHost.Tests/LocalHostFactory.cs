using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using XVideoCollector.Application.Services;

namespace XVideoCollector.LocalHost.Tests;

/// <summary>
/// ローカルホストをスタンドアロン構成（SQLite + ローカルファイル + プロセス内キュー）で
/// 起動するテスト用ファクトリ。
/// 実際のダウンロードは行わないよう <see cref="IVideoDownloadService"/> を差し替える。
/// </summary>
public sealed class LocalHostFactory : WebApplicationFactory<Program>
{
    public string RootDirectory { get; } =
        Path.Combine(Path.GetTempPath(), $"xvc_host_{Guid.NewGuid():N}");

    public string MediaPath => Path.Combine(RootDirectory, "media");

    public string DatabasePath => Path.Combine(RootDirectory, "xvc.db");

    public LocalHostFactory()
    {
        Directory.CreateDirectory(MediaPath);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SKIP_AUTH"] = "true",
                ["Storage:Provider"] = "Local",
                ["Queue:Provider"] = "InProcess",
                ["ConnectionStrings:SqlDb"] = $"Data Source={DatabasePath}",
                ["LocalStorage:RootPath"] = MediaPath,
                ["LocalStorage:SigningKey"] = "integration-test-signing-key",
                ["LocalStorage:MinimumFreeDiskMB"] = "1",
                // 走査ループがテスト中に動かないよう十分長くする
                ["DownloadWorker:SweepIntervalSeconds"] = "3600",
                ["YtDlp:ExecutablePath"] = "/bin/true",
                ["YtDlp:FfmpegPath"] = "/bin/true",
                ["YtDlp:FfprobePath"] = "/bin/true",
            });
        });

        builder.ConfigureServices(services =>
        {
            // 実際に yt-dlp を起動しないよう差し替える
            services.RemoveAll<IVideoDownloadService>();
            services.AddScoped<IVideoDownloadService, NoOpVideoDownloadService>();

            // 常駐ワーカーがテストと並行してキューを消費しないよう停止する。
            // ワーカーの走査ロジックは DownloadWorkerTests で直接呼び出して検証する。
            var worker = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType?.Name == nameof(Workers.DownloadWorker));

            if (worker is not null)
                services.Remove(worker);
        });

        return base.CreateHost(builder);
    }

    /// <summary>
    /// 署名付き URL の検証やメディア配信を試すため、メディアファイルを直接配置する。
    /// </summary>
    public string WriteMediaFile(string blobPath, byte[] content)
    {
        var physicalPath = Path.Combine([RootDirectory, "media", .. blobPath.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        File.WriteAllBytes(physicalPath, content);

        return physicalPath;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(RootDirectory))
        {
            try
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
            catch (IOException)
            {
                // SQLite のファイルハンドルが残っている場合は無視する
            }
        }
    }
}

internal sealed class NoOpVideoDownloadService : IVideoDownloadService
{
    public Task<VideoDownloadResult> DownloadAsync(string tweetUrl, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("ダウンロードはテストでは実行されません。");
}
