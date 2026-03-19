using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using XVideoCollector.Application.Services;
using XVideoCollector.Infrastructure.Options;

namespace XVideoCollector.Infrastructure.Services;

internal sealed class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _serviceClient;
    private readonly BlobStorageOptions _options;

    public BlobStorageService(IOptions<BlobStorageOptions> options)
    {
        _options = options.Value;
        _serviceClient = new BlobServiceClient(_options.ConnectionString);
    }

    public async Task<string> UploadVideoAsync(
        Stream stream,
        string blobName,
        CancellationToken cancellationToken = default)
        => await UploadAsync(_options.VideoContainerName, stream, blobName, "video/mp4", cancellationToken);

    public async Task<string> UploadThumbnailAsync(
        Stream stream,
        string blobName,
        CancellationToken cancellationToken = default)
        => await UploadAsync(_options.ThumbnailContainerName, stream, blobName, "image/jpeg", cancellationToken);

    public async Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var (containerName, blobName) = ParseBlobPath(blobPath);
        var container = _serviceClient.GetBlobContainerClient(containerName);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var (containerName, blobName) = ParseBlobPath(blobPath);
        var container = _serviceClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(blobName);
        var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public Task<string> GetSasUrlAsync(
        string blobPath,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        var (containerName, blobName) = ParseBlobPath(blobPath);
        var container = _serviceClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(blobName);

        var sasUri = blob.GenerateSasUri(
            Azure.Storage.Sas.BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.Add(expiry));

        return Task.FromResult(sasUri.ToString());
    }

    private async Task<string> UploadAsync(
        string containerName,
        Stream stream,
        string blobName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var container = _serviceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blob = container.GetBlobClient(blobName);
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };

        await blob.UploadAsync(stream, uploadOptions, cancellationToken);

        return $"{containerName}/{blobName}";
    }

    private static (string containerName, string blobName) ParseBlobPath(string blobPath)
    {
        var slashIndex = blobPath.IndexOf('/');
        if (slashIndex < 0)
            throw new ArgumentException($"Invalid blob path (expected 'container/blob'): {blobPath}", nameof(blobPath));

        return (blobPath[..slashIndex], blobPath[(slashIndex + 1)..]);
    }
}
