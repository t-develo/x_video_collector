namespace XVideoCollector.Infrastructure.Options;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string VideoContainerName { get; set; } = "videos";
    public string ThumbnailContainerName { get; set; } = "thumbnails";
}
