# Skill: yt-dlp-integration

yt-dlp の C# プロセス呼び出しによる安全な動画ダウンロード実装を標準化するスキル。

## 概要

Azure Functions 上で `yt-dlp` と `ffmpeg` を `Process.Start` で直接呼び出し、X（旧Twitter）の動画をダウンロードする。

## 設定クラス

```csharp
namespace XVideoCollector.Infrastructure.Options;

public class YtDlpOptions
{
    public const string SectionName = "YtDlp";

    public string ExecutablePath { get; set; } = "yt-dlp";
    public string FfmpegPath { get; set; } = "ffmpeg";
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxFileSizeMB { get; set; } = 500;
}
```

## Application 層 — インターフェース

```csharp
namespace XVideoCollector.Application.Interfaces;

public record DownloadResult(
    string FilePath,
    string FileName,
    long FileSizeBytes,
    int DurationSeconds,
    string ThumbnailPath);

public record DownloadProgress(
    double Percentage,
    string Status);

public interface IVideoDownloadService
{
    Task<DownloadResult> DownloadAsync(
        string tweetUrl,
        string outputDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default);

    Task<string> GetVideoInfoJsonAsync(
        string tweetUrl,
        CancellationToken ct = default);
}
```

## Infrastructure 層 — 実装

### プロセス呼び出しパターン

```csharp
namespace XVideoCollector.Infrastructure.Services;

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Infrastructure.Options;

public class YtDlpDownloadService(
    IOptions<YtDlpOptions> options,
    ILogger<YtDlpDownloadService> logger) : IVideoDownloadService
{
    private readonly YtDlpOptions _options = options.Value;

    public async Task<DownloadResult> DownloadAsync(
        string tweetUrl,
        string outputDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        // URL バリデーション（コマンドインジェクション対策）
        ValidateUrl(tweetUrl);

        var outputTemplate = Path.Combine(outputDirectory, "%(id)s.%(ext)s");

        var arguments = BuildDownloadArguments(tweetUrl, outputTemplate);

        var (exitCode, stdout, stderr) = await RunProcessAsync(
            _options.ExecutablePath, arguments, ct);

        if (exitCode != 0)
        {
            logger.LogError("yt-dlp failed with exit code {ExitCode}: {Stderr}",
                exitCode, stderr);
            throw new InvalidOperationException(
                $"yt-dlp failed with exit code {exitCode}: {stderr}");
        }

        return ParseDownloadResult(stdout, outputDirectory);
    }

    public async Task<string> GetVideoInfoJsonAsync(
        string tweetUrl, CancellationToken ct = default)
    {
        ValidateUrl(tweetUrl);

        var arguments = $"--dump-json --no-download \"{tweetUrl}\"";

        var (exitCode, stdout, stderr) = await RunProcessAsync(
            _options.ExecutablePath, arguments, ct);

        if (exitCode != 0)
            throw new InvalidOperationException(
                $"yt-dlp info failed: {stderr}");

        return stdout;
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
        sb.Append("--newline ");  // 進捗を行ごとに出力
        sb.Append($"\"{tweetUrl}\"");
        return sb.ToString();
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
        string fileName, string arguments, CancellationToken ct)
    {
        logger.LogInformation("Running: {FileName} {Arguments}", fileName, arguments);

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
            if (e.Data is not null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource
            .CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            SafeKillProcess(process);
            throw new TimeoutException(
                $"yt-dlp timed out after {_options.TimeoutSeconds}s");
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

    /// <summary>
    /// URL バリデーション — コマンドインジェクション対策
    /// </summary>
    private static void ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid URL: {url}");

        var allowedHosts = new[] { "x.com", "twitter.com", "mobile.twitter.com" };
        if (!allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"URL host not allowed: {uri.Host}");

        // パスに危険な文字がないか確認
        if (url.Contains('\'') || url.Contains('`') || url.Contains('$')
            || url.Contains(';') || url.Contains('|') || url.Contains('&'))
            throw new ArgumentException($"URL contains disallowed characters: {url}");
    }

    private static DownloadResult ParseDownloadResult(
        string stdout, string outputDirectory)
    {
        // yt-dlp 出力からダウンロード済みファイルを特定
        // 実際の実装ではファイルシステムスキャンも併用
        var files = Directory.GetFiles(outputDirectory)
            .Where(f => !f.EndsWith(".jpg") && !f.EndsWith(".json"))
            .OrderByDescending(File.GetCreationTimeUtc)
            .ToList();

        var videoFile = files.FirstOrDefault()
            ?? throw new InvalidOperationException("Downloaded video file not found");

        var thumbnailFile = Directory.GetFiles(outputDirectory, "*.jpg")
            .OrderByDescending(File.GetCreationTimeUtc)
            .FirstOrDefault() ?? string.Empty;

        var fileInfo = new FileInfo(videoFile);

        return new DownloadResult(
            FilePath: videoFile,
            FileName: fileInfo.Name,
            FileSizeBytes: fileInfo.Length,
            DurationSeconds: 0, // メタデータから取得する場合は別途実装
            ThumbnailPath: thumbnailFile);
    }
}
```

## セキュリティ要件（厳守）

### コマンドインジェクション対策

1. **URL ホワイトリスト**: `x.com`, `twitter.com` のみ許可
2. **危険文字の拒否**: `'`, `` ` ``, `$`, `;`, `|`, `&` を含む URL を拒否
3. **URL のクォート**: 引数内の URL は必ずダブルクォートで囲む
4. **`UseShellExecute = false`**: シェル経由の実行を禁止
5. **入力値を引数文字列に直接展開しない**: 必ずバリデーション済みの値のみ使用

### プロセス管理

1. **タイムアウト必須**: デフォルト 300 秒、設定で変更可能
2. **プロセスツリー Kill**: タイムアウト時は `Kill(entireProcessTree: true)`
3. **CancellationToken 対応**: 呼び出し元からのキャンセルに対応
4. **stdout/stderr キャプチャ**: ログ記録とエラー診断のため

### ファイルシステム

1. **一時ディレクトリ使用**: ダウンロードは一時ディレクトリで行い、完了後に Blob へアップロード
2. **ファイルサイズ制限**: `--max-filesize` で上限設定
3. **ダウンロード後クリーンアップ**: Blob アップロード後に一時ファイルを削除

## テストパターン

```csharp
namespace XVideoCollector.Infrastructure.Tests.Services;

public class YtDlpDownloadServiceTests
{
    [Theory]
    [InlineData("https://x.com/user/status/123456789")]
    [InlineData("https://twitter.com/user/status/123456789")]
    public void ValidateUrl_ValidUrls_DoesNotThrow(string url)
    {
        // ValidateUrl は private なのでリフレクションまたは
        // DownloadAsync 経由でテスト
    }

    [Theory]
    [InlineData("https://evil.com/malware")]
    [InlineData("https://x.com/user/status/123; rm -rf /")]
    [InlineData("https://x.com/user/status/123$(whoami)")]
    [InlineData("not-a-url")]
    public void ValidateUrl_InvalidUrls_ThrowsArgumentException(string url)
    {
        // 不正 URL はすべて ArgumentException
    }
}
```

## Azure Functions での利用

### Windows Consumption Plan での注意点

- yt-dlp と ffmpeg はデプロイパッケージに同梱するか、Blob からランタイムにダウンロード
- Windows バイナリ（`.exe`）を使用
- D:\home\site 配下にバイナリを配置
- `WEBSITE_RUN_FROM_PACKAGE` 使用時はバイナリの実行権限に注意

### yt-dlp バイナリのデプロイ方法

```
src/api/XVideoCollector.Functions/
├── tools/
│   ├── yt-dlp.exe
│   └── ffmpeg.exe
└── XVideoCollector.Functions.csproj  ← tools/ を出力にコピーする設定
```

```xml
<!-- .csproj に追加 -->
<ItemGroup>
  <None Update="tools\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```
