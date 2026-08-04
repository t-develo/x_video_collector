namespace XVideoCollector.LocalHost.Workers;

public sealed class DownloadWorkerOptions
{
    public const string SectionName = "DownloadWorker";

    /// <summary>DB を走査して未処理の動画を拾い直す間隔 (秒)。</summary>
    public int SweepIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Downloading / Processing のまま滞留している動画を「中断された」と判定するまでの時間 (分)。
    /// </summary>
    public int StaleAfterMinutes { get; set; } = 30;
}
