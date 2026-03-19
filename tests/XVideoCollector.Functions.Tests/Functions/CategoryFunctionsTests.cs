using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;
using System.Text.Json;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Functions.Functions;

namespace XVideoCollector.Functions.Tests.Functions;

public sealed class CategoryFunctionsTests
{
    private static readonly Mock<ICategoryRepository> CategoryRepo = new();

    private static Mock<ManageCategoriesUseCase> DefaultMock() => new(CategoryRepo.Object);

    private static CategoryFunctions CreateSut(Mock<ManageCategoriesUseCase>? manage = null) =>
        new(manage?.Object ?? DefaultMock().Object, NullLogger<CategoryFunctions>.Instance);

    private static CategoryDto CreateCategoryDto(Guid? id = null) => new(
        Id: id ?? Guid.NewGuid(),
        Name: "TestCategory",
        SortOrder: 0,
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
    public async Task ListCategories_ReturnsOk()
    {
        var categories = new List<CategoryDto> { CreateCategoryDto() };
        var mock = DefaultMock();
        mock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        var result = await CreateSut(mock).ListCategoriesAsync(CreateRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(categories, ok.Value);
    }

    [Fact]
    public async Task CreateCategory_WithValidBody_ReturnsCreated()
    {
        var categoryDto = CreateCategoryDto();
        var body = JsonSerializer.Serialize(new { name = "NewCategory", sortOrder = 1 });

        var mock = DefaultMock();
        mock.Setup(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(categoryDto);

        var result = await CreateSut(mock).CreateCategoryAsync(CreateRequest("POST", body), CancellationToken.None);

        Assert.IsType<CreatedAtRouteResult>(result);
    }

    [Fact]
    public async Task CreateCategory_WithEmptyName_ReturnsBadRequest()
    {
        var body = JsonSerializer.Serialize(new { name = "", sortOrder = 0 });

        var result = await CreateSut().CreateCategoryAsync(CreateRequest("POST", body), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateCategory_WithValidBody_ReturnsOk()
    {
        var categoryId = Guid.NewGuid();
        var categoryDto = CreateCategoryDto(categoryId);
        var body = JsonSerializer.Serialize(new { name = "Updated", sortOrder = 2 });

        var mock = DefaultMock();
        mock.Setup(x => x.UpdateAsync(categoryId, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(categoryDto);

        var result = await CreateSut(mock).UpdateCategoryAsync(CreateRequest("PUT", body), categoryId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(categoryDto, ok.Value);
    }

    [Fact]
    public async Task DeleteCategory_ReturnsNoContent()
    {
        var mock = DefaultMock();
        mock.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateSut(mock).DeleteCategoryAsync(CreateRequest("DELETE"), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }
}
