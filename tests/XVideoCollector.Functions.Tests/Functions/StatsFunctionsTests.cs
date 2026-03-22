using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Functions.Functions;

namespace XVideoCollector.Functions.Tests.Functions;

public sealed class StatsFunctionsTests
{
    private static HttpRequest CreateRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        return context.Request;
    }

    [Fact]
    public async Task GetStatsAsync_WhenCalled_ReturnsOkWithStats()
    {
        var expected = new VideoStatsDto(
            TotalCount: 10,
            PendingCount: 2,
            DownloadingCount: 1,
            ProcessingCount: 1,
            ReadyCount: 5,
            FailedCount: 1,
            TotalFileSizeBytes: 1024 * 1024 * 500L);

        var mock = new Mock<IGetStatsUseCase>();
        mock.Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new StatsFunctions(mock.Object);

        var result = await sut.GetStatsAsync(CreateRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expected, ok.Value);
    }
}
