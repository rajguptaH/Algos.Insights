using Algos.Insights.Models;
using Algos.Insights.Options;
using Algos.Insights.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace Algos.Insights.Storage.InMemory;

public sealed class InMemoryAlgosInsightsStore : IAlgosInsightsStore
{
    private readonly BoundedBuffer<AlgosRequestLog> _requests;
    private readonly BoundedBuffer<AlgosExceptionLog> _exceptions;
    private readonly BoundedBuffer<AlgosEventLog> _events;
    private readonly BoundedBuffer<AlgosMetricLog> _metrics;
    private readonly BoundedBuffer<AlgosTraceLog> _traces;
    private readonly BoundedBuffer<AlgosFeatureUsageLog> _features;
    private readonly BoundedBuffer<AlgosDependencyLog> _dependencies;
    private readonly TimeSpan _retention;

    public InMemoryAlgosInsightsStore(IOptions<AlgosInMemoryStorageOptions> options)
    {
        var value = options.Value;
        _retention = TimeSpan.FromHours(value.RetentionHours);
        _requests = new(value.MaxRequestLogs, x => x.TimestampUtc);
        _exceptions = new(value.MaxExceptionLogs, x => x.TimestampUtc);
        _events = new(value.MaxEventLogs, x => x.TimestampUtc);
        _metrics = new(value.MaxMetricLogs, x => x.TimestampUtc);
        _traces = new(value.MaxTraceLogs, x => x.TimestampUtc);
        _features = new(value.MaxFeatureUsageLogs, x => x.TimestampUtc);
        _dependencies = new(value.MaxTraceLogs, x => x.TimestampUtc);
    }

    public Task SaveRequestAsync(AlgosRequestLog log, CancellationToken cancellationToken = default) { _requests.Add(log); return Task.CompletedTask; }
    public Task SaveExceptionAsync(AlgosExceptionLog log, CancellationToken cancellationToken = default) { _exceptions.Add(log); return Task.CompletedTask; }
    public Task SaveEventAsync(AlgosEventLog log, CancellationToken cancellationToken = default) { _events.Add(log); return Task.CompletedTask; }
    public Task SaveMetricAsync(AlgosMetricLog log, CancellationToken cancellationToken = default) { _metrics.Add(log); return Task.CompletedTask; }
    public Task SaveTraceAsync(AlgosTraceLog log, CancellationToken cancellationToken = default) { _traces.Add(log); return Task.CompletedTask; }
    public Task SaveFeatureUsageAsync(AlgosFeatureUsageLog log, CancellationToken cancellationToken = default) { _features.Add(log); return Task.CompletedTask; }
    public Task SaveDependencyAsync(AlgosDependencyLog log, CancellationToken cancellationToken = default) { _dependencies.Add(log); return Task.CompletedTask; }

    public Task<PagedResult<AlgosRequestLog>> GetRequestsAsync(AlgosQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Page(Filter(_requests.Snapshot(_retention), query, x => $"{x.Method} {x.Path} {x.StatusCode} {x.Message}"), query));

    public Task<PagedResult<AlgosExceptionLog>> GetExceptionsAsync(AlgosQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Page(Filter(_exceptions.Snapshot(_retention), query, x => $"{x.ExceptionType} {x.Message}"), query));

    public Task<PagedResult<AlgosEventLog>> GetEventsAsync(AlgosQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Page(Filter(_events.Snapshot(_retention), query, x => $"{x.EventName} {x.Message}"), query));

    public Task<PagedResult<AlgosMetricLog>> GetMetricsAsync(AlgosQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Page(Filter(_metrics.Snapshot(_retention), query, x => $"{x.Name} {x.Value}"), query));

    public Task<PagedResult<AlgosFeatureUsageLog>> GetFeatureUsageAsync(AlgosQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Page(Filter(_features.Snapshot(_retention), query, x => $"{x.ModuleName} {x.FeatureName} {x.UserId}"), query));

    public Task<PagedResult<AlgosDependencyLog>> GetDependenciesAsync(AlgosQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Page(Filter(_dependencies.Snapshot(_retention), query, x => $"{x.DependencyName} {x.OperationName}"), query));

    public Task<AlgosTraceTree?> GetTraceTreeAsync(string traceId, CancellationToken cancellationToken = default)
    {
        var spans = _traces.Snapshot(_retention)
            .Where(x => string.Equals(x.TraceId, traceId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.TimestampUtc)
            .ToArray();

        if (spans.Length == 0)
        {
            return Task.FromResult<AlgosTraceTree?>(null);
        }

        var children = spans.GroupBy(x => x.ParentSpanId ?? "")
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);

        AlgosTraceNode Node(AlgosTraceLog span) =>
            new(span, children.TryGetValue(span.SpanId ?? "", out var nodes) ? nodes.Select(Node).ToArray() : []);

        var roots = spans.Where(x => string.IsNullOrEmpty(x.ParentSpanId) || !spans.Any(p => p.SpanId == x.ParentSpanId)).Select(Node).ToArray();
        return Task.FromResult<AlgosTraceTree?>(new AlgosTraceTree(traceId, roots));
    }

    private static IEnumerable<T> Filter<T>(IEnumerable<T> source, AlgosQuery query, Func<T, string> text) where T : AlgosLogBase
    {
        if (query.FromUtc.HasValue) source = source.Where(x => x.TimestampUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) source = source.Where(x => x.TimestampUtc <= query.ToUtc.Value);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => text(x).Contains(query.Search, StringComparison.OrdinalIgnoreCase));
        return source.OrderByDescending(x => x.TimestampUtc);
    }

    private static PagedResult<T> Page<T>(IEnumerable<T> source, AlgosQuery query)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 500);
        var list = source.ToArray();
        return new PagedResult<T>(list.Skip((page - 1) * size).Take(size).ToArray(), list.Length, page, size);
    }
}
