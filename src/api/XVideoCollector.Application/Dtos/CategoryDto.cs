namespace XVideoCollector.Application.Dtos;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
