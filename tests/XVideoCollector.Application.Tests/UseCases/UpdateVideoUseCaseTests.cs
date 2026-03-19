using Moq;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class UpdateVideoUseCaseTests
{
    private readonly Mock<IVideoRepository> _videoRepoMock = new();
    private readonly Mock<ITagRepository> _tagRepoMock = new();
    private readonly Mock<IVideoTagRepository> _videoTagRepoMock = new();
    private readonly UpdateVideoUseCase _sut;

    public UpdateVideoUseCaseTests()
    {
        _sut = new UpdateVideoUseCase(
            _videoRepoMock.Object,
            _tagRepoMock.Object,
            _videoTagRepoMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_UpdatesVideo()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/u/status/9"),
            VideoTitle.Create("Old Title"));
        var categoryId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        _videoRepoMock
            .Setup(r => r.GetByIdAsync(video.Id, default))
            .ReturnsAsync(video);
        _tagRepoMock
            .Setup(r => r.GetByVideoIdAsync(video.Id, default))
            .ReturnsAsync([]);

        var request = new UpdateVideoRequest(video.Id, "New Title", categoryId, [tagId]);
        var result = await _sut.ExecuteAsync(request);

        Assert.Equal("New Title", result.Title);
        Assert.Equal(categoryId, result.CategoryId);
        _videoTagRepoMock.Verify(
            r => r.DeleteByVideoIdAsync(video.Id, default), Times.Once);
        _videoTagRepoMock.Verify(
            r => r.AddAsync(It.Is<VideoTag>(vt => vt.TagId == tagId), default), Times.Once);
        _videoRepoMock.Verify(r => r.UpdateAsync(video, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistingVideo_ThrowsInvalidOperationException()
    {
        _videoRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Video?)null);

        var request = new UpdateVideoRequest(Guid.NewGuid(), "Title", null, []);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(request));
    }
}
