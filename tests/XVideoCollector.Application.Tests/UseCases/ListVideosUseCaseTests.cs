using Moq;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
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
            Video.Create(TweetUrl.Create("https://x.com/u/status/1"), VideoTitle.Create("V1"), TimeProvider.System),
            Video.Create(TweetUrl.Create("https://x.com/u/status/2"), VideoTitle.Create("V2"), TimeProvider.System),
        };
        _videoRepoMock
            .Setup(r => r.GetPagedAsync(0, 2, It.IsAny<VideoSortOrder>(), default))
            .ReturnsAsync((videos, 3));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

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
            .Setup(r => r.GetPagedAsync(0, 20, It.IsAny<VideoSortOrder>(), default))
            .ReturnsAsync((new List<Video>(), 0));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var result = await _sut.ExecuteAsync();

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ExecuteAsync_PageSizeExceeds100_CapsTo100()
    {
        _videoRepoMock
            .Setup(r => r.GetPagedAsync(0, 100, It.IsAny<VideoSortOrder>(), default))
            .ReturnsAsync((new List<Video>(), 0));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var result = await _sut.ExecuteAsync(page: 1, pageSize: 200);

        Assert.Equal(100, result.PageSize);
        _videoRepoMock.Verify(r => r.GetPagedAsync(0, 100, It.IsAny<VideoSortOrder>(), default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PageSizeLessThan1_DefaultsTo20()
    {
        _videoRepoMock
            .Setup(r => r.GetPagedAsync(0, 20, It.IsAny<VideoSortOrder>(), default))
            .ReturnsAsync((new List<Video>(), 0));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Tag>>());

        var result = await _sut.ExecuteAsync(page: 1, pageSize: 0);

        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task ExecuteAsync_WithTags_MapsTags()
    {
        var video = Video.Create(TweetUrl.Create("https://x.com/u/status/1"), VideoTitle.Create("V1"), TimeProvider.System);
        var tag = Tag.Create("test", Domain.Enums.TagColor.Blue, TimeProvider.System);
        var tagMap = new Dictionary<Guid, IReadOnlyList<Tag>>
        {
            { video.Id, new List<Tag> { tag } }
        };

        _videoRepoMock
            .Setup(r => r.GetPagedAsync(0, 20, It.IsAny<VideoSortOrder>(), default))
            .ReturnsAsync((new List<Video> { video }, 1));
        _tagRepoMock
            .Setup(r => r.GetByVideoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), default))
            .ReturnsAsync(tagMap);

        var result = await _sut.ExecuteAsync();

        Assert.Single(result.Items);
        Assert.Single(result.Items[0].Tags);
        Assert.Equal(tag.Name, result.Items[0].Tags[0].Name);
    }
}
