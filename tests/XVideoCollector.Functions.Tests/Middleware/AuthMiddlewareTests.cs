using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using XVideoCollector.Functions.Middleware;

namespace XVideoCollector.Functions.Tests.Middleware;

public sealed class AuthMiddlewareTests
{
    private const string HttpContextKey = "HttpRequestContext";

    private static (Mock<FunctionContext>, DefaultHttpContext) CreateFunctionContextWithHttp()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var items = new Dictionary<object, object> { [HttpContextKey] = httpContext };

        var contextMock = new Mock<FunctionContext>();
        contextMock.Setup(c => c.Items).Returns(items);

        return (contextMock, httpContext);
    }

    [Fact]
    public async Task Invoke_WithoutAuthHeader_Returns401AndDoesNotCallNext()
    {
        var config = new ConfigurationBuilder().Build();
        var sut = new AuthMiddleware(config, NullLogger<AuthMiddleware>.Instance);
        var (contextMock, httpContext) = CreateFunctionContextWithHttp();

        var nextCalled = false;
        Task Next(FunctionContext _) { nextCalled = true; return Task.CompletedTask; }

        await sut.Invoke(contextMock.Object, Next);

        Assert.False(nextCalled);
        Assert.Equal(401, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_WithAuthHeader_CallsNext()
    {
        var config = new ConfigurationBuilder().Build();
        var sut = new AuthMiddleware(config, NullLogger<AuthMiddleware>.Instance);
        var (contextMock, httpContext) = CreateFunctionContextWithHttp();
        httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = "some-principal-value";

        var nextCalled = false;
        Task Next(FunctionContext _) { nextCalled = true; return Task.CompletedTask; }

        await sut.Invoke(contextMock.Object, Next);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Invoke_SkipAuthEnabled_CallsNextEvenWithoutHeader()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SKIP_AUTH"] = "true" })
            .Build();
        var sut = new AuthMiddleware(config, NullLogger<AuthMiddleware>.Instance);
        var (contextMock, _) = CreateFunctionContextWithHttp();

        var nextCalled = false;
        Task Next(FunctionContext _) { nextCalled = true; return Task.CompletedTask; }

        await sut.Invoke(contextMock.Object, Next);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Invoke_NullHttpContext_CallsNext()
    {
        var config = new ConfigurationBuilder().Build();
        var sut = new AuthMiddleware(config, NullLogger<AuthMiddleware>.Instance);

        // Empty items → GetHttpContext() returns null
        var items = new Dictionary<object, object>();
        var contextMock = new Mock<FunctionContext>();
        contextMock.Setup(c => c.Items).Returns(items);

        var nextCalled = false;
        Task Next(FunctionContext _) { nextCalled = true; return Task.CompletedTask; }

        await sut.Invoke(contextMock.Object, Next);

        Assert.True(nextCalled);
    }
}
