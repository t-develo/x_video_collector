using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Application.Services;

namespace XVideoCollector.LocalHost.Endpoints;

/// <summary>
/// 統計・ヘルスチェック・認証情報スタブ。
/// </summary>
internal static class SystemEndpoints
{
    internal static void MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stats", GetStatsAsync);
        app.MapGet("/api/health", CheckHealthAsync);
        app.MapGet("/.auth/me", GetClientPrincipal);
    }

    private static async Task<IResult> GetStatsAsync(
        IGetStatsUseCase getStats,
        CancellationToken cancellationToken)
    {
        var stats = await getStats.ExecuteAsync(cancellationToken);
        return Results.Ok(stats);
    }

    private static async Task<IResult> CheckHealthAsync(
        IHealthCheckService healthCheck,
        CancellationToken cancellationToken)
    {
        var result = await healthCheck.CheckAsync(cancellationToken);
        var statusCode = result.Status == HealthStatus.Healthy ? 200 : 503;

        return Results.Json(result, statusCode: statusCode);
    }

    /// <summary>
    /// Static Web Apps の /.auth/me 互換スタブ。
    /// フロントエンド (components/header.js) がユーザー表示のために参照するため、
    /// スタンドアロン運用でも同じ形状のレスポンスを返す。
    /// </summary>
    private static IResult GetClientPrincipal()
        => Results.Ok(new
        {
            clientPrincipal = new
            {
                identityProvider = "local",
                userId = "local",
                userDetails = "ローカル",
                userRoles = new[] { "authenticated" },
            }
        });
}
