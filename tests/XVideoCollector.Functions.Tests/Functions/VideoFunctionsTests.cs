using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;
using System.Text.Json;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Services;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Functions.Functions;

namespace XVideoCollector.Functions.Tests.Functions;

public sealed class VideoFunctionsTests
{
    private static readonly Mock<IVideoRepository> VideoRepo = new();
    private static readonly Mock<ITagRepository> TagRepo = new();
    private static readonly Mock<IVideoTagRepository> VideoTagRepo = new();
    private static readonly Mock<IBlobStorageService> Blob = new();
    private static readonly Mock<IVideoDownloadService> DownloadService = new();
    private static readonly Mock<IThumbnailService> ThumbnailService = new();

    private static Mock<RegisterVideoUseCase> DefaultRegisterMock() =>
        new(VideoRepo.Object);

    private static Mock<GetVideoUseCase> DefaultGetMock() =>
        new(VideoRepo.Object, TagRepo.Object);

    private static Mock<ListVideosUseCase> DefaultListMock() =>
        new(VideoRepo.Object, TagRepo.Object);

    private static Mock<UpdateVideoUseCase> DefaultUpdateMock() =>
        new(VideoRepo.Object, TagRepo.Object, VideoTagRepo.Object);

    private static Mock<DeleteVideoUseCase> DefaultDeleteMock() =>
        new(VideoRepo.Object, VideoTagRepo.Object, Blob.Object);

    private static Mock<DownloadVideoUseCase> DefaultDownloadMock() =>
        new(VideoRepo.Object, DownloadService.Object, Blob.Object, ThumbnailService.Object);

    private static Mock<SearchVideosUseCase> DefaultSearchMock() =>
        new(VideoRepo.Object, TagRepo.Object);

    private static VideoFunctions CreateSut(
        Mock<RegisterVideoUseCase>? registerVideo = null,
        Mock<GetVideoUseCase>? getVideo = null,
        Mock<ListVideosUseCase>? listVideos = null,
        Mock<UpdateVideoUseCase>? updateVideo = null,
        Mock<DeleteVideoUseCase>? deleteVideo = null,
        Mock<DownloadVideoUseCase>? downloadVideo = null,
        Mock<SearchVideosUseCase>? searchVideos = null,
        Mock<IBlobStorageService>? blobStorage = null)
    {
        return new VideoFunctions(
            registerVideo?.Object ?? DefaultRegisterMock().Object,
            getVideo?.Object ?? DefaultGetMock().Object,
            listVideos?.Object ?? DefaultListMock().Object,
            updateVideo?.Object ?? DefaultUpdateMock().Object,
            deleteVideo?.Object ?? DefaultDeleteMock().Object,
            downloadVideo?.Object ?? DefaultDownloadMock().Object,
            searchVideos?.Object ?? DefaultSearchMock().Object,
            blobStorage?.Object ?? Blob.Object,
            NullLogger<VideoFunctions>.Instance);
    }

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

    [Fact]
    public async Task GetVideo_WhenFound_ReturnsOk()
    {
        var videoId = Guid.NewGuid();
        var expected = CreateVideoDto(videoId);

        var getVideoMock = DefaultGetMock();
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
        var getVideoMock = DefaultGetMock();
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

        var listVideosMock = DefaultListMock();
        listVideosMock
            .Setup(x => x.ExecuteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginated);

        var sut = CreateSut(listVideos: listVideosMock);

        var result = await sut.ListVideosAsync(CreateRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(paginated, ok.Value);
    }

    [Fact]
    public async Task RegisterVideo_WithValidBody_ReturnsCreated()
    {
        var videoId = Guid.NewGuid();
        var videoDto = CreateVideoDto(videoId);
        var body = JsonSerializer.Serialize(new { tweetUrl = "https://x.com/user/status/123", title = "Test" });

        var registerMock = DefaultRegisterMock();
        registerMock
            .Setup(x => x.ExecuteAsync(It.IsAny<RegisterVideoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoDto);

        var downloadMock = DefaultDownloadMock();

        var sut = CreateSut(registerVideo: registerMock, downloadVideo: downloadMock);

        var result = await sut.RegisterVideoAsync(CreateRequest("POST", body), CancellationToken.None);

        Assert.IsType<CreatedAtRouteResult>(result);
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
        var deleteMock = DefaultDeleteMock();

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

        var getVideoMock = DefaultGetMock();
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

        var getVideoMock = DefaultGetMock();
        getVideoMock
            .Setup(x => x.ExecuteAsync(videoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoDto);

        var sut = CreateSut(getVideo: getVideoMock);

        var result = await sut.GetVideoStreamUrlAsync(CreateRequest(), videoId, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }
}
