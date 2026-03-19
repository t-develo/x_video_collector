using Moq;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class ListVideosUseCaseTests
{
    private readonly Mock<IVideoRepository> _videoRepoMock = new();
    private readonly Mock<ITagRepository> _tagRepoMock = new();
    private readonly ListVideosUseCase _sut;

    public ListVideosUseCaseTests()
    {
        _sut = new ListVideosUseCase(_videoRepoMock.Object, _tagRepoMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsPagedResult()
    {
        var videos = new List<Video>
        {
            Video.Create(TweetUrl.Create("https://x.com/u/status/1"), VideoTitle.Create("V1")),
            Video.Create(TweetUrl.Create("https://x.com/u/status/2"), VideoTitle.Create("V2")),
            Video.Create(TweetUrl.Create("https://x.com/u/status/3"), VideoTitle.Create("V3")),
        };
        _videoRepoMock
            .Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(videos);
        _tagRepoMock
            .Setup(r => r.GetByVideoIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync([]);

        var result = await _sut.ExecuteAsync(page: 1, pageSize: 2);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRepository_ReturnsEmptyResult()
    {
        _videoRepoMock
            .Setup(r => r.GetAllAsync(default))
            .ReturnsAsync([]);

        var result = await _sut.ExecuteAsync();

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }
}
