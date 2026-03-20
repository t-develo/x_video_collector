using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Application.Services;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Functions.Helpers;

namespace XVideoCollector.Functions.Functions;

public sealed class VideoFunctions(
    IRegisterVideoUseCase registerVideo,
    IGetVideoUseCase getVideo,
    IListVideosUseCase listVideos,
    IUpdateVideoUseCase updateVideo,
    IDeleteVideoUseCase deleteVideo,
    IDownloadVideoUseCase downloadVideo,
    ISearchVideosUseCase searchVideos,
    IBlobStorageService blobStorageService,
    ILogger<VideoFunctions> logger)
{
    [Function("RegisterVideo")]
    public async Task<IActionResult> RegisterVideoAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "videos")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var request = await FunctionHelper.ReadBodyAsync<RegisterVideoRequest>(req, cancellationToken);
        if (request is null)
            return new BadRequestObjectResult(new { error = "Invalid request body." });

        var video = await registerVideo.ExecuteAsync(request, cancellationToken);

        // TODO: Queue Trigger（Azure Storage Queue）経由の非同期ダウンロードへ移行すること（Consumption Plan での fire-and-forget 禁止）
        _ = Task.Run(() => downloadVideo.ExecuteAsync(video.Id, CancellationToken.None), CancellationToken.None)
            .ContinueWith(
                t => logger.LogError(t.Exception, "動画ダウンロードでエラーが発生しました。VideoId={VideoId}", video.Id),
                TaskContinuationOptions.OnlyOnFaulted);

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
        var request = await FunctionHelper.ReadBodyAsync<UpdateVideoRequest>(req, cancellationToken);
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

        VideoStatus? status = null;
        var statusStr = req.Query["status"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(statusStr) && Enum.TryParse<VideoStatus>(statusStr, ignoreCase: true, out var parsedStatus))
            status = parsedStatus;

        Guid? categoryId = null;
        var categoryIdStr = req.Query["categoryId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(categoryIdStr) && Guid.TryParse(categoryIdStr, out var parsedCategoryId))
            categoryId = parsedCategoryId;

        IReadOnlyList<Guid>? tagIds = null;
        var tagsStr = req.Query["tagIds"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(tagsStr))
        {
            tagIds = tagsStr
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToList();
        }

        var request = new SearchVideoRequest(
            Keyword: string.IsNullOrWhiteSpace(keyword) ? null : keyword,
            Status: status,
            TagIds: tagIds,
            CategoryId: categoryId,
            Page: page,
            PageSize: pageSize);

        var result = await searchVideos.ExecuteAsync(request, cancellationToken);
        return new OkObjectResult(result);
    }

    private static int ParseIntQuery(HttpRequest req, string key, int defaultValue)
    {
        var value = req.Query[key].FirstOrDefault();
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
