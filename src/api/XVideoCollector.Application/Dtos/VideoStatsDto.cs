namespace XVideoCollector.Application.Dtos;

public sealed record VideoStatsDto(
    int TotalCount,
    int PendingCount,
    int DownloadingCount,
    int ProcessingCount,
    int ReadyCount,
    int FailedCount,
    long TotalFileSizeBytes);
