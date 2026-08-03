namespace XVideoCollector.Infrastructure.Options;

public sealed class YtDlpOptions
{
    public const string SectionName = "YtDlp";

    public string ExecutablePath { get; set; } = "yt-dlp";
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxFileSizeMB { get; set; } = 500;

    /// <summary>
    /// Netscape 形式の cookies ファイルのパス（任意）。
    /// X は多くの動画で認証を要求するため、設定されている場合は --cookies に渡す。
    /// 未設定なら引数を付与しない。
    /// </summary>
    public string? CookiesPath { get; set; }
}
