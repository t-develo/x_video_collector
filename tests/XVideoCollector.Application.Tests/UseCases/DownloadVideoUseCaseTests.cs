using Moq;
using XVideoCollector.Application.Services;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class DownloadVideoUseCaseTests
{
    private readonly Mock<IVideoRepository> _videoRepoMock = new();
    private readonly Mock<IVideoDownloadService> _downloadMock = new();
    private readonly Mock<IBlobStorageService> _blobMock = new();
    private readonly Mock<IThumbnailService> _thumbnailMock = new();
    private readonly DownloadVideoUseCase _sut;

    public DownloadVideoUseCaseTests()
    {
        _sut = new DownloadVideoUseCase(
            _videoRepoMock.Object,
            _downloadMock.Object,
            _blobMock.Object,
            _thumbnailMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistingVideo_ThrowsInvalidOperationException()
    {
        _videoRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Video?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ExecuteAsync_DownloadFails_MarksVideoAsFailed()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/u/status/77"),
            VideoTitle.Create("Fail Video"));
        _videoRepoMock
            .Setup(r => r.GetByIdAsync(video.Id, default))
            .ReturnsAsync(video);
        _downloadMock
            .Setup(d => d.DownloadAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("yt-dlp failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(video.Id));

        Assert.Equal(VideoStatus.Failed, video.Status);
        _videoRepoMock.Verify(r => r.UpdateAsync(video, default), Times.AtLeast(2));
    }
}
