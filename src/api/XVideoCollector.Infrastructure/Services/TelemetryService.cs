using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using XVideoCollector.Application.Services;

namespace XVideoCollector.Infrastructure.Services;

internal sealed class TelemetryService(TelemetryClient telemetryClient) : ITelemetryService
{
    public void TrackDownloadSuccess(Guid videoId, TimeSpan duration, long fileSizeBytes)
    {
        var properties = new Dictionary<string, string>
        {
            ["VideoId"] = videoId.ToString(),
            ["FileSizeBytes"] = fileSizeBytes.ToString(),
        };

        var metrics = new Dictionary<string, double>
        {
            ["DurationSeconds"] = duration.TotalSeconds,
            ["FileSizeMB"] = fileSizeBytes / (1024.0 * 1024.0),
        };

        telemetryClient.TrackEvent("VideoDownloadSuccess", properties, metrics);

        telemetryClient.GetMetric("VideoDownload.DurationSeconds", "Outcome")
            .TrackValue(duration.TotalSeconds, "Success");
    }

    public void TrackDownloadFailure(Guid videoId, string reason, TimeSpan duration)
    {
        var properties = new Dictionary<string, string>
        {
            ["VideoId"] = videoId.ToString(),
            ["FailureReason"] = reason,
        };

        var metrics = new Dictionary<string, double>
        {
            ["DurationSeconds"] = duration.TotalSeconds,
        };

        telemetryClient.TrackEvent("VideoDownloadFailure", properties, metrics);

        telemetryClient.GetMetric("VideoDownload.DurationSeconds", "Outcome")
            .TrackValue(duration.TotalSeconds, "Failure");
    }

    public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
    {
        telemetryClient.TrackEvent(eventName, properties);
    }
}
