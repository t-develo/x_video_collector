using System.Text.Json;
using System.Text.Json.Serialization;
using XVideoCollector.Domain.Enums;

namespace XVideoCollector.LocalHost.Helpers;

/// <summary>
/// リクエスト解析ヘルパー。
/// Azure Functions 版 (<c>FunctionHelper</c>) と同じ挙動・同じレスポンス形状を維持するため、
/// JSON 設定とクエリ解析ロジックを揃えている。
/// </summary>
internal static class RequestHelper
{
    /// <summary>アプリケーション共通の JSON シリアライズ設定。</summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// リクエストボディを JSON として読み取る。解析に失敗した場合は default を返す。
    /// </summary>
    internal static async Task<T?> ReadBodyAsync<T>(HttpRequest req, CancellationToken cancellationToken)
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

    internal static int ParseIntQuery(HttpRequest req, string key, int defaultValue)
    {
        var value = req.Query[key].FirstOrDefault();
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    internal static VideoSortOrder ParseSortOrder(HttpRequest req)
    {
        var sortBy = req.Query["sortBy"].FirstOrDefault() ?? string.Empty;
        var sortDir = req.Query["sortDir"].FirstOrDefault() ?? string.Empty;
        var isDesc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "title" => isDesc ? VideoSortOrder.TitleDesc : VideoSortOrder.TitleAsc,
            "duration" => VideoSortOrder.DurationDesc,
            "filesize" => VideoSortOrder.FileSizeDesc,
            "createdat" => isDesc ? VideoSortOrder.CreatedAtDesc : VideoSortOrder.CreatedAtAsc,
            _ => isDesc ? VideoSortOrder.CreatedAtDesc : VideoSortOrder.CreatedAtAsc,
        };
    }

    /// <summary>拡張子から Content-Type を解決する。</summary>
    internal static string ResolveContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".m4a" => "audio/mp4",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
}
