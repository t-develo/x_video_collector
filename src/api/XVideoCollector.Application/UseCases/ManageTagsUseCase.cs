using XVideoCollector.Application.Dtos;
using XVideoCollector.Application.Interfaces;
using XVideoCollector.Domain.Entities;
using XVideoCollector.Domain.Enums;
using XVideoCollector.Domain.Repositories;

namespace XVideoCollector.Application.UseCases;

public sealed class ManageTagsUseCase(ITagRepository tagRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider) : IManageTagsUseCase
{
    public async Task<IReadOnlyList<TagDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var tags = await tagRepository.GetAllAsync(cancellationToken);
        return tags.Select(VideoMapper.ToDto).ToList();
    }

    public async Task<TagDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tag = await tagRepository.GetByIdAsync(id, cancellationToken);
        return tag is null ? null : VideoMapper.ToDto(tag);
    }

    public async Task<TagDto> CreateAsync(
        string name,
        TagColor color,
        CancellationToken cancellationToken = default)
    {
        var tag = Tag.Create(name, color, timeProvider);
        await tagRepository.AddAsync(tag, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return VideoMapper.ToDto(tag);
    }

    public async Task<TagDto> UpdateAsync(
        Guid id,
        string name,
        TagColor color,
        CancellationToken cancellationToken = default)
    {
        var tag = await tagRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Tag '{id}' not found.");

        tag.Update(name, color, timeProvider);
        await tagRepository.UpdateAsync(tag, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return VideoMapper.ToDto(tag);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tag = await tagRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Tag '{id}' not found.");

        await tagRepository.DeleteAsync(tag.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
