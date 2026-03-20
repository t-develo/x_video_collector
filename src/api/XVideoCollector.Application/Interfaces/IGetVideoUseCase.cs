using XVideoCollector.Application.Dtos;

namespace XVideoCollector.Application.Interfaces;

public interface IGetVideoUseCase
{
    Task<VideoDto?> ExecuteAsync(Guid videoId, CancellationToken cancellationToken = default);
}
