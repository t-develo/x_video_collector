namespace XVideoCollector.LocalHost.Middleware;

/// <summary>
/// /api/* に対して認証ヘッダの有無を検査するミドルウェア。
/// Azure Functions 版 (<c>AuthMiddleware</c>) と同じ判定を行う。
/// LAN 内スタンドアロン運用では SKIP_AUTH=true でバイパスするのが既定。
/// リバースプロキシ等で <c>X-MS-CLIENT-PRINCIPAL</c> を付与する構成に切り替える場合は
/// SKIP_AUTH を false にする。
/// </summary>
internal sealed class ClientPrincipalMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string ClientPrincipalHeader = "X-MS-CLIENT-PRINCIPAL";
    private const string HealthPath = "/api/health";

    private readonly bool _skipAuth = configuration.GetValue<bool>("SKIP_AUTH");

    public async Task InvokeAsync(HttpContext context)
    {
        if (_skipAuth || !RequiresAuthentication(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.ContainsKey(ClientPrincipalHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private static bool RequiresAuthentication(PathString path)
        => path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
           && !path.Equals(HealthPath, StringComparison.OrdinalIgnoreCase);
}
