using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XVideoCollector.Application.Services;
using XVideoCollector.Infrastructure.Options;

namespace XVideoCollector.Infrastructure.Services;

public sealed class FfmpegThumbnailService(
    IOptions<YtDlpOptions> options,
    ILogger<FfmpegThumbnailService> logger) : IThumbnailService
{
    private readonly YtDlpOptions _options = options.Value;

    public async Task<Stream?> GenerateFromVideoAsync(
        string videoFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(videoFilePath))
        {
            logger.LogWarning("Video file not found for thumbnail generation: {Path}", videoFilePath);
            return null;
        }

        var thumbnailPath = Path.ChangeExtension(videoFilePath, ".thumb.jpg");

        try
        {
            // yt-dlp の --write-thumbnail で既にサムネイルが生成されている場合はそれを使用
            var ytdlpThumbnail = Path.ChangeExtension(videoFilePath, ".jpg");
            if (File.Exists(ytdlpThumbnail))
            {
                logger.LogInformation("Using yt-dlp generated thumbnail: {Path}", ytdlpThumbnail);
                return new FileStream(
                    ytdlpThumbnail,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);
            }

            // ffmpeg でサムネイルを生成（動画の先頭フレーム）
            var generated = await GenerateWithFfmpegAsync(
                videoFilePath, thumbnailPath, cancellationToken);

            if (!generated || !File.Exists(thumbnailPath))
            {
                logger.LogWarning("Thumbnail generation failed for: {Path}", videoFilePath);
                return null;
            }

            return new FileStream(
                thumbnailPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating thumbnail for: {Path}", videoFilePath);
            return null;
        }
    }

    private async Task<bool> GenerateWithFfmpegAsync(
        string videoFilePath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        // -ss 0: 先頭フレーム, -vframes 1: 1フレームのみ, -q:v 2: 高品質JPEG
        var arguments =
            $"-y -ss 0 -i \"{videoFilePath}\" -vframes 1 -q:v 2 \"{outputPath}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _options.FfmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { /* ignore */ }

            logger.LogWarning("ffmpeg thumbnail generation timed out for: {Path}", videoFilePath);
            return false;
        }

        if (process.ExitCode != 0)
        {
            logger.LogWarning("ffmpeg exited with code {ExitCode} for: {Path}",
                process.ExitCode, videoFilePath);
            return false;
        }

        return true;
    }
}
