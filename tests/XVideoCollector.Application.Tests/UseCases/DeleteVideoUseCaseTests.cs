using Moq;
using XVideoCollector.Application;
using XVideoCollector.Application.Services;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;
using XVideoCollector.Domain.ValueObjects;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class DeleteVideoUseCaseTests
{
    private readonly Mock<IVideoRepository> _videoRepoMock = new();
    private readonly Mock<IVideoTagRepository> _videoTagRepoMock = new();
    private readonly Mock<IBlobStorageService> _blobMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DeleteVideoUseCase _sut;

    public DeleteVideoUseCaseTests()
    {
        _sut = new DeleteVideoUseCase(
            _videoRepoMock.Object,
            _videoTagRepoMock.Object,
            _blobMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingVideo_DeletesAll()
    {
        var video = Video.Create(
            TweetUrl.Create("https://x.com/u/status/42"),
            VideoTitle.Create("To Delete"),
            TimeProvider.System);
        _videoRepoMock
            .Setup(r => r.GetByIdAsync(video.Id, default))
            .ReturnsAsync(video);

        await _sut.ExecuteAsync(video.Id);

        _videoTagRepoMock.Verify(r => r.DeleteByVideoIdAsync(video.Id, default), Times.Once);
        _videoRepoMock.Verify(r => r.DeleteAsync(video.Id, default), Times.Once);
        _blobMock.Verify(b => b.DeleteAsync(It.IsAny<string>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistingVideo_ThrowsInvalidOperationException()
    {
        _videoRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Video?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(Guid.NewGuid()));
    }
}
