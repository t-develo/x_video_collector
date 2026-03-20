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
            .Setup(r => r.SearchPagedAsync(It.IsAny<VideoSearchQuery>(), 0, 10, default))
            .ReturnsAsync((videos, 2));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

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

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    public async Task ExecuteAsync_BoundaryPageSizes_ReturnCorrectResults(int totalCount, int expectedPages)
    {
        var videos = Enumerable.Range(0, totalCount)
            .Select(i => Video.Create(
                TweetUrl.Create($"https://x.com/u/status/{i}"),
                VideoTitle.Create($"V{i}")))
            .ToList();

        _videoRepoMock
            .Setup(r => r.SearchPagedAsync(It.IsAny<VideoSearchQuery>(), 0, 10, default))
            .ReturnsAsync((videos, totalCount));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var request = new SearchVideoRequest(Keyword: null, Page: 1, PageSize: 10);
        var result = await _sut.ExecuteAsync(request);

        Assert.Equal(totalCount, result.TotalCount);
        Assert.Equal(expectedPages, result.TotalPages);
    }
}
