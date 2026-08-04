using XVideoCollector.Application.Interfaces;
using XVideoCollector.Infrastructure.Services;
using XVideoCollector.LocalHost.Helpers;

namespace XVideoCollector.LocalHost.Endpoints;

/// <summary>
/// ローカルファイルシステム上のメディア配信。
/// Azure Blob の SAS URL に相当する署名付き URL を検証してファイルを返す。
/// 動画のシークのため Range リクエストに対応する。
/// </summary>
internal static class MediaEndpoints
{
    private const int ThumbnailCacheSeconds = 86400;

    internal static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/media/{**blobPath}", GetMedia);
        app.MapGet("/api/thumbnails/{id:guid}", GetThumbnailAsync);
    }

    private static IResult GetMedia(
        string blobPath,
        HttpRequest req,
        ILocalMediaAccessor mediaAccessor)
    {
        var expires = req.Query["exp"].FirstOrDefault();
        var signature = req.Query["sig"].FirstOrDefault();

        if (!mediaAccessor.ValidateSignature(blobPath, expires, signature))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (!mediaAccessor.TryResolvePhysicalPath(blobPath, out var physicalPath))
            return Results.NotFound();

        return Results.File(
            physicalPath,
            contentType: RequestHelper.ResolveContentType(physicalPath),
            enableRangeProcessing: true);
    }

    /// <summary>
    /// サムネイル配信。フロントエンド (components/videoCard.js) が参照するエンドポイント。
    /// 動画 ID から解決するため署名は不要（/api/* と同じ認証境界の内側にある）。
    /// </summary>
    private static async Task<IResult> GetThumbnailAsync(
        Guid id,
        HttpContext context,
        IGetVideoUseCase getVideo,
        ILocalMediaAccessor mediaAccessor,
        CancellationToken cancellationToken)
    {
        var video = await getVideo.ExecuteAsync(id, cancellationToken);
        if (video?.ThumbnailBlobPath is null)
            return Results.NotFound();

        if (!mediaAccessor.TryResolvePhysicalPath(video.ThumbnailBlobPath, out var physicalPath))
            return Results.NotFound();

        context.Response.Headers.CacheControl = $"private, max-age={ThumbnailCacheSeconds}";

        return Results.File(
            physicalPath,
            contentType: RequestHelper.ResolveContentType(physicalPath));
    }
}
