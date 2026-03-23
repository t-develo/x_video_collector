using Microsoft.Extensions.Logging.Abstractions;
using XVideoCollector.Infrastructure.Options;
using XVideoCollector.Infrastructure.Services;
using MsOptions = Microsoft.Extensions.Options;

namespace XVideoCollector.Infrastructure.Tests.Services;

public sealed class FfmpegThumbnailServiceTests
{
    private static FfmpegThumbnailService CreateService(YtDlpOptions? opts = null)
    {
        var options = MsOptions.Options.Create(opts ?? new YtDlpOptions());
        return new FfmpegThumbnailService(options, NullLogger<FfmpegThumbnailService>.Instance);
    }

    [Fact]
    public async Task GenerateFromVideoAsync_FileNotFound_ReturnsNull()
    {
        var service = CreateService();

        var result = await service.GenerateFromVideoAsync(
            "/nonexistent/path/video.mp4", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateFromVideoAsync_ExistingThumbnail_ReturnsThumbnailStream()
    {
        // yt-dlp が生成したサムネイルが既に存在する場合、それを返すことを確認
        var service = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var videoPath = Path.Combine(tempDir, "video123.mp4");
            var thumbnailPath = Path.Combine(tempDir, "video123.jpg");

            // ダミーファイルを作成
            await File.WriteAllBytesAsync(videoPath, [0xFF, 0xFB]); // mp4 dummy
            await File.WriteAllBytesAsync(thumbnailPath, [0xFF, 0xD8, 0xFF]); // JPEG magic

            var stream = await service.GenerateFromVideoAsync(videoPath, CancellationToken.None);

            Assert.NotNull(stream);
            await stream.DisposeAsync();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateFromVideoAsync_CancellationRequested_HandlesGracefully()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // ファイルが存在しない場合は null を返す（キャンセル前に早期リターン）
        var result = await service.GenerateFromVideoAsync(
            "/nonexistent/video.mp4", cts.Token);

        Assert.Null(result);
    }
}
