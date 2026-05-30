using System.Diagnostics;
using Algos.Insights.Models;

namespace Algos.Insights.Logging;

public interface IAlgosInsightsLogger
{
    IDisposable BeginScope(string name, object? properties = null);
    void TrackEvent(string eventName, object? properties = null);
    void TrackMetric(string name, double value, object? properties = null);
    void TrackFeatureUsage(string module, string feature, object? properties = null);
    void TrackException(Exception exception, object? properties = null);
    Activity? StartActivity(string operationName, object? properties = null);
    Task<T> TrackDependencyAsync<T>(string dependencyName, string operationName, Func<Task<T>> operation);
    Task TrackDependencyAsync(string dependencyName, string operationName, Func<Task> operation);
}

public interface IAlgosTelemetryProvider
{
    Task TrackRequestAsync(AlgosRequestLog log, CancellationToken cancellationToken = default);
    Task TrackExceptionAsync(AlgosExceptionLog log, CancellationToken cancellationToken = default);
    Task TrackEventAsync(AlgosEventLog log, CancellationToken cancellationToken = default);
    Task TrackMetricAsync(AlgosMetricLog log, CancellationToken cancellationToken = default);
    Task TrackTraceAsync(AlgosTraceLog log, CancellationToken cancellationToken = default);
    Task TrackFeatureUsageAsync(AlgosFeatureUsageLog log, CancellationToken cancellationToken = default);
}
