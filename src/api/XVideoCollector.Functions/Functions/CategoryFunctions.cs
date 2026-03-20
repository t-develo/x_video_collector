using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using XVideoCollector.Application.UseCases;

namespace XVideoCollector.Functions.Functions;

public sealed class CategoryFunctions(
    ManageCategoriesUseCase manageCategories)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Function("ListCategories")]
    public async Task<IActionResult> ListCategoriesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "categories")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var categories = await manageCategories.GetAllAsync(cancellationToken);
        return new OkObjectResult(categories);
    }

    [Function("CreateCategory")]
    public async Task<IActionResult> CreateCategoryAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "categories")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<CreateCategoryRequest>(req, cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Invalid request body." });

        if (string.IsNullOrWhiteSpace(body.Name))
            return new BadRequestObjectResult(new { error = "Category name is required." });

        var category = await manageCategories.CreateAsync(body.Name, body.SortOrder, cancellationToken);
        return new CreatedAtRouteResult(routeName: null, routeValues: new { id = category.Id }, value: category);
    }

    [Function("UpdateCategory")]
    public async Task<IActionResult> UpdateCategoryAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "categories/{id:guid}")] HttpRequest req,
        Guid id,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<CreateCategoryRequest>(req, cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Invalid request body." });

        var category = await manageCategories.UpdateAsync(id, body.Name, body.SortOrder, cancellationToken);
        return new OkObjectResult(category);
    }

    [Function("DeleteCategory")]
    public async Task<IActionResult> DeleteCategoryAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "categories/{id:guid}")] HttpRequest req,
        Guid id,
        CancellationToken cancellationToken)
    {
        await manageCategories.DeleteAsync(id, cancellationToken);
        return new NoContentResult();
    }

    private static async Task<T?> ReadBodyAsync<T>(HttpRequest req, CancellationToken cancellationToken)
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

internal sealed record CreateCategoryRequest(string Name, int SortOrder = 0);
