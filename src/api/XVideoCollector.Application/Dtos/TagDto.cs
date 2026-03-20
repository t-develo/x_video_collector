using XVideoCollector.Domain.Enums;

namespace XVideoCollector.Application.Dtos;

public sealed record TagDto(
    Guid Id,
    string Name,
    TagColor Color,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
