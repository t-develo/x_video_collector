using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Domain.Tests.Entities;

public class VideoTests
{
    private static TweetUrl MakeTweetUrl() =>
        TweetUrl.Create("https://x.com/user123/status/1234567890");

    private static VideoTitle MakeTitle() =>
        VideoTitle.Create("Test Video");

    [Fact]
    public void Create_ValidArgs_ReturnsVideoWithPendingStatus()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());

        Assert.NotEqual(Guid.Empty, video.Id);
        Assert.Equal(VideoStatus.Pending, video.Status);
        Assert.Null(video.BlobPath);
        Assert.Null(video.ThumbnailBlobPath);
    }

    [Fact]
    public void Create_NullTweetUrl_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Video.Create(null!, MakeTitle()));
    }

    [Fact]
    public void Create_NullTitle_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Video.Create(MakeTweetUrl(), null!));
    }

    [Fact]
    public void StartDownloading_FromPending_SetsDownloadingStatus()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());

        video.StartDownloading();

        Assert.Equal(VideoStatus.Downloading, video.Status);
    }

    [Fact]
    public void StartDownloading_FromFailed_Succeeds()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());
        video.StartDownloading();
        video.MarkFailed();

        video.StartDownloading();

        Assert.Equal(VideoStatus.Downloading, video.Status);
    }

    [Fact]
    public void StartDownloading_FromDownloading_ThrowsInvalidOperationException()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());
        video.StartDownloading();

        Assert.Throws<InvalidOperationException>(() => video.StartDownloading());
    }

    [Fact]
    public void StartProcessing_FromDownloading_SetsProcessingStatus()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());
        video.StartDownloading();

        video.StartProcessing();

        Assert.Equal(VideoStatus.Processing, video.Status);
    }

    [Fact]
    public void StartProcessing_FromPending_ThrowsInvalidOperationException()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());

        Assert.Throws<InvalidOperationException>(() => video.StartProcessing());
    }

    [Fact]
    public void MarkReady_FromProcessing_SetsReadyStatus()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());
        video.StartDownloading();
        video.StartProcessing();

        var blobPath = BlobPath.Create("videos/test.mp4");
        video.MarkReady(blobPath, null, 120, 1024 * 1024);

        Assert.Equal(VideoStatus.Ready, video.Status);
        Assert.Equal(blobPath, video.BlobPath);
        Assert.Equal(120, video.DurationSeconds);
        Assert.Equal(1024 * 1024, video.FileSizeBytes);
    }

    [Fact]
    public void MarkReady_FromPending_ThrowsInvalidOperationException()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());

        Assert.Throws<InvalidOperationException>(() =>
            video.MarkReady(BlobPath.Create("videos/test.mp4"), null, 120, 1024));
    }

    [Fact]
    public void MarkFailed_FromDownloading_SetsFailedStatus()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());
        video.StartDownloading();

        video.MarkFailed();

        Assert.Equal(VideoStatus.Failed, video.Status);
    }

    [Fact]
    public void MarkFailed_FromReady_ThrowsInvalidOperationException()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());
        video.StartDownloading();
        video.StartProcessing();
        video.MarkReady(BlobPath.Create("videos/test.mp4"), null, 120, 1024);

        Assert.Throws<InvalidOperationException>(() => video.MarkFailed());
    }

    [Fact]
    public void UpdateTitle_ChangesTitle()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());
        var newTitle = VideoTitle.Create("Updated Title");

        video.UpdateTitle(newTitle);

        Assert.Equal(newTitle, video.Title);
    }

    [Fact]
    public void UpdateTitle_Null_ThrowsArgumentNullException()
    {
        var video = Video.Create(MakeTweetUrl(), MakeTitle());

        Assert.Throws<ArgumentNullException>(() => video.UpdateTitle(null!));
    }
}
