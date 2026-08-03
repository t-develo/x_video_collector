using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using XVideoCollector.Application.Exceptions;
using XVideoCollector.LocalHost.Helpers;

namespace XVideoCollector.LocalHost.Middleware;

/// <summary>
/// 未処理例外を JSON エラーレスポンスに変換するミドルウェア。
/// Azure Functions 版 (<c>ExceptionMiddleware</c>) と同じマッピング・同じ本文形状を維持する。
/// </summary>
internal sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message) = ex switch
        {
            ValidationException or ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
            NotFoundException nfe => (HttpStatusCode.NotFound, nfe.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new ErrorResponse(
            (int)statusCode,
            statusCode.ToString(),
            message), RequestHelper.JsonOptions);

        await context.Response.WriteAsync(body);
    }
}

internal sealed record ErrorResponse(int Status, string Error, string Message);
