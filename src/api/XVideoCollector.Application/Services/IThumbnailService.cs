namespace XVideoCollector.Application.Services;

public interface IThumbnailService
{
    Task<Stream?> GenerateFromVideoAsync(string videoFilePath, CancellationToken cancellationToken = default);
}
