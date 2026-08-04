using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Exceptions;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Application.Services;
using XVideoCollector.Domain.Enums;
using XVideoCollector.LocalHost.Helpers;

namespace XVideoCollector.LocalHost.Endpoints;

/// <summary>
/// 動画 API。Azure Functions 版 <c>VideoFunctions</c> と同じルート・同じレスポンスを提供する。
/// </summary>
internal static class VideoEndpoints
{
    internal static void MapVideoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/videos", RegisterAsync);
        app.MapGet("/api/videos", ListAsync);
        app.MapGet("/api/videos/search", SearchAsync);
        app.MapGet("/api/videos/{id:guid}", GetAsync);
        app.MapPut("/api/videos/{id:guid}", UpdateAsync);
        app.MapDelete("/api/videos/{id:guid}", DeleteAsync);
        app.MapPost("/api/videos/{id:guid}/retry", RetryAsync);
        app.MapGet("/api/videos/{id:guid}/stream", GetStreamUrlAsync);
    }

    private static async Task<IResult> RegisterAsync(
        HttpRequest req,
        IRegisterVideoUseCase registerVideo,
        IDownloadQueueService downloadQueue,
        CancellationToken cancellationToken)
    {
        var request = await RequestHelper.ReadBodyAsync<RegisterVideoRequest>(req, cancellationToken);
        if (request is null)
            return Results.BadRequest(new { error = "Invalid request body." });

        try
        {
            var video = await registerVideo.ExecuteAsync(request, cancellationToken);
            await downloadQueue.EnqueueAsync(video.Id, cancellationToken);

            return Results.Created($"/api/videos/{video.Id}", video);
        }
        catch (DuplicateTweetUrlException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ListAsync(
        HttpRequest req,
        IListVideosUseCase listVideos,
        CancellationToken cancellationToken)
    {
        var page = RequestHelper.ParseIntQuery(req, "page", 1);
        var pageSize = RequestHelper.ParseIntQuery(req, "pageSize", 20);
        var sortOrder = RequestHelper.ParseSortOrder(req);

        var result = await listVideos.ExecuteAsync(page, pageSize, sortOrder, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IGetVideoUseCase getVideo,
        CancellationToken cancellationToken)
    {
        var video = await getVideo.ExecuteAsync(id, cancellationToken);
        if (video is null)
            return Results.NotFound(new { error = $"Video '{id}' not found." });

        return Results.Ok(video);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        HttpRequest req,
        IUpdateVideoUseCase updateVideo,
        CancellationToken cancellationToken)
    {
        var request = await RequestHelper.ReadBodyAsync<UpdateVideoRequest>(req, cancellationToken);
        if (request is null)
            return Results.BadRequest(new { error = "Invalid request body." });

        var video = await updateVideo.ExecuteAsync(request with { Id = id }, cancellationToken);
        return Results.Ok(video);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        IDeleteVideoUseCase deleteVideo,
        CancellationToken cancellationToken)
    {
        await deleteVideo.ExecuteAsync(id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RetryAsync(
        Guid id,
        IRetryVideoDownloadUseCase retryVideoDownload,
        CancellationToken cancellationToken)
    {
        try
        {
            await retryVideoDownload.ExecuteAsync(id, cancellationToken);
            return Results.Accepted();
        }
        catch (VideoNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetStreamUrlAsync(
        Guid id,
        IGetVideoUseCase getVideo,
        IBlobStorageService blobStorageService,
        CancellationToken cancellationToken)
    {
        var video = await getVideo.ExecuteAsync(id, cancellationToken);
        if (video is null)
            return Results.NotFound(new { error = $"Video '{id}' not found." });

        if (video.BlobPath is null)
            return Results.Conflict(new { error = "Video is not ready for streaming." });

        var url = await blobStorageService.GetSasUrlAsync(
            video.BlobPath, TimeSpan.FromHours(1), cancellationToken);

        return Results.Ok(new { streamUrl = url });
    }

    private static async Task<IResult> SearchAsync(
        HttpRequest req,
        ISearchVideosUseCase searchVideos,
        CancellationToken cancellationToken)
    {
        var keyword = req.Query["q"].FirstOrDefault();
        var page = RequestHelper.ParseIntQuery(req, "page", 1);
        var pageSize = RequestHelper.ParseIntQuery(req, "pageSize", 20);
        var sortOrder = RequestHelper.ParseSortOrder(req);

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
            PageSize: pageSize,
            SortOrder: sortOrder);

        var result = await searchVideos.ExecuteAsync(request, cancellationToken);
        return Results.Ok(result);
    }
}
