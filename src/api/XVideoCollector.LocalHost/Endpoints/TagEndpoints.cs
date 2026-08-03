using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Enums;
using XVideoCollector.LocalHost.Helpers;

namespace XVideoCollector.LocalHost.Endpoints;

/// <summary>
/// タグ API。Azure Functions 版 <c>TagFunctions</c> と同じルート・同じレスポンスを提供する。
/// </summary>
internal static class TagEndpoints
{
    internal static void MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tags", ListAsync);
        app.MapPost("/api/tags", CreateAsync);
        app.MapPut("/api/tags/{id:guid}", UpdateAsync);
        app.MapDelete("/api/tags/{id:guid}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(
        IManageTagsUseCase manageTags,
        CancellationToken cancellationToken)
    {
        var tags = await manageTags.GetAllAsync(cancellationToken);
        return Results.Ok(tags);
    }

    private static async Task<IResult> CreateAsync(
        HttpRequest req,
        IManageTagsUseCase manageTags,
        CancellationToken cancellationToken)
    {
        var body = await RequestHelper.ReadBodyAsync<CreateTagRequest>(req, cancellationToken);
        if (body is null)
            return Results.BadRequest(new { error = "Invalid request body." });

        if (string.IsNullOrWhiteSpace(body.Name))
            return Results.BadRequest(new { error = "Tag name is required." });

        var tag = await manageTags.CreateAsync(body.Name, body.Color, cancellationToken);
        return Results.Created($"/api/tags/{tag.Id}", tag);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        HttpRequest req,
        IManageTagsUseCase manageTags,
        CancellationToken cancellationToken)
    {
        var body = await RequestHelper.ReadBodyAsync<CreateTagRequest>(req, cancellationToken);
        if (body is null)
            return Results.BadRequest(new { error = "Invalid request body." });

        var tag = await manageTags.UpdateAsync(id, body.Name, body.Color, cancellationToken);
        return Results.Ok(tag);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        IManageTagsUseCase manageTags,
        CancellationToken cancellationToken)
    {
        await manageTags.DeleteAsync(id, cancellationToken);
        return Results.NoContent();
    }
}

internal sealed record CreateTagRequest(string Name, TagColor Color);
