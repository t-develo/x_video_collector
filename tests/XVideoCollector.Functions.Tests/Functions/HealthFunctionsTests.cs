using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Services;
using XVideoCollector.Functions.Functions;

namespace XVideoCollector.Functions.Tests.Functions;

public sealed class HealthFunctionsTests
{
    private static HttpRequest CreateRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        return context.Request;
    }

    private static HealthCheckResult CreateHealthyResult() =>
        new(
            Status: "Healthy",
            Checks: new Dictionary<string, HealthCheckEntry>
            {
                ["sql"] = new HealthCheckEntry("Healthy", null, 10),
                ["blob"] = new HealthCheckEntry("Healthy", null, 5),
                ["ytdlp"] = new HealthCheckEntry("Healthy", "yt-dlp.exe", 1),
                ["ffmpeg"] = new HealthCheckEntry("Healthy", "ffmpeg.exe", 1),
                ["ffprobe"] = new HealthCheckEntry("Healthy", "ffprobe.exe", 1),
            },
            Timestamp: DateTimeOffset.UtcNow);

    private static HealthCheckResult CreateUnhealthyResult() =>
        new(
            Status: "Unhealthy",
            Checks: new Dictionary<string, HealthCheckEntry>
            {
                ["sql"] = new HealthCheckEntry("Unhealthy", "Cannot connect to SQL Database.", 100),
                ["blob"] = new HealthCheckEntry("Healthy", null, 5),
                ["ytdlp"] = new HealthCheckEntry("Healthy", "yt-dlp.exe", 1),
                ["ffmpeg"] = new HealthCheckEntry("Healthy", "ffmpeg.exe", 1),
                ["ffprobe"] = new HealthCheckEntry("Healthy", "ffprobe.exe", 1),
            },
            Timestamp: DateTimeOffset.UtcNow);

    [Fact]
    public async Task CheckAsync_WhenAllHealthy_Returns200WithHealthyStatus()
    {
        var expected = CreateHealthyResult();
        var mock = new Mock<IHealthCheckService>();
        mock.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new HealthFunctions(mock.Object);

        var result = await sut.CheckAsync(CreateRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(expected, objectResult.Value);
    }

    [Fact]
    public async Task CheckAsync_WhenUnhealthy_Returns503WithUnhealthyStatus()
    {
        var expected = CreateUnhealthyResult();
        var mock = new Mock<IHealthCheckService>();
        mock.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new HealthFunctions(mock.Object);

        var result = await sut.CheckAsync(CreateRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
        Assert.Equal(expected, objectResult.Value);
    }
}
