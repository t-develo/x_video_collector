namespace XVideoCollector.Application.Dtos;

public sealed record UpdateVideoRequest(
    Guid Id,
    string Title,
    Guid? CategoryId,
    IReadOnlyList<Guid> TagIds);
