using Moq;
using XVideoCollector.Application.Services;

namespace XVideoCollector.Infrastructure.Tests.Services;

public sealed class BlobStorageServiceTests
{
    private readonly Mock<IBlobStorageService> _mock = new();

    [Fact]
    public async Task UploadVideoAsync_ReturnsNonEmptyBlobPath()
    {
        _mock.Setup(s => s.UploadVideoAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("videos/test.mp4");

        var result = await _mock.Object.UploadVideoAsync(Stream.Null, "test.mp4");

        Assert.False(string.IsNullOrEmpty(result));
        Assert.Equal("videos/test.mp4", result);
    }

    [Fact]
    public async Task UploadThumbnailAsync_ReturnsNonEmptyBlobPath()
    {
        _mock.Setup(s => s.UploadThumbnailAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("thumbnails/test.jpg");

        var result = await _mock.Object.UploadThumbnailAsync(Stream.Null, "test.jpg");

        Assert.False(string.IsNullOrEmpty(result));
        Assert.Equal("thumbnails/test.jpg", result);
    }

    [Fact]
    public async Task DeleteAsync_CallsServiceWithCorrectPath()
    {
        _mock.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _mock.Object.DeleteAsync("videos/test.mp4");

        _mock.Verify(s => s.DeleteAsync("videos/test.mp4", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsStream()
    {
        var expectedStream = new MemoryStream([1, 2, 3]);
        _mock.Setup(s => s.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStream);

        var result = await _mock.Object.OpenReadAsync("videos/test.mp4");

        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
    }
}
