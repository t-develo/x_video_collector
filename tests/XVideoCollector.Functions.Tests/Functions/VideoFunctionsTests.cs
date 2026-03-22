using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Text;
using System.Text.Json;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Exceptions;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Application.Services;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Functions.Functions;

namespace XVideoCollector.Functions.Tests.Functions;

public sealed class VideoFunctionsTests
{
    private static VideoDto CreateVideoDto(Guid? id = null, string? blobPath = null) => new(
        Id: id ?? Guid.NewGuid(),
        TweetUrl: "https://x.com/user/status/123",
        TweetId: "123",
        UserName: "user",
        Title: "Test Video",
        Status: VideoStatus.Ready,
        BlobPath: blobPath,
        ThumbnailBlobPath: null,
        DurationSeconds: 60,
        FileSizeBytes: 1024,
        CategoryId: null,
        Tags: [],
        CreatedAt: DateTimeOffset.UtcNow,
        UpdatedAt: DateTimeOffset.UtcNow);

    private static HttpRequest CreateRequest(string method = "GET", string? body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentType = "application/json";
        }
        return context.Request;
    }

    private static VideoFunctions CreateSut(
        Mock<IRegisterVideoUseCase>? registerVideo = null,
        Mock<IGetVideoUseCase>? getVideo = null,
        Mock<IListVideosUseCase>? listVideos = null,
        Mock<IUpdateVideoUseCase>? updateVideo = null,
        Mock<IDeleteVideoUseCase>? deleteVideo = null,
        Mock<ISearchVideosUseCase>? searchVideos = null,
        Mock<IRetryVideoDownloadUseCase>? retryVideoDownload = null,
        Mock<IBlobStorageService>? blobStorage = null,
        Mock<IDownloadQueueService>? downloadQueue = null)
    {
        return new VideoFunctions(
            registerVideo?.Object ?? new Mock<IRegisterVideoUseCase>().Object,
            getVideo?.Object ?? new Mock<IGetVideoUseCase>().Object,
            listVideos?.Object ?? new Mock<IListVideosUseCase>().Object,
            updateVideo?.Object ?? new Mock<IUpdateVideoUseCase>().Object,
            deleteVideo?.Object ?? new Mock<IDeleteVideoUseCase>().Object,
            searchVideos?.Object ?? new Mock<ISearchVideosUseCase>().Object,
            retryVideoDownload?.Object ?? new Mock<IRetryVideoDownloadUseCase>().Object,
            blobStorage?.Object ?? new Mock<IBlobStorageService>().Object,
            downloadQueue?.Object ?? new Mock<IDownloadQueueService>().Object);
    }

    [Fact]
    public async Task GetVideo_WhenFound_ReturnsOk()
    {
        var videoId = Guid.NewGuid();
        var expected = CreateVideoDto(videoId);

        var getVideoMock = new Mock<IGetVideoUseCase>();
        getVideoMock
            .Setup(x => x.ExecuteAsync(videoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = CreateSut(getVideo: getVideoMock);

        var result = await sut.GetVideoAsync(CreateRequest(), videoId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expected, ok.Value);
    }

    [Fact]
    public async Task GetVideo_WhenNotFound_ReturnsNotFound()
    {
        var getVideoMock = new Mock<IGetVideoUseCase>();
        getVideoMock
            .Setup(x => x.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VideoDto?)null);

        var sut = CreateSut(getVideo: getVideoMock);

        var result = await sut.GetVideoAsync(CreateRequest(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ListVideos_ReturnsOk()
    {
        var paginated = new PaginatedResult<VideoListItemDto>([], 0, 1, 20);

        var listVideosMock = new Mock<IListVideosUseCase>();
        listVideosMock
            .Setup(x => x.ExecuteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<VideoSortOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginated);

        var sut = CreateSut(listVideos: listVideosMock);

        var result = await sut.ListVideosAsync(CreateRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(paginated, ok.Value);
    }

    [Fact]
    public async Task ListVideosAsync_WithSortByTitleAsc_PassesTitleAscToUseCase()
    {
        var paginated = new PaginatedResult<VideoListItemDto>([], 0, 1, 20);

        var listVideosMock = new Mock<IListVideosUseCase>();
        listVideosMock
            .Setup(x => x.ExecuteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<VideoSortOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginated);

        var req = CreateRequest();
        req.QueryString = new QueryString("?sortBy=title&sortDir=asc");

        var sut = CreateSut(listVideos: listVideosMock);
        await sut.ListVideosAsync(req, CancellationToken.None);

        listVideosMock.Verify(x => x.ExecuteAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            VideoSortOrder.TitleAsc,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListVideosAsync_WithUnknownSortBy_DefaultsToCreatedAtDesc()
    {
        var paginated = new PaginatedResult<VideoListItemDto>([], 0, 1, 20);

        var listVideosMock = new Mock<IListVideosUseCase>();
        listVideosMock
            .Setup(x => x.ExecuteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<VideoSortOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginated);

        var req = CreateRequest();
        req.QueryString = new QueryString("?sortBy=unknown");

        var sut = CreateSut(listVideos: listVideosMock);
        await sut.ListVideosAsync(req, CancellationToken.None);

        listVideosMock.Verify(x => x.ExecuteAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            VideoSortOrder.CreatedAtDesc,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterVideo_WithValidBody_ReturnsCreated()
    {
        var videoId = Guid.NewGuid();
        var videoDto = CreateVideoDto(videoId);
        var body = JsonSerializer.Serialize(new { tweetUrl = "https://x.com/user/status/123", title = "Test" });

        var registerMock = new Mock<IRegisterVideoUseCase>();
        registerMock
            .Setup(x => x.ExecuteAsync(It.IsAny<RegisterVideoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoDto);

        var downloadQueueMock = new Mock<IDownloadQueueService>();
        downloadQueueMock
            .Setup(x => x.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(registerVideo: registerMock, downloadQueue: downloadQueueMock);

        var result = await sut.RegisterVideoAsync(CreateRequest("POST", body), CancellationToken.None);

        Assert.IsType<CreatedAtRouteResult>(result);
        downloadQueueMock.Verify(x => x.EnqueueAsync(videoId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterVideo_WithInvalidBody_ReturnsBadRequest()
    {
        var sut = CreateSut();

        var result = await sut.RegisterVideoAsync(CreateRequest("POST", "not-valid-json{{"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteVideo_ReturnsNoContent()
    {
        var deleteMock = new Mock<IDeleteVideoUseCase>();
        deleteMock
            .Setup(x => x.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(deleteVideo: deleteMock);

        var result = await sut.DeleteVideoAsync(CreateRequest("DELETE"), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetVideoStreamUrl_WhenVideoReady_ReturnsUrl()
    {
        var videoId = Guid.NewGuid();
        var videoDto = CreateVideoDto(videoId, blobPath: "videos/test.mp4");
        const string expectedUrl = "https://storage.example.com/sas-url";

        var getVideoMock = new Mock<IGetVideoUseCase>();
        getVideoMock
            .Setup(x => x.ExecuteAsync(videoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoDto);

        var blobMock = new Mock<IBlobStorageService>();
        blobMock
            .Setup(x => x.GetSasUrlAsync("videos/test.mp4", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        var sut = CreateSut(getVideo: getVideoMock, blobStorage: blobMock);

        var result = await sut.GetVideoStreamUrlAsync(CreateRequest(), videoId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetVideoStreamUrl_WhenVideoNotReady_ReturnsConflict()
    {
        var videoId = Guid.NewGuid();
        var videoDto = CreateVideoDto(videoId, blobPath: null);

        var getVideoMock = new Mock<IGetVideoUseCase>();
        getVideoMock
            .Setup(x => x.ExecuteAsync(videoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoDto);

        var sut = CreateSut(getVideo: getVideoMock);

        var result = await sut.GetVideoStreamUrlAsync(CreateRequest(), videoId, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task SearchVideos_WithKeyword_ReturnsOk()
    {
        var paginated = new PaginatedResult<VideoListItemDto>([], 0, 1, 20);

        var searchMock = new Mock<ISearchVideosUseCase>();
        searchMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SearchVideoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginated);

        var req = CreateRequest();
        req.QueryString = new QueryString("?q=test&page=1&pageSize=10");

        var sut = CreateSut(searchVideos: searchMock);
        var result = await sut.SearchVideosAsync(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(paginated, ok.Value);
        searchMock.Verify(x => x.ExecuteAsync(
            It.Is<SearchVideoRequest>(r => r.Keyword == "test" && r.Page == 1 && r.PageSize == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchVideos_WithStatusFilter_PassesStatusToUseCase()
    {
        var paginated = new PaginatedResult<VideoListItemDto>([], 0, 1, 20);

        var searchMock = new Mock<ISearchVideosUseCase>();
        searchMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SearchVideoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginated);

        var req = CreateRequest();
        req.QueryString = new QueryString("?status=Ready");

        var sut = CreateSut(searchVideos: searchMock);
        await sut.SearchVideosAsync(req, CancellationToken.None);

        searchMock.Verify(x => x.ExecuteAsync(
            It.Is<SearchVideoRequest>(r => r.Status == VideoStatus.Ready),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchVideos_WithTagIds_PassesTagIdsToUseCase()
    {
        var tagId1 = Guid.NewGuid();
        var tagId2 = Guid.NewGuid();
        var paginated = new PaginatedResult<VideoListItemDto>([], 0, 1, 20);

        var searchMock = new Mock<ISearchVideosUseCase>();
        searchMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SearchVideoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginated);

        var req = CreateRequest();
        req.QueryString = new QueryString($"?tagIds={tagId1},{tagId2}");

        var sut = CreateSut(searchVideos: searchMock);
        await sut.SearchVideosAsync(req, CancellationToken.None);

        searchMock.Verify(x => x.ExecuteAsync(
            It.Is<SearchVideoRequest>(r =>
                r.TagIds != null &&
                r.TagIds.Count == 2 &&
                r.TagIds.Contains(tagId1) &&
                r.TagIds.Contains(tagId2)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchVideos_WithCategoryId_PassesCategoryIdToUseCase()
    {
        var categoryId = Guid.NewGuid();
        var paginated = new PaginatedResult<VideoListItemDto>([], 0, 1, 20);

        var searchMock = new Mock<ISearchVideosUseCase>();
        searchMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SearchVideoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginated);

        var req = CreateRequest();
        req.QueryString = new QueryString($"?categoryId={categoryId}");

        var sut = CreateSut(searchVideos: searchMock);
        await sut.SearchVideosAsync(req, CancellationToken.None);

        searchMock.Verify(x => x.ExecuteAsync(
            It.Is<SearchVideoRequest>(r => r.CategoryId == categoryId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchVideos_WithInvalidStatus_IgnoresStatusFilter()
    {
        var paginated = new PaginatedResult<VideoListItemDto>([], 0, 1, 20);

        var searchMock = new Mock<ISearchVideosUseCase>();
        searchMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SearchVideoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginated);

        var req = CreateRequest();
        req.QueryString = new QueryString("?status=InvalidValue");

        var sut = CreateSut(searchVideos: searchMock);
        await sut.SearchVideosAsync(req, CancellationToken.None);

        searchMock.Verify(x => x.ExecuteAsync(
            It.Is<SearchVideoRequest>(r => r.Status == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterVideo_WhenDuplicateUrl_ReturnsConflict()
    {
        var body = JsonSerializer.Serialize(new { tweetUrl = "https://x.com/user/status/123", title = "Test" });

        var registerMock = new Mock<IRegisterVideoUseCase>();
        registerMock
            .Setup(x => x.ExecuteAsync(It.IsAny<RegisterVideoRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateTweetUrlException("123"));

        var sut = CreateSut(registerVideo: registerMock);

        var result = await sut.RegisterVideoAsync(CreateRequest("POST", body), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task RetryVideoDownload_WhenFailed_ReturnsAccepted()
    {
        var videoId = Guid.NewGuid();

        var retryMock = new Mock<IRetryVideoDownloadUseCase>();
        retryMock
            .Setup(x => x.ExecuteAsync(videoId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(retryVideoDownload: retryMock);

        var result = await sut.RetryVideoDownloadAsync(CreateRequest("POST"), videoId, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        retryMock.Verify(x => x.ExecuteAsync(videoId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryVideoDownload_WhenNotFound_ReturnsNotFound()
    {
        var videoId = Guid.NewGuid();

        var retryMock = new Mock<IRetryVideoDownloadUseCase>();
        retryMock
            .Setup(x => x.ExecuteAsync(videoId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VideoNotFoundException(videoId));

        var sut = CreateSut(retryVideoDownload: retryMock);

        var result = await sut.RetryVideoDownloadAsync(CreateRequest("POST"), videoId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RetryVideoDownload_WhenNotFailed_ReturnsConflict()
    {
        var videoId = Guid.NewGuid();

        var retryMock = new Mock<IRetryVideoDownloadUseCase>();
        retryMock
            .Setup(x => x.ExecuteAsync(videoId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot retry a video with status 'Ready'."));

        var sut = CreateSut(retryVideoDownload: retryMock);

        var result = await sut.RetryVideoDownloadAsync(CreateRequest("POST"), videoId, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }
}
