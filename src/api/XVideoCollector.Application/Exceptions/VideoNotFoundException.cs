namespace XVideoCollector.Application.Exceptions;

public sealed class VideoNotFoundException(Guid videoId)
    : Exception($"Video '{videoId}' not found.")
{
    public Guid VideoId { get; } = videoId;
}
