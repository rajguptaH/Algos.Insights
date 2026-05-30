using System.Text.Json;
using Algos.Insights.Models;
using Algos.Insights.Options;
using Algos.Insights.Storage.Abstractions;
using Algos.Insights.Storage.InMemory;
using Microsoft.Extensions.Options;

namespace Algos.Insights.Storage.JsonFile;

public sealed class JsonFileAlgosInsightsStore : IAlgosInsightsStore
{
    private readonly InMemoryAlgosInsightsStore _inner;
    private readonly string _directory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JsonFileAlgosInsightsStore(IOptions<AlgosJsonFileStorageOptions> jsonOptions, IOptions<AlgosInMemoryStorageOptions> memoryOptions)
    {
        _inner = new InMemoryAlgosInsightsStore(memoryOptions);
        _directory = Path.GetFullPath(jsonOptions.Value.DirectoryPath);
        Directory.CreateDirectory(_directory);
    }

    public async Task SaveRequestAsync(AlgosRequestLog log, CancellationToken cancellationToken = default) { await _inner.SaveRequestAsync(log, cancellationToken); await AppendAsync("requests.ndjson", log, cancellationToken); }
    public async Task SaveExceptionAsync(AlgosExceptionLog log, CancellationToken cancellationToken = default) { await _inner.SaveExceptionAsync(log, cancellationToken); await AppendAsync("exceptions.ndjson", log, cancellationToken); }
    public async Task SaveEventAsync(AlgosEventLog log, CancellationToken cancellationToken = default) { await _inner.SaveEventAsync(log, cancellationToken); await AppendAsync("events.ndjson", log, cancellationToken); }
    public async Task SaveMetricAsync(AlgosMetricLog log, CancellationToken cancellationToken = default) { await _inner.SaveMetricAsync(log, cancellationToken); await AppendAsync("metrics.ndjson", log, cancellationToken); }
    public async Task SaveTraceAsync(AlgosTraceLog log, CancellationToken cancellationToken = default) { await _inner.SaveTraceAsync(log, cancellationToken); await AppendAsync("traces.ndjson", log, cancellationToken); }
    public async Task SaveFeatureUsageAsync(AlgosFeatureUsageLog log, CancellationToken cancellationToken = default) { await _inner.SaveFeatureUsageAsync(log, cancellationToken); await AppendAsync("features.ndjson", log, cancellationToken); }
    public async Task SaveDependencyAsync(AlgosDependencyLog log, CancellationToken cancellationToken = default) { await _inner.SaveDependencyAsync(log, cancellationToken); await AppendAsync("dependencies.ndjson", log, cancellationToken); }
    public Task<PagedResult<AlgosRequestLog>> GetRequestsAsync(AlgosQuery query, CancellationToken cancellationToken = default) => _inner.GetRequestsAsync(query, cancellationToken);
    public Task<PagedResult<AlgosExceptionLog>> GetExceptionsAsync(AlgosQuery query, CancellationToken cancellationToken = default) => _inner.GetExceptionsAsync(query, cancellationToken);
    public Task<PagedResult<AlgosEventLog>> GetEventsAsync(AlgosQuery query, CancellationToken cancellationToken = default) => _inner.GetEventsAsync(query, cancellationToken);
    public Task<PagedResult<AlgosMetricLog>> GetMetricsAsync(AlgosQuery query, CancellationToken cancellationToken = default) => _inner.GetMetricsAsync(query, cancellationToken);
    public Task<PagedResult<AlgosFeatureUsageLog>> GetFeatureUsageAsync(AlgosQuery query, CancellationToken cancellationToken = default) => _inner.GetFeatureUsageAsync(query, cancellationToken);
    public Task<PagedResult<AlgosDependencyLog>> GetDependenciesAsync(AlgosQuery query, CancellationToken cancellationToken = default) => _inner.GetDependenciesAsync(query, cancellationToken);
    public Task<AlgosTraceTree?> GetTraceTreeAsync(string traceId, CancellationToken cancellationToken = default) => _inner.GetTraceTreeAsync(traceId, cancellationToken);

    private async Task AppendAsync<T>(string fileName, T value, CancellationToken cancellationToken)
    {
        var line = JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(Path.Combine(_directory, fileName), line, cancellationToken);
    }
}
