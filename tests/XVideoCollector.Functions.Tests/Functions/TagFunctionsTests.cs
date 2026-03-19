using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Text;
using System.Text.Json;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Functions.Functions;

namespace XVideoCollector.Functions.Tests.Functions;

public sealed class TagFunctionsTests
{
    private static readonly Mock<ITagRepository> TagRepo = new();

    private static Mock<ManageTagsUseCase> DefaultMock() => new(TagRepo.Object);

    private static TagFunctions CreateSut(Mock<ManageTagsUseCase>? manage = null) =>
        new(manage?.Object ?? DefaultMock().Object);

    private static TagDto CreateTagDto(Guid? id = null) => new(
        Id: id ?? Guid.NewGuid(),
        Name: "TestTag",
        Color: TagColor.Blue,
        CreatedAt: DateTimeOffset.UtcNow);

    private static HttpRequest CreateRequest(string method = "GET", string? body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentType = "application/json";
        }
        return context.Request;
    }

    [Fact]
    public async Task ListTags_ReturnsOk()
    {
        var tags = new List<TagDto> { CreateTagDto() };
        var mock = DefaultMock();
        mock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        var result = await CreateSut(mock).ListTagsAsync(CreateRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(tags, ok.Value);
    }

    [Fact]
    public async Task CreateTag_WithValidBody_ReturnsCreated()
    {
        var tagDto = CreateTagDto();
        var body = JsonSerializer.Serialize(new { name = "NewTag", color = "Blue" });

        var mock = DefaultMock();
        mock.Setup(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<TagColor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tagDto);

        var result = await CreateSut(mock).CreateTagAsync(CreateRequest("POST", body), CancellationToken.None);

        Assert.IsType<CreatedAtRouteResult>(result);
    }

    [Fact]
    public async Task CreateTag_WithEmptyName_ReturnsBadRequest()
    {
        var body = JsonSerializer.Serialize(new { name = "", color = "Blue" });

        var result = await CreateSut().CreateTagAsync(CreateRequest("POST", body), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteTag_ReturnsNoContent()
    {
        var mock = DefaultMock();
        mock.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateSut(mock).DeleteTagAsync(CreateRequest("DELETE"), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UpdateTag_WithValidBody_ReturnsOk()
    {
        var tagId = Guid.NewGuid();
        var tagDto = CreateTagDto(tagId);
        var body = JsonSerializer.Serialize(new { name = "Updated", color = "Red" });

        var mock = DefaultMock();
        mock.Setup(x => x.UpdateAsync(tagId, It.IsAny<string>(), It.IsAny<TagColor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tagDto);

        var result = await CreateSut(mock).UpdateTagAsync(CreateRequest("PUT", body), tagId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(tagDto, ok.Value);
    }
}
