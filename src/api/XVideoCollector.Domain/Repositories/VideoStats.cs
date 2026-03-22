namespace XVideoCollector.Domain.Repositories;

public sealed record VideoStats(
    int TotalCount,
    int PendingCount,
    int DownloadingCount,
    int ProcessingCount,
    int ReadyCount,
    int FailedCount,
    long TotalFileSizeBytes);
