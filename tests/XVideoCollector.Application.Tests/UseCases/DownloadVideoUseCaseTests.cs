using Moq;
using XVideoCollector.Application;
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
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DownloadVideoUseCase _sut;

    public DownloadVideoUseCaseTests()
    {
        _sut = new DownloadVideoUseCase(
            _videoRepoMock.Object,
            _downloadMock.Object,
            _blobMock.Object,
            _thumbnailMock.Object,
            _unitOfWorkMock.Object,
            TimeProvider.System);
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
            VideoTitle.Create("Fail Video"),
            TimeProvider.System);
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

    [Fact]
    public async Task ExecuteAsync_DownloadFails_RecordsFailureReason()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/u/status/78"),
            VideoTitle.Create("Fail Reason Video"),
            TimeProvider.System);
        const string errorMessage = "yt-dlp: ERROR: Unable to extract video data";

        _videoRepoMock
            .Setup(r => r.GetByIdAsync(video.Id, default))
            .ReturnsAsync(video);
        _downloadMock
            .Setup(d => d.DownloadAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException(errorMessage));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(video.Id));

        Assert.Equal(VideoStatus.Failed, video.Status);
        Assert.Equal(errorMessage, video.FailureReason);
    }

    [Theory]
    [InlineData("video.mp4", "video/mp4")]
    [InlineData("video.webm", "video/webm")]
    [InlineData("video.mov", "video/quicktime")]
    [InlineData("video.mkv", "video/x-matroska")]
    [InlineData("video.unknown", "video/mp4")]
    public async Task ExecuteAsync_VideoDownloaded_UsesCorrectContentType(
        string fileName, string expectedContentType)
    {
        // Arrange
        var video = Video.Create(
            TweetUrl.Create("https://x.com/u/status/99"),
            VideoTitle.Create("Content Type Video"),
            TimeProvider.System);

        // ユニークな一時ディレクトリを作成してファイル競合を防ぐ
        var tempDir = Path.Combine(Path.GetTempPath(), $"xvc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, fileName);
        await File.WriteAllBytesAsync(tempFile, []);

        try
        {
            _videoRepoMock
                .Setup(r => r.GetByIdAsync(video.Id, default))
                .ReturnsAsync(video);
            _downloadMock
                .Setup(d => d.DownloadAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new VideoDownloadResult(tempFile, 30, 1024));
            _blobMock
                .Setup(b => b.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), default))
                .ReturnsAsync("videos/test.mp4");
            _thumbnailMock
                .Setup(t => t.GenerateFromVideoAsync(It.IsAny<string>(), default))
                .ReturnsAsync((Stream?)null);

            // Act
            await _sut.ExecuteAsync(video.Id);

            // Assert
            _blobMock.Verify(b => b.UploadVideoAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                expectedContentType,
                default), Times.Once);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
