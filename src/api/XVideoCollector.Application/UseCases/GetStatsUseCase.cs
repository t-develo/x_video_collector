using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public sealed class GetStatsUseCase(IVideoRepository videoRepository) : IGetStatsUseCase
{
    public async Task<VideoStatsDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var stats = await videoRepository.GetStatsAsync(cancellationToken);

        return new VideoStatsDto(
            TotalCount: stats.TotalCount,
            PendingCount: stats.PendingCount,
            DownloadingCount: stats.DownloadingCount,
            ProcessingCount: stats.ProcessingCount,
            ReadyCount: stats.ReadyCount,
            FailedCount: stats.FailedCount,
            TotalFileSizeBytes: stats.TotalFileSizeBytes);
    }
}
