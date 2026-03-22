namespace XVideoCollector.Application.Interfaces;

public interface IRetryVideoDownloadUseCase
{
    Task ExecuteAsync(Guid videoId, CancellationToken cancellationToken = default);
}
