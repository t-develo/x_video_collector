using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Functions.Functions;

public sealed class TagFunctions(
    ManageTagsUseCase manageTags)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Function("ListTags")]
    public async Task<IActionResult> ListTagsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tags")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var tags = await manageTags.GetAllAsync(cancellationToken);
        return new OkObjectResult(tags);
    }

    [Function("CreateTag")]
    public async Task<IActionResult> CreateTagAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tags")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<CreateTagRequest>(req, cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Invalid request body." });

        if (string.IsNullOrWhiteSpace(body.Name))
            return new BadRequestObjectResult(new { error = "Tag name is required." });

        var tag = await manageTags.CreateAsync(body.Name, body.Color, cancellationToken);
        return new CreatedAtRouteResult(routeName: null, routeValues: new { id = tag.Id }, value: tag);
    }

    [Function("UpdateTag")]
    public async Task<IActionResult> UpdateTagAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tags/{id:guid}")] HttpRequest req,
        Guid id,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<CreateTagRequest>(req, cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Invalid request body." });

        var tag = await manageTags.UpdateAsync(id, body.Name, body.Color, cancellationToken);
        return new OkObjectResult(tag);
    }

    [Function("DeleteTag")]
    public async Task<IActionResult> DeleteTagAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tags/{id:guid}")] HttpRequest req,
        Guid id,
        CancellationToken cancellationToken)
    {
        await manageTags.DeleteAsync(id, cancellationToken);
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

internal sealed record CreateTagRequest(string Name, TagColor Color);
