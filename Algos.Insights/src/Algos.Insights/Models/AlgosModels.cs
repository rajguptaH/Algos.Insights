using System.Collections.Concurrent;

namespace Algos.Insights.Models;

public abstract record AlgosLogBase
{
    public string Id { get; init; } = Guid.NewGuid().ToString("n");
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? ApplicationName { get; init; }
    public string? Environment { get; init; }
    public AlgosSeverity Severity { get; init; } = AlgosSeverity.Information;
    public string? Category { get; init; }
    public string? Module { get; init; }
    public string? Feature { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, object?> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? CorrelationId { get; init; }
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
}

public record AlgosLogEntry : AlgosLogBase;

public record AlgosRequestLog : AlgosLogBase
{
    public string Method { get; init; } = "";
    public string Path { get; init; } = "";
    public string? QueryString { get; init; }
    public int StatusCode { get; init; }
    public long DurationMs { get; init; }
    public long? RequestSize { get; init; }
    public long? ResponseSize { get; init; }
    public string? ClientIp { get; init; }
    public string? UserAgent { get; init; }
    public string? RouteName { get; init; }
    public string? Controller { get; init; }
    public string? Action { get; init; }
    public Dictionary<string, string> RequestHeaders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ResponseHeaders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? RequestBody { get; init; }
    public string? ResponseBody { get; init; }
}

public record AlgosExceptionLog : AlgosLogBase
{
    public string ExceptionType { get; init; } = "";
    public string? StackTrace { get; init; }
    public string? RequestId { get; init; }
}

public record AlgosEventLog : AlgosLogBase
{
    public string EventName { get; init; } = "";
}

public record AlgosAuditLog : AlgosLogBase;
public record AlgosSecurityLog : AlgosLogBase;

public record AlgosMetricLog : AlgosLogBase
{
    public string Name { get; init; } = "";
    public double Value { get; init; }
}

public record AlgosFeatureUsageLog : AlgosLogBase
{
    public string ModuleName { get; init; } = "";
    public string FeatureName { get; init; } = "";
    public long? DurationMs { get; init; }
}

public record AlgosTraceLog : AlgosLogBase
{
    public string OperationName { get; init; } = "";
    public long DurationMs { get; init; }
    public bool IsError { get; init; }
}

public record AlgosDependencyLog : AlgosLogBase
{
    public string DependencyName { get; init; } = "";
    public string OperationName { get; init; } = "";
    public long DurationMs { get; init; }
    public bool Success { get; init; } = true;
}

public record AlgosTraceTree(string TraceId, IReadOnlyList<AlgosTraceNode> Roots);
public record AlgosTraceNode(AlgosTraceLog Span, IReadOnlyList<AlgosTraceNode> Children);

public record AlgosQuery
{
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed class AlgosFeatureAttribute : Attribute
{
    public AlgosFeatureAttribute(string module, string feature)
    {
        Module = module;
        Feature = feature;
    }

    public string Module { get; }
    public string Feature { get; }
}

internal sealed class BoundedBuffer<T>
{
    private readonly ConcurrentQueue<T> _items = new();
    private readonly Func<T, DateTimeOffset> _timestamp;
    private int _maxItems;

    public BoundedBuffer(int maxItems, Func<T, DateTimeOffset> timestamp)
    {
        _maxItems = Math.Max(1, maxItems);
        _timestamp = timestamp;
    }

    public void SetMaxItems(int maxItems) => _maxItems = Math.Max(1, maxItems);

    public void Add(T item)
    {
        _items.Enqueue(item);
        while (_items.Count > _maxItems && _items.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<T> Snapshot(TimeSpan? retention = null)
    {
        var cutoff = retention.HasValue ? DateTimeOffset.UtcNow.Subtract(retention.Value) : DateTimeOffset.MinValue;
        return _items.Where(x => _timestamp(x) >= cutoff).ToArray();
    }
}
