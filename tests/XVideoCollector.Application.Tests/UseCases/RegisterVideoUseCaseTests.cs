using Moq;
using XVideoCollector.Application;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Exceptions;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class RegisterVideoUseCaseTests
{
    private readonly Mock<IVideoRepository> _videoRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RegisterVideoUseCase _sut;

    public RegisterVideoUseCaseTests()
    {
        _sut = new RegisterVideoUseCase(_videoRepoMock.Object, _unitOfWorkMock.Object, TimeProvider.System);
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
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
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

    [Fact]
    public async Task ExecuteAsync_DuplicateTweetId_ThrowsDuplicateTweetUrlException()
    {
        var existingVideo = Video.Create(
            TweetUrl.Create("https://x.com/user/status/123456789"),
            VideoTitle.Create("Existing Video"),
            TimeProvider.System);

        _videoRepoMock
            .Setup(r => r.FindByTweetIdAsync("123456789", default))
            .ReturnsAsync(existingVideo);

        var request = new RegisterVideoRequest(
            "https://x.com/user/status/123456789",
            "New Video");

        var ex = await Assert.ThrowsAsync<DuplicateTweetUrlException>(
            () => _sut.ExecuteAsync(request));

        Assert.Equal("123456789", ex.TweetId);
        _videoRepoMock.Verify(r => r.AddAsync(It.IsAny<Video>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_UniqueUrl_CallsAddAndSave()
    {
        _videoRepoMock
            .Setup(r => r.FindByTweetIdAsync(It.IsAny<string>(), default))
            .ReturnsAsync((Video?)null);

        var request = new RegisterVideoRequest(
            "https://x.com/user/status/999999999",
            "Unique Video");

        var result = await _sut.ExecuteAsync(request);

        Assert.NotEqual(Guid.Empty, result.Id);
        _videoRepoMock.Verify(r => r.AddAsync(It.IsAny<Video>(), default), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
