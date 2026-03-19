using Moq;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class RegisterVideoUseCaseTests
{
    private readonly Mock<IVideoRepository> _videoRepoMock = new();
    private readonly RegisterVideoUseCase _sut;

    public RegisterVideoUseCaseTests()
    {
        _sut = new RegisterVideoUseCase(_videoRepoMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsVideoDto()
    {
        var request = new RegisterVideoRequest(
            "https://x.com/user/status/123456789",
            "Test Video");

        var result = await _sut.ExecuteAsync(request);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("https://x.com/user/status/123456789", result.TweetUrl);
        Assert.Equal("Test Video", result.Title);
        Assert.Equal(Domain.Enums.VideoStatus.Pending, result.Status);
        _videoRepoMock.Verify(r => r.AddAsync(It.IsAny<Video>(), default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidUrl_ThrowsArgumentException()
    {
        var request = new RegisterVideoRequest("https://invalid.com/url", "Title");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ExecuteAsync(request));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyTitle_ThrowsArgumentException()
    {
        var request = new RegisterVideoRequest(
            "https://x.com/user/status/123456789",
            "   ");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ExecuteAsync(request));
    }
}
