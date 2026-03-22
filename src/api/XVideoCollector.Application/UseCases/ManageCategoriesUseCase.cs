using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public sealed class ManageCategoriesUseCase(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider) : IManageCategoriesUseCase
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        return categories.Select(VideoMapper.ToDto).ToList();
    }

    public async Task<CategoryDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(id, cancellationToken);
        return category is null ? null : VideoMapper.ToDto(category);
    }

    public async Task<CategoryDto> CreateAsync(
        string name,
        int sortOrder = 0,
        CancellationToken cancellationToken = default)
    {
        var category = Category.Create(name, sortOrder, timeProvider);
        await categoryRepository.AddAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return VideoMapper.ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(
        Guid id,
        string name,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Category '{id}' not found.");

        category.Update(name, sortOrder, timeProvider);
        await categoryRepository.UpdateAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return VideoMapper.ToDto(category);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Category '{id}' not found.");

        await categoryRepository.DeleteAsync(category.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
