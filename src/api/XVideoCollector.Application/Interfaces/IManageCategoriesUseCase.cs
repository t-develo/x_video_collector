using XVideoCollector.Application.Dtos;

namespace XVideoCollector.Application.Interfaces;

public interface IManageCategoriesUseCase
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateAsync(string name, int sortOrder = 0, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateAsync(Guid id, string name, int sortOrder, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
