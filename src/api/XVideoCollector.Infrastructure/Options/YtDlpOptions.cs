namespace XVideoCollector.Infrastructure.Options;

public sealed class YtDlpOptions
{
    public const string SectionName = "YtDlp";

    public string ExecutablePath { get; set; } = "yt-dlp";
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxFileSizeMB { get; set; } = 500;
}
