using XVideoCollector.Application.Dtos;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public class ManageTagsUseCase(ITagRepository tagRepository)
{
    public virtual async Task<IReadOnlyList<TagDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var tags = await tagRepository.GetAllAsync(cancellationToken);
        return tags.Select(VideoMapper.ToDto).ToList();
    }

    public virtual async Task<TagDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tag = await tagRepository.GetByIdAsync(id, cancellationToken);
        return tag is null ? null : VideoMapper.ToDto(tag);
    }

    public virtual async Task<TagDto> CreateAsync(
        string name,
        TagColor color,
        CancellationToken cancellationToken = default)
    {
        var tag = Tag.Create(name, color);
        await tagRepository.AddAsync(tag, cancellationToken);
        return VideoMapper.ToDto(tag);
    }

    public virtual async Task<TagDto> UpdateAsync(
        Guid id,
        string name,
        TagColor color,
        CancellationToken cancellationToken = default)
    {
        var tag = await tagRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Tag '{id}' not found.");

        tag.Update(name, color);
        await tagRepository.UpdateAsync(tag, cancellationToken);
        return VideoMapper.ToDto(tag);
    }

    public virtual async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tag = await tagRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Tag '{id}' not found.");

        await tagRepository.DeleteAsync(tag.Id, cancellationToken);
    }
}
