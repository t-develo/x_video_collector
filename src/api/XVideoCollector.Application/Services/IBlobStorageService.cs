namespace XVideoCollector.Application.Services;

public interface IBlobStorageService
{
    Task<string> UploadVideoAsync(Stream stream, string blobName, CancellationToken cancellationToken = default);
    Task<string> UploadThumbnailAsync(Stream stream, string blobName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default);
}
