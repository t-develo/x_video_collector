using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using XVideoCollector.Application.Exceptions;
using XVideoCollector.Functions.Helpers;

namespace XVideoCollector.Functions.Middleware;

internal sealed class ExceptionMiddleware(ILogger<ExceptionMiddleware> logger) : IFunctionsWorkerMiddleware
{

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in function {FunctionName}", context.FunctionDefinition.Name);

            var httpContext = context.GetHttpContext();
            if (httpContext is null)
                throw;

            await WriteErrorResponseAsync(httpContext, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext httpContext, Exception ex)
    {
        var (statusCode, message) = ex switch
        {
            ValidationException or ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
            NotFoundException nfe => (HttpStatusCode.NotFound, nfe.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new ErrorResponse(
            (int)statusCode,
            statusCode.ToString(),
            message), FunctionHelper.JsonOptions);

        await httpContext.Response.WriteAsync(body);
    }
}

internal sealed record ErrorResponse(int Status, string Error, string Message);
