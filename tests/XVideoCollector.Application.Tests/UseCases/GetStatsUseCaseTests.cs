using Moq;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class GetStatsUseCaseTests
{
    private readonly Mock<IVideoRepository> _videoRepoMock = new();
    private readonly GetStatsUseCase _sut;

    public GetStatsUseCaseTests()
    {
        _sut = new GetStatsUseCase(_videoRepoMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStatsReturned_MapsAllFieldsToDto()
    {
        var stats = new VideoStats(
            TotalCount: 10,
            PendingCount: 2,
            DownloadingCount: 1,
            ProcessingCount: 1,
            ReadyCount: 5,
            FailedCount: 1,
            TotalFileSizeBytes: 1024L * 1024 * 100);

        _videoRepoMock
            .Setup(r => r.GetStatsAsync(default))
            .ReturnsAsync(stats);

        var result = await _sut.ExecuteAsync();

        Assert.Equal(stats.TotalCount, result.TotalCount);
        Assert.Equal(stats.PendingCount, result.PendingCount);
        Assert.Equal(stats.DownloadingCount, result.DownloadingCount);
        Assert.Equal(stats.ProcessingCount, result.ProcessingCount);
        Assert.Equal(stats.ReadyCount, result.ReadyCount);
        Assert.Equal(stats.FailedCount, result.FailedCount);
        Assert.Equal(stats.TotalFileSizeBytes, result.TotalFileSizeBytes);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoVideos_ReturnsZeroCounts()
    {
        var stats = new VideoStats(
            TotalCount: 0,
            PendingCount: 0,
            DownloadingCount: 0,
            ProcessingCount: 0,
            ReadyCount: 0,
            FailedCount: 0,
            TotalFileSizeBytes: 0L);

        _videoRepoMock
            .Setup(r => r.GetStatsAsync(default))
            .ReturnsAsync(stats);

        var result = await _sut.ExecuteAsync();

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalFileSizeBytes);
    }
}
