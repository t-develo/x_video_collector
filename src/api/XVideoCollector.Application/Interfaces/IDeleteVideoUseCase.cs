namespace XVideoCollector.Application.Interfaces;

public interface IDeleteVideoUseCase
{
    Task ExecuteAsync(Guid videoId, CancellationToken cancellationToken = default);
}
