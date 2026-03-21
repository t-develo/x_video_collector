using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using XVideoCollector.Application.Services;
using XVideoCollector.Infrastructure.Options;

namespace XVideoCollector.Infrastructure.Services;

internal sealed class StorageQueueDownloadQueueService(
    IOptions<QueueStorageOptions> options) : IDownloadQueueService
{
    private readonly QueueStorageOptions _options = options.Value;

    public async Task EnqueueAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var client = new QueueClient(_options.ConnectionString, _options.DownloadQueueName);
        await client.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await client.SendMessageAsync(videoId.ToString(), cancellationToken: cancellationToken);
    }
}
