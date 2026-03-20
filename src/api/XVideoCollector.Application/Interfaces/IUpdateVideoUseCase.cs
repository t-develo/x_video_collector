using XVideoCollector.Application.Dtos;

namespace XVideoCollector.Application.Interfaces;

public interface IUpdateVideoUseCase
{
    Task<VideoDto> ExecuteAsync(UpdateVideoRequest request, CancellationToken cancellationToken = default);
}
