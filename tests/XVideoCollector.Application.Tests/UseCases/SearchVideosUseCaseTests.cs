using Moq;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class SearchVideosUseCaseTests
{
    private readonly Mock<IVideoRepository> _videoRepoMock = new();
    private readonly Mock<ITagRepository> _tagRepoMock = new();
    private readonly SearchVideosUseCase _sut;

    public SearchVideosUseCaseTests()
    {
        _sut = new SearchVideosUseCase(_videoRepoMock.Object, _tagRepoMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsPagedResults()
    {
        var videos = new List<Video>
        {
            Video.Create(TweetUrl.Create("https://x.com/u/status/10"), VideoTitle.Create("A")),
            Video.Create(TweetUrl.Create("https://x.com/u/status/11"), VideoTitle.Create("B")),
        };
        _videoRepoMock
            .Setup(r => r.SearchAsync(It.IsAny<VideoSearchQuery>(), default))
            .ReturnsAsync(videos);
        _tagRepoMock
            .Setup(r => r.GetByVideoIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync([]);

        var request = new SearchVideoRequest(Keyword: "test", Page: 1, PageSize: 10);
        var result = await _sut.ExecuteAsync(request);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ExecuteAsync(null!));
    }
}
