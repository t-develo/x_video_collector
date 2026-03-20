using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XVideoCollector.Application.Services;
using XVideoCollector.Infrastructure.Options;

namespace XVideoCollector.Infrastructure.Services;

public sealed class YtDlpDownloadService(
    IOptions<YtDlpOptions> options,
    ILogger<YtDlpDownloadService> logger) : IVideoDownloadService
{
    private static readonly string[] AllowedHosts =
        ["x.com", "twitter.com", "mobile.twitter.com"];

    private static readonly char[] DisallowedChars =
        ['\'', '`', '$', ';', '|', '&'];

    private readonly YtDlpOptions _options = options.Value;

    public async Task<VideoDownloadResult> DownloadAsync(
        string tweetUrl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUrl(tweetUrl);

        var tempDir = Path.Combine(Path.GetTempPath(), $"ytdlp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputTemplate = Path.Combine(tempDir, "%(id)s.%(ext)s");
            var arguments = BuildDownloadArguments(tweetUrl, outputTemplate);

            var (exitCode, _, stderr) = await RunProcessAsync(
                _options.ExecutablePath, arguments, cancellationToken);

            if (exitCode != 0)
            {
                logger.LogError("yt-dlp failed (exit={ExitCode}): {Stderr}", exitCode, stderr);
                throw new InvalidOperationException(
                    $"yt-dlp failed with exit code {exitCode}: {stderr}");
            }

            return BuildResult(tempDir);
        }
        catch
        {
            // クリーンアップは呼び出し元（DownloadVideoUseCase）の責務だが、
            // 失敗時は自前でクリーンアップする
            TryDeleteDirectory(tempDir);
            throw;
        }
    }

    internal static void ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid URL: {url}", nameof(url));

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            throw new ArgumentException(
                $"URL scheme not allowed: {uri.Scheme}", nameof(url));

        if (!AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"URL host not allowed: {uri.Host}", nameof(url));

        if (url.IndexOfAny(DisallowedChars) >= 0)
            throw new ArgumentException(
                $"URL contains disallowed characters: {url}", nameof(url));
    }

    private string BuildDownloadArguments(string tweetUrl, string outputTemplate)
    {
        var sb = new StringBuilder();
        sb.Append($"--ffmpeg-location \"{_options.FfmpegPath}\" ");
        sb.Append($"--output \"{outputTemplate}\" ");
        sb.Append("--format \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\" ");
        sb.Append("--write-thumbnail ");
        sb.Append("--convert-thumbnails jpg ");
        sb.Append($"--max-filesize {_options.MaxFileSizeMB}M ");
        sb.Append("--no-playlist ");
        sb.Append("--newline ");
        sb.Append($"\"{tweetUrl}\"");
        return sb.ToString();
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
        string fileName, string arguments, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting: {FileName} {Arguments}", fileName, arguments);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
                logger.LogDebug("[yt-dlp] {Line}", e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            SafeKillProcess(process);
            throw new TimeoutException(
                $"yt-dlp timed out after {_options.TimeoutSeconds} seconds.");
        }

        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private void SafeKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                logger.LogWarning("Killed yt-dlp process due to timeout");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to kill yt-dlp process");
        }
    }

    private static VideoDownloadResult BuildResult(string outputDirectory)
    {
        var videoFile = Directory
            .GetFiles(outputDirectory)
            .Where(f => !f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                     && !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetCreationTimeUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Downloaded video file not found in output directory.");

        var fileInfo = new FileInfo(videoFile);

        return new VideoDownloadResult(
            FilePath: videoFile,
            DurationSeconds: 0,  // ffprobe 連携が必要な場合は別途実装
            FileSizeBytes: fileInfo.Length);
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete temp directory: {Path}", path);
        }
    }
}
