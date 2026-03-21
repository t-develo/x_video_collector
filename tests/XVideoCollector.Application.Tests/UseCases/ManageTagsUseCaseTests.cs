using Moq;
using XVideoCollector.Application;
using XVideoCollector.Application.UseCases;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.Tests.UseCases;

public sealed class ManageTagsUseCaseTests
{
    private readonly Mock<ITagRepository> _tagRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ManageTagsUseCase _sut;

    public ManageTagsUseCaseTests()
    {
        _sut = new ManageTagsUseCase(_tagRepoMock.Object, _unitOfWorkMock.Object, TimeProvider.System);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMappedDtos()
    {
        var tags = new List<Tag>
        {
            Tag.Create("Action", TagColor.Red, TimeProvider.System),
            Tag.Create("Comedy", TagColor.Yellow, TimeProvider.System),
        };
        _tagRepoMock.Setup(r => r.GetAllAsync(default)).ReturnsAsync(tags);

        var result = await _sut.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "Action");
    }

    [Fact]
    public async Task CreateAsync_ValidInput_ReturnsDto()
    {
        var result = await _sut.CreateAsync("Drama", TagColor.Blue);

        Assert.Equal("Drama", result.Name);
        Assert.Equal(TagColor.Blue, result.Color);
        _tagRepoMock.Verify(r => r.AddAsync(It.IsAny<Tag>(), default), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NonExistingTag_ThrowsInvalidOperationException()
    {
        _tagRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Tag?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(Guid.NewGuid(), "Name", TagColor.Gray));
    }

    [Fact]
    public async Task DeleteAsync_ExistingTag_CallsRepository()
    {
        var tag = Tag.Create("ToDelete", TagColor.Pink, TimeProvider.System);
        _tagRepoMock
            .Setup(r => r.GetByIdAsync(tag.Id, default))
            .ReturnsAsync(tag);

        await _sut.DeleteAsync(tag.Id);

        _tagRepoMock.Verify(r => r.DeleteAsync(tag.Id, default), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingTag_ThrowsInvalidOperationException()
    {
        _tagRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Tag?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(Guid.NewGuid()));
    }
}
