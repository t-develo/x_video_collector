using XVideoCollector.Application.Interfaces;
using XVideoCollector.LocalHost.Helpers;

namespace XVideoCollector.LocalHost.Endpoints;

/// <summary>
/// カテゴリ API。Azure Functions 版 <c>CategoryFunctions</c> と同じルート・同じレスポンスを提供する。
/// </summary>
internal static class CategoryEndpoints
{
    internal static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", ListAsync);
        app.MapPost("/api/categories", CreateAsync);
        app.MapPut("/api/categories/{id:guid}", UpdateAsync);
        app.MapDelete("/api/categories/{id:guid}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(
        IManageCategoriesUseCase manageCategories,
        CancellationToken cancellationToken)
    {
        var categories = await manageCategories.GetAllAsync(cancellationToken);
        return Results.Ok(categories);
    }

    private static async Task<IResult> CreateAsync(
        HttpRequest req,
        IManageCategoriesUseCase manageCategories,
        CancellationToken cancellationToken)
    {
        var body = await RequestHelper.ReadBodyAsync<CreateCategoryRequest>(req, cancellationToken);
        if (body is null)
            return Results.BadRequest(new { error = "Invalid request body." });

        if (string.IsNullOrWhiteSpace(body.Name))
            return Results.BadRequest(new { error = "Category name is required." });

        var category = await manageCategories.CreateAsync(body.Name, body.SortOrder, cancellationToken);
        return Results.Created($"/api/categories/{category.Id}", category);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        HttpRequest req,
        IManageCategoriesUseCase manageCategories,
        CancellationToken cancellationToken)
    {
        var body = await RequestHelper.ReadBodyAsync<CreateCategoryRequest>(req, cancellationToken);
        if (body is null)
            return Results.BadRequest(new { error = "Invalid request body." });

        var category = await manageCategories.UpdateAsync(id, body.Name, body.SortOrder, cancellationToken);
        return Results.Ok(category);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        IManageCategoriesUseCase manageCategories,
        CancellationToken cancellationToken)
    {
        await manageCategories.DeleteAsync(id, cancellationToken);
        return Results.NoContent();
    }
}

internal sealed record CreateCategoryRequest(string Name, int SortOrder = 0);
