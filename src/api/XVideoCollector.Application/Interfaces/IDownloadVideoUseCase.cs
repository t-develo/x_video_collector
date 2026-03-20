namespace XVideoCollector.Application.Interfaces;

public interface IDownloadVideoUseCase
{
    Task ExecuteAsync(Guid videoId, CancellationToken cancellationToken = default);
}
