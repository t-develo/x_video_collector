using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Services;
using XVideoCollector.Application.UseCases;

namespace XVideoCollector.Functions.Functions;

public sealed class VideoFunctions(
    RegisterVideoUseCase registerVideo,
    GetVideoUseCase getVideo,
    ListVideosUseCase listVideos,
    UpdateVideoUseCase updateVideo,
    DeleteVideoUseCase deleteVideo,
    DownloadVideoUseCase downloadVideo,
    SearchVideosUseCase searchVideos,
    IBlobStorageService blobStorageService,
    ILogger<VideoFunctions> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Function("RegisterVideo")]
    public async Task<IActionResult> RegisterVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "videos")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<RegisterVideoRequest>(req, cancellationToken);
        if (request is null)
            return new BadRequestObjectResult(new { error = "Invalid request body." });

        var video = await registerVideo.ExecuteAsync(request, cancellationToken);

        _ = Task.Run(() => downloadVideo.ExecuteAsync(video.Id, CancellationToken.None), CancellationToken.None);

        return new CreatedAtRouteResult(
            routeName: null,
            routeValues: new { id = video.Id },
            value: video);
    }

    [Function("ListVideos")]
    public async Task<IActionResult> ListVideosAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "videos")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var page = ParseIntQuery(req, "page", 1);
        var pageSize = ParseIntQuery(req, "pageSize", 20);

        var result = await listVideos.ExecuteAsync(page, pageSize, cancellationToken);
        return new OkObjectResult(result);
    }

    [Function("GetVideo")]
    public async Task<IActionResult> GetVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "videos/{id:guid}")] HttpRequest req,
        Guid id,
        CancellationToken cancellationToken)
    {
        var video = await getVideo.ExecuteAsync(id, cancellationToken);
        if (video is null)
            return new NotFoundObjectResult(new { error = $"Video '{id}' not found." });

        return new OkObjectResult(video);
    }

    [Function("UpdateVideo")]
    public async Task<IActionResult> UpdateVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "videos/{id:guid}")] HttpRequest req,
        Guid id,
        CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<UpdateVideoRequest>(req, cancellationToken);
        if (request is null)
            return new BadRequestObjectResult(new { error = "Invalid request body." });

        var requestWithId = request with { Id = id };
        var video = await updateVideo.ExecuteAsync(requestWithId, cancellationToken);
        return new OkObjectResult(video);
    }

    [Function("DeleteVideo")]
    public async Task<IActionResult> DeleteVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "videos/{id:guid}")] HttpRequest req,
        Guid id,
        CancellationToken cancellationToken)
    {
        await deleteVideo.ExecuteAsync(id, cancellationToken);
        return new NoContentResult();
    }

    [Function("GetVideoStreamUrl")]
    public async Task<IActionResult> GetVideoStreamUrlAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "videos/{id:guid}/stream")] HttpRequest req,
        Guid id,
        CancellationToken cancellationToken)
    {
        var video = await getVideo.ExecuteAsync(id, cancellationToken);
        if (video is null)
            return new NotFoundObjectResult(new { error = $"Video '{id}' not found." });

        if (video.BlobPath is null)
            return new ConflictObjectResult(new { error = "Video is not ready for streaming." });

        var url = await blobStorageService.GetSasUrlAsync(
            video.BlobPath, TimeSpan.FromHours(1), cancellationToken);

        return new OkObjectResult(new { streamUrl = url });
    }

    [Function("SearchVideos")]
    public async Task<IActionResult> SearchVideosAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "videos/search")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var keyword = req.Query["q"].FirstOrDefault();
        var page = ParseIntQuery(req, "page", 1);
        var pageSize = ParseIntQuery(req, "pageSize", 20);

        var request = new SearchVideoRequest(
            Keyword: string.IsNullOrWhiteSpace(keyword) ? null : keyword,
            Page: page,
            PageSize: pageSize);

        var result = await searchVideos.ExecuteAsync(request, cancellationToken);
        return new OkObjectResult(result);
    }

    private static async Task<T?> ReadBodyAsync<T>(HttpRequest req, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(req.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static int ParseIntQuery(HttpRequest req, string key, int defaultValue)
    {
        var value = req.Query[key].FirstOrDefault();
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
