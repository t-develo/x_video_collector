using Moq;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
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
            Video.Create(TweetUrl.Create("https://x.com/u/status/10"), VideoTitle.Create("A"), TimeProvider.System),
            Video.Create(TweetUrl.Create("https://x.com/u/status/11"), VideoTitle.Create("B"), TimeProvider.System),
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
                VideoTitle.Create($"V{i}"),
                TimeProvider.System))
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

    [Fact]
    public async Task ExecuteAsync_WithStatusFilter_PassesStatusToQuery()
    {
        VideoSearchQuery? capturedQuery = null;
        _videoRepoMock
            .Setup(r => r.SearchPagedAsync(It.IsAny<VideoSearchQuery>(), 0, 20, default))
            .Callback<VideoSearchQuery, int, int, CancellationToken>((q, _, _, _) => capturedQuery = q)
            .ReturnsAsync((new List<Video>(), 0));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var request = new SearchVideoRequest(Status: VideoStatus.Ready);
        await _sut.ExecuteAsync(request);

        Assert.NotNull(capturedQuery);
        Assert.Equal(VideoStatus.Ready, capturedQuery!.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithTagIds_PassesTagIdsToQuery()
    {
        var tagId = Guid.NewGuid();
        VideoSearchQuery? capturedQuery = null;
        _videoRepoMock
            .Setup(r => r.SearchPagedAsync(It.IsAny<VideoSearchQuery>(), 0, 20, default))
            .Callback<VideoSearchQuery, int, int, CancellationToken>((q, _, _, _) => capturedQuery = q)
            .ReturnsAsync((new List<Video>(), 0));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var request = new SearchVideoRequest(TagIds: [tagId]);
        await _sut.ExecuteAsync(request);

        Assert.NotNull(capturedQuery);
        Assert.NotNull(capturedQuery!.TagIds);
        Assert.Contains(tagId, capturedQuery.TagIds!);
    }

    [Fact]
    public async Task ExecuteAsync_WithCategoryId_PassesCategoryIdToQuery()
    {
        var categoryId = Guid.NewGuid();
        VideoSearchQuery? capturedQuery = null;
        _videoRepoMock
            .Setup(r => r.SearchPagedAsync(It.IsAny<VideoSearchQuery>(), 0, 20, default))
            .Callback<VideoSearchQuery, int, int, CancellationToken>((q, _, _, _) => capturedQuery = q)
            .ReturnsAsync((new List<Video>(), 0));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var request = new SearchVideoRequest(CategoryId: categoryId);
        await _sut.ExecuteAsync(request);

        Assert.NotNull(capturedQuery);
        Assert.Equal(categoryId, capturedQuery!.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_CompoundConditions_PassesAllFiltersToQuery()
    {
        var tagId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        VideoSearchQuery? capturedQuery = null;
        _videoRepoMock
            .Setup(r => r.SearchPagedAsync(It.IsAny<VideoSearchQuery>(), 0, 20, default))
            .Callback<VideoSearchQuery, int, int, CancellationToken>((q, _, _, _) => capturedQuery = q)
            .ReturnsAsync((new List<Video>(), 0));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var request = new SearchVideoRequest(
            Keyword: "複合",
            Status: VideoStatus.Ready,
            TagIds: [tagId],
            CategoryId: categoryId);
        await _sut.ExecuteAsync(request);

        Assert.NotNull(capturedQuery);
        Assert.Equal("複合", capturedQuery!.Keyword);
        Assert.Equal(VideoStatus.Ready, capturedQuery.Status);
        Assert.Contains(tagId, capturedQuery.TagIds!);
        Assert.Equal(categoryId, capturedQuery.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_PageSizeExceeds100_CapsTo100()
    {
        _videoRepoMock
            .Setup(r => r.SearchPagedAsync(It.IsAny<VideoSearchQuery>(), 0, 100, default))
            .ReturnsAsync((new List<Video>(), 0));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var request = new SearchVideoRequest(PageSize: 200);
        var result = await _sut.ExecuteAsync(request);

        Assert.Equal(100, result.PageSize);
        _videoRepoMock.Verify(
            r => r.SearchPagedAsync(It.IsAny<VideoSearchQuery>(), 0, 100, default),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PageSizeLessThan1_DefaultsTo20()
    {
        _videoRepoMock
            .Setup(r => r.SearchPagedAsync(It.IsAny<VideoSearchQuery>(), 0, 20, default))
            .ReturnsAsync((new List<Video>(), 0));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var request = new SearchVideoRequest(PageSize: 0);
        var result = await _sut.ExecuteAsync(request);

        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task ExecuteAsync_PageOutOfRange_NormalizesToPage1()
    {
        _videoRepoMock
            .Setup(r => r.SearchPagedAsync(It.IsAny<VideoSearchQuery>(), 0, 20, default))
            .ReturnsAsync((new List<Video>(), 0));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var request = new SearchVideoRequest(Page: -1, PageSize: 0);
        var result = await _sut.ExecuteAsync(request);

        Assert.Equal(1, result.Page);
    }
}
