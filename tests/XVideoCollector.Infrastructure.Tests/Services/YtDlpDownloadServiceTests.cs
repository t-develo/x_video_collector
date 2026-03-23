using Microsoft.Extensions.Logging.Abstractions;
using XVideoCollector.Infrastructure.Options;
using XVideoCollector.Infrastructure.Services;
using MsOptions = Microsoft.Extensions.Options;

namespace XVideoCollector.Infrastructure.Tests.Services;

public sealed class YtDlpDownloadServiceTests
{
    private static YtDlpDownloadService CreateService(YtDlpOptions? opts = null)
    {
        var options = MsOptions.Options.Create(opts ?? new YtDlpOptions());
        return new YtDlpDownloadService(options, NullLogger<YtDlpDownloadService>.Instance);
    }

    // ── URL バリデーション（正常系）──────────────────────────────────

    [Theory]
    [InlineData("https://x.com/user/status/123456789")]
    [InlineData("https://twitter.com/user/status/123456789")]
    [InlineData("https://mobile.twitter.com/user/status/123456789")]
    [InlineData("https://x.com/user/status/123456789?s=20")]
    public void ValidateUrl_ValidUrls_DoesNotThrow(string url)
    {
        var exception = Record.Exception(() => YtDlpDownloadService.ValidateUrl(url));
        Assert.Null(exception);
    }

    // ── URL バリデーション（異常系）──────────────────────────────────

    [Theory]
    [InlineData("https://evil.com/malware")]
    [InlineData("https://notx.com/user/status/123")]
    [InlineData("https://x.com.evil.com/user/status/123")]
    public void ValidateUrl_DisallowedHost_ThrowsArgumentException(string url)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            YtDlpDownloadService.ValidateUrl(url));
        Assert.Contains("not allowed", ex.Message);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("")]
    [InlineData("ftp://x.com/user/status/123")]
    public void ValidateUrl_InvalidUrl_ThrowsArgumentException(string url)
    {
        Assert.Throws<ArgumentException>(() =>
            YtDlpDownloadService.ValidateUrl(url));
    }

    [Theory]
    [InlineData("https://x.com/user/status/123; rm -rf /")]
    [InlineData("https://x.com/user/status/123$(whoami)")]
    [InlineData("https://x.com/user/status/123`id`")]
    [InlineData("https://x.com/user/status/123|cat /etc/passwd")]
    [InlineData("https://x.com/user/status/123&echo hacked")]
    [InlineData("https://x.com/user/status/123'OR 1=1--")]
    public void ValidateUrl_CommandInjectionAttempt_ThrowsArgumentException(string url)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            YtDlpDownloadService.ValidateUrl(url));
        Assert.Contains("disallowed characters", ex.Message);
    }

    // ── DownloadAsync — プロセス実行のタイムアウトテスト ────────────

    [Fact]
    public async Task DownloadAsync_InvalidUrl_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DownloadAsync("https://evil.com/video", CancellationToken.None));
    }

    [Fact]
    public void YtDlpOptions_TimeoutSeconds_CanBeConfigured()
    {
        var opts = new YtDlpOptions { TimeoutSeconds = 1 };

        Assert.Equal(1, opts.TimeoutSeconds);
    }

    [Fact]
    public async Task DownloadAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.DownloadAsync("https://x.com/user/status/123", cts.Token));
    }

    // ── オプション設定テスト ─────────────────────────────────────────

    [Fact]
    public void YtDlpOptions_Defaults_AreCorrect()
    {
        var opts = new YtDlpOptions();

        Assert.Equal("yt-dlp", opts.ExecutablePath);
        Assert.Equal("ffmpeg", opts.FfmpegPath);
        Assert.Equal(300, opts.TimeoutSeconds);
        Assert.Equal(500, opts.MaxFileSizeMB);
    }

    [Fact]
    public void YtDlpOptions_SectionName_IsYtDlp()
    {
        Assert.Equal("YtDlp", YtDlpOptions.SectionName);
    }

    [Fact]
    public void YtDlpOptions_FfprobePath_DefaultIsFfprobe()
    {
        var opts = new YtDlpOptions();

        Assert.Equal("ffprobe", opts.FfprobePath);
    }

    // ── GetDurationSecondsAsync ──────────────────────────────────────

    [Fact]
    public async Task GetDurationSecondsAsync_InvalidExecutable_ReturnsZero()
    {
        // ffprobe のパスに無効な実行ファイルを指定した場合は 0 を返す（例外を握りつぶす）
        var service = CreateService(new YtDlpOptions { FfprobePath = "/nonexistent/ffprobe" });

        var result = await service.GetDurationSecondsAsync(
            "/some/file.mp4", CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetDurationSecondsAsync_CancellationRequested_ReturnsZero()
    {
        // キャンセル時も 0 を返す（例外を握りつぶす）
        var service = CreateService(new YtDlpOptions { FfprobePath = "/nonexistent/ffprobe" });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.GetDurationSecondsAsync("/some/file.mp4", cts.Token);

        Assert.Equal(0, result);
    }
}
