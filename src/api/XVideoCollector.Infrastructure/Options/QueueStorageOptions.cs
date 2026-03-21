namespace XVideoCollector.Infrastructure.Options;

public sealed class QueueStorageOptions
{
    public const string SectionName = "QueueStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string DownloadQueueName { get; set; } = "video-download-requests";
}
