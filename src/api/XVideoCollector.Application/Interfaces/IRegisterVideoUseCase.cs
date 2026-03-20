using XVideoCollector.Application.Dtos;

namespace XVideoCollector.Application.Interfaces;

public interface IRegisterVideoUseCase
{
    Task<VideoDto> ExecuteAsync(RegisterVideoRequest request, CancellationToken cancellationToken = default);
}
