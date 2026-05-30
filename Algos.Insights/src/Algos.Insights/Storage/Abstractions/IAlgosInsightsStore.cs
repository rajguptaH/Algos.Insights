using Algos.Insights.Models;

namespace Algos.Insights.Storage.Abstractions;

public interface IAlgosInsightsStore
{
    Task SaveRequestAsync(AlgosRequestLog log, CancellationToken cancellationToken = default);
    Task SaveExceptionAsync(AlgosExceptionLog log, CancellationToken cancellationToken = default);
    Task SaveEventAsync(AlgosEventLog log, CancellationToken cancellationToken = default);
    Task SaveMetricAsync(AlgosMetricLog log, CancellationToken cancellationToken = default);
    Task SaveTraceAsync(AlgosTraceLog log, CancellationToken cancellationToken = default);
    Task SaveFeatureUsageAsync(AlgosFeatureUsageLog log, CancellationToken cancellationToken = default);
    Task SaveDependencyAsync(AlgosDependencyLog log, CancellationToken cancellationToken = default);
    Task<PagedResult<AlgosRequestLog>> GetRequestsAsync(AlgosQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<AlgosExceptionLog>> GetExceptionsAsync(AlgosQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<AlgosEventLog>> GetEventsAsync(AlgosQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<AlgosMetricLog>> GetMetricsAsync(AlgosQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<AlgosFeatureUsageLog>> GetFeatureUsageAsync(AlgosQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<AlgosDependencyLog>> GetDependenciesAsync(AlgosQuery query, CancellationToken cancellationToken = default);
    Task<AlgosTraceTree?> GetTraceTreeAsync(string traceId, CancellationToken cancellationToken = default);
}
