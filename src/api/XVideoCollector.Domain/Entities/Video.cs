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

    // EF Core 用
#pragma warning disable CS8618
    private Video() { }
#pragma warning restore CS8618

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

    public static Video Create(TweetUrl tweetUrl, VideoTitle title, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(tweetUrl);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new Video(
            Guid.NewGuid(),
            tweetUrl,
            title,
            VideoStatus.Pending,
            timeProvider.GetUtcNow());
    }

    public void StartDownloading(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != VideoStatus.Pending && Status != VideoStatus.Failed)
            throw new InvalidOperationException($"Cannot start downloading from status '{Status}'.");

        Status = VideoStatus.Downloading;
        UpdatedAt = timeProvider.GetUtcNow();
    }

    public void StartProcessing(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != VideoStatus.Downloading)
            throw new InvalidOperationException($"Cannot start processing from status '{Status}'.");

        Status = VideoStatus.Processing;
        UpdatedAt = timeProvider.GetUtcNow();
    }

    public void MarkReady(
        BlobPath blobPath,
        BlobPath? thumbnailBlobPath,
        int durationSeconds,
        long fileSizeBytes,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

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
        UpdatedAt = timeProvider.GetUtcNow();
    }

    public void MarkFailed(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status == VideoStatus.Ready)
            throw new InvalidOperationException("Cannot mark a ready video as failed.");

        Status = VideoStatus.Failed;
        UpdatedAt = timeProvider.GetUtcNow();
    }

    public void ResetToPending(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != VideoStatus.Failed)
            throw new InvalidOperationException($"Cannot reset to pending from status '{Status}'.");

        Status = VideoStatus.Pending;
        UpdatedAt = timeProvider.GetUtcNow();
    }

    public void UpdateTitle(VideoTitle title, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Title = title;
        UpdatedAt = timeProvider.GetUtcNow();
    }

    public void SetCategory(Guid? categoryId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        CategoryId = categoryId;
        UpdatedAt = timeProvider.GetUtcNow();
    }
}
