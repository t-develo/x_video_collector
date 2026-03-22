using XVideoCollector.Application.Dtos;

namespace XVideoCollector.Application.Interfaces;

public interface IGetStatsUseCase
{
    Task<VideoStatsDto> ExecuteAsync(CancellationToken cancellationToken = default);
}
