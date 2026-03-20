using Moq;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class GetVideoUseCaseTests
{
    private readonly Mock<IVideoRepository> _videoRepoMock = new();
    private readonly Mock<ITagRepository> _tagRepoMock = new();
    private readonly GetVideoUseCase _sut;

    public GetVideoUseCaseTests()
    {
        _sut = new GetVideoUseCase(_videoRepoMock.Object, _tagRepoMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingVideo_ReturnsVideoDto()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/user/status/111"),
            VideoTitle.Create("My Video"),
            TimeProvider.System);
        _videoRepoMock
            .Setup(r => r.GetByIdAsync(video.Id, default))
            .ReturnsAsync(video);
        _tagRepoMock
            .Setup(r => r.GetByVideoIdAsync(video.Id, default))
            .ReturnsAsync([]);

        var result = await _sut.ExecuteAsync(video.Id);

        Assert.NotNull(result);
        Assert.Equal(video.Id, result.Id);
        Assert.Equal("My Video", result.Title);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistingVideo_ReturnsNull()
    {
        _videoRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Video?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
