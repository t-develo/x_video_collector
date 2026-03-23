using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XVideoCollector.Functions.Helpers;

internal static class FunctionHelper
{
    /// <summary>
    /// アプリケーション共通の JSON シリアライズ設定。
    /// FunctionHelper / ExceptionMiddleware / Program.cs で共有する。
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

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
}
