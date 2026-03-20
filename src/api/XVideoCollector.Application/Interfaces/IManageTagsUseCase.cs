using XVideoCollector.Application.Dtos;
using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Application.Interfaces;

public interface IManageTagsUseCase
{
    Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TagDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TagDto> CreateAsync(string name, TagColor color, CancellationToken cancellationToken = default);
    Task<TagDto> UpdateAsync(Guid id, string name, TagColor color, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
