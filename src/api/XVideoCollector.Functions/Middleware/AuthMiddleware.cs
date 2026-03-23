using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace XVideoCollector.Functions.Middleware;

internal sealed class AuthMiddleware(
    IConfiguration configuration,
    ILogger<AuthMiddleware> logger) : IFunctionsWorkerMiddleware
{
    private const string ClientPrincipalHeader = "X-MS-CLIENT-PRINCIPAL";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // HTTP Trigger 以外（Queue Trigger 等）はスキップ
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        // ヘルスチェックエンドポイントは認証をスキップ
        var path = httpContext.Request.Path.Value ?? string.Empty;
        if (path.Equals("/api/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // 開発環境では認証をスキップ
        if (configuration.GetValue<bool>("SKIP_AUTH"))
        {
            await next(context);
            return;
        }

        // X-MS-CLIENT-PRINCIPAL ヘッダーを検証
        if (!httpContext.Request.Headers.ContainsKey(ClientPrincipalHeader))
        {
            logger.LogWarning("Unauthorized request: {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

            httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync("{\"error\":\"Authentication required.\"}");
            return;
        }

        await next(context);
    }
}
