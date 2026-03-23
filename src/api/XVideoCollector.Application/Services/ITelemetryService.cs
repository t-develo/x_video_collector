namespace XVideoCollector.Application.Services;

public interface ITelemetryService
{
    void TrackDownloadSuccess(Guid videoId, TimeSpan duration, long fileSizeBytes);
    void TrackDownloadFailure(Guid videoId, string reason, TimeSpan duration);
    void TrackEvent(string eventName, IDictionary<string, string>? properties = null);
}
