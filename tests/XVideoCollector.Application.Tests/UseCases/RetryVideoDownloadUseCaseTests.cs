using Moq;
using XVideoCollector.Application;
using XVideoCollector.Application.Exceptions;
using XVideoCollector.Application.Services;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class RetryVideoDownloadUseCaseTests
{
    private readonly Mock<IVideoRepository> _videoRepoMock = new();
    private readonly Mock<IDownloadQueueService> _downloadQueueMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RetryVideoDownloadUseCase _sut;

    public RetryVideoDownloadUseCaseTests()
    {
        _sut = new RetryVideoDownloadUseCase(
            _videoRepoMock.Object,
            _downloadQueueMock.Object,
            _unitOfWorkMock.Object,
            TimeProvider.System);
    }

    private static Video CreateFailedVideo()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/user/status/123456789"),
            VideoTitle.Create("Test Video"),
            TimeProvider.System);
        video.StartDownloading(TimeProvider.System);
        video.MarkFailed(null, TimeProvider.System);
        return video;
    }

    [Fact]
    public async Task ExecuteAsync_FailedVideo_ResetsStatusAndEnqueues()
    {
        var video = CreateFailedVideo();
        var videoId = video.Id;

        _videoRepoMock
            .Setup(r => r.GetByIdAsync(videoId, default))
            .ReturnsAsync(video);

        _downloadQueueMock
            .Setup(q => q.EnqueueAsync(videoId, default))
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(videoId);

        Assert.Equal(VideoStatus.Pending, video.Status);
        _videoRepoMock.Verify(r => r.UpdateAsync(video, default), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
        _downloadQueueMock.Verify(q => q.EnqueueAsync(videoId, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_VideoNotFound_ThrowsVideoNotFoundException()
    {
        var videoId = Guid.NewGuid();

        _videoRepoMock
            .Setup(r => r.GetByIdAsync(videoId, default))
            .ReturnsAsync((Video?)null);

        var ex = await Assert.ThrowsAsync<VideoNotFoundException>(
            () => _sut.ExecuteAsync(videoId));

        Assert.Equal(videoId, ex.VideoId);
        _downloadQueueMock.Verify(q => q.EnqueueAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PendingVideo_ThrowsInvalidOperationException()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/user/status/123456789"),
            VideoTitle.Create("Pending Video"),
            TimeProvider.System);

        _videoRepoMock
            .Setup(r => r.GetByIdAsync(video.Id, default))
            .ReturnsAsync(video);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(video.Id));

        _downloadQueueMock.Verify(q => q.EnqueueAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReadyVideo_ThrowsInvalidOperationException()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/user/status/123456789"),
            VideoTitle.Create("Ready Video"),
            TimeProvider.System);
        video.StartDownloading(TimeProvider.System);
        video.StartProcessing(TimeProvider.System);
        video.MarkReady(
            BlobPath.Create("videos/test.mp4"),
            null,
            60,
            1024,
            TimeProvider.System);

        _videoRepoMock
            .Setup(r => r.GetByIdAsync(video.Id, default))
            .ReturnsAsync(video);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(video.Id));

        _downloadQueueMock.Verify(q => q.EnqueueAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}
