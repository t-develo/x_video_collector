using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.ComponentModel.DataAnnotations;
using XVideoCollector.Application.Exceptions;
using XVideoCollector.Functions.Middleware;

namespace XVideoCollector.Functions.Tests.Middleware;

public sealed class ExceptionMiddlewareTests
{
    private const string HttpContextKey = "HttpRequestContext";

    private static (Mock<FunctionContext>, DefaultHttpContext) CreateFunctionContextWithHttp()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var items = new Dictionary<object, object> { [HttpContextKey] = httpContext };

        var functionDefinition = new Mock<FunctionDefinition>();
        functionDefinition.Setup(f => f.Name).Returns("TestFunction");

        var contextMock = new Mock<FunctionContext>();
        contextMock.Setup(c => c.Items).Returns(items);
        contextMock.Setup(c => c.FunctionDefinition).Returns(functionDefinition.Object);

        return (contextMock, httpContext);
    }

    [Fact]
    public async Task Invoke_NoException_CallsNext()
    {
        var sut = new ExceptionMiddleware(NullLogger<ExceptionMiddleware>.Instance);
        var (contextMock, _) = CreateFunctionContextWithHttp();

        var nextCalled = false;
        await sut.Invoke(contextMock.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Invoke_NotFoundException_Returns404()
    {
        var sut = new ExceptionMiddleware(NullLogger<ExceptionMiddleware>.Instance);
        var (contextMock, httpContext) = CreateFunctionContextWithHttp();

        await sut.Invoke(contextMock.Object, _ => throw new VideoNotFoundException(Guid.NewGuid()));

        Assert.Equal(404, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_ValidationException_Returns400()
    {
        var sut = new ExceptionMiddleware(NullLogger<ExceptionMiddleware>.Instance);
        var (contextMock, httpContext) = CreateFunctionContextWithHttp();

        await sut.Invoke(contextMock.Object, _ => throw new ValidationException("invalid input"));

        Assert.Equal(400, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_ArgumentException_Returns400()
    {
        var sut = new ExceptionMiddleware(NullLogger<ExceptionMiddleware>.Instance);
        var (contextMock, httpContext) = CreateFunctionContextWithHttp();

        await sut.Invoke(contextMock.Object, _ => throw new ArgumentException("bad argument"));

        Assert.Equal(400, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_UnhandledException_Returns500()
    {
        var sut = new ExceptionMiddleware(NullLogger<ExceptionMiddleware>.Instance);
        var (contextMock, httpContext) = CreateFunctionContextWithHttp();

        await sut.Invoke(contextMock.Object, _ => throw new InvalidOperationException("unexpected"));

        Assert.Equal(500, httpContext.Response.StatusCode);
    }
}
