using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Domain.Entities;

public sealed class Video
{
    public Guid Id { get; private set; }
    public TweetUrl TweetUrl { get; private set; }
    public VideoTitle Title { get; private set; }
    public VideoStatus Status { get; private set; }
    public BlobPath? BlobPath { get; private set; }
    public BlobPath? ThumbnailBlobPath { get; private set; }
    public int? DurationSeconds { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public Guid? CategoryId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Video(
        Guid id,
        TweetUrl tweetUrl,
        VideoTitle title,
        VideoStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        TweetUrl = tweetUrl;
        Title = title;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Video Create(TweetUrl tweetUrl, VideoTitle title)
    {
        ArgumentNullException.ThrowIfNull(tweetUrl);
        ArgumentNullException.ThrowIfNull(title);

        return new Video(
            Guid.NewGuid(),
            tweetUrl,
            title,
            VideoStatus.Pending,
            DateTimeOffset.UtcNow);
    }

    public void StartDownloading()
    {
        if (Status != VideoStatus.Pending && Status != VideoStatus.Failed)
            throw new InvalidOperationException($"Cannot start downloading from status '{Status}'.");

        Status = VideoStatus.Downloading;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void StartProcessing()
    {
        if (Status != VideoStatus.Downloading)
            throw new InvalidOperationException($"Cannot start processing from status '{Status}'.");

        Status = VideoStatus.Processing;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkReady(
        BlobPath blobPath,
        BlobPath? thumbnailBlobPath,
        int durationSeconds,
        long fileSizeBytes)
    {
        if (Status != VideoStatus.Processing)
            throw new InvalidOperationException($"Cannot mark ready from status '{Status}'.");

        ArgumentNullException.ThrowIfNull(blobPath);
        if (durationSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        if (fileSizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes));

        BlobPath = blobPath;
        ThumbnailBlobPath = thumbnailBlobPath;
        DurationSeconds = durationSeconds;
        FileSizeBytes = fileSizeBytes;
        Status = VideoStatus.Ready;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed()
    {
        if (Status == VideoStatus.Ready)
            throw new InvalidOperationException("Cannot mark a ready video as failed.");

        Status = VideoStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateTitle(VideoTitle title)
    {
        ArgumentNullException.ThrowIfNull(title);

        Title = title;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetCategory(Guid? categoryId)
    {
        CategoryId = categoryId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
