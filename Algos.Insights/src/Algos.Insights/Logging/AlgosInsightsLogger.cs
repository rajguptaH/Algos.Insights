using System.Diagnostics;
using Algos.Insights.Alerts;
using Algos.Insights.Models;
using Algos.Insights.Options;
using Algos.Insights.Redaction;
using Algos.Insights.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Algos.Insights.Logging;

public sealed class AlgosInsightsLogger : IAlgosInsightsLogger
{
    public static readonly ActivitySource ActivitySource = new("Algos.Insights");
    private readonly IAlgosInsightsStore _store;
    private readonly IAlgosRedactor _redactor;
    private readonly AlgosInsightsOptions _options;
    private readonly IAlgosAlertService _alerts;
    private readonly ILogger<AlgosInsightsLogger> _logger;

    public AlgosInsightsLogger(IAlgosInsightsStore store, IAlgosRedactor redactor, IOptions<AlgosInsightsOptions> options, IAlgosAlertService alerts, ILogger<AlgosInsightsLogger> logger)
    {
        _store = store;
        _redactor = redactor;
        _options = options.Value;
        _alerts = alerts;
        _logger = logger;
    }

    public IDisposable BeginScope(string name, object? properties = null)
    {
        var activity = StartActivity(name, properties);
        return activity is not null ? activity : NullScope.Instance;
    }

    public void TrackEvent(string eventName, object? properties = null) => SafeFire(async () =>
    {
        var activity = Activity.Current;
        await _store.SaveEventAsync(new AlgosEventLog
        {
            ApplicationName = _options.ApplicationName,
            Environment = _options.EnvironmentName,
            EventName = eventName,
            Message = eventName,
            Category = "Manual",
            Properties = _redactor.RedactObject(properties),
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString(),
            ParentSpanId = activity?.ParentSpanId.ToString()
        });
    });

    public void TrackMetric(string name, double value, object? properties = null) => SafeFire(async () =>
    {
        var activity = Activity.Current;
        await _store.SaveMetricAsync(new AlgosMetricLog
        {
            ApplicationName = _options.ApplicationName,
            Environment = _options.EnvironmentName,
            Name = name,
            Value = value,
            Message = name,
            Properties = _redactor.RedactObject(properties),
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString()
        });
    });

    public void TrackFeatureUsage(string module, string feature, object? properties = null) => SafeFire(async () =>
    {
        var activity = Activity.Current;
        await _store.SaveFeatureUsageAsync(new AlgosFeatureUsageLog
        {
            ApplicationName = _options.ApplicationName,
            Environment = _options.EnvironmentName,
            Module = module,
            Feature = feature,
            ModuleName = module,
            FeatureName = feature,
            Message = $"{module}/{feature}",
            Properties = _redactor.RedactObject(properties),
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString()
        });
    });

    public void TrackException(Exception exception, object? properties = null) => SafeFire(async () =>
    {
        var activity = Activity.Current;
        var log = new AlgosExceptionLog
        {
            ApplicationName = _options.ApplicationName,
            Environment = _options.EnvironmentName,
            Severity = AlgosSeverity.Error,
            Category = "Manual",
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.ToString(),
            Properties = _redactor.RedactObject(properties),
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString(),
            ParentSpanId = activity?.ParentSpanId.ToString()
        };

        await _store.SaveExceptionAsync(log);
        await _alerts.EvaluateExceptionAsync(log);
    });

    public Activity? StartActivity(string operationName, object? properties = null)
    {
        var activity = ActivitySource.StartActivity(operationName);
        if (activity is null)
        {
            return null;
        }

        foreach (var item in _redactor.RedactObject(properties))
        {
            activity.SetTag(item.Key, item.Value);
        }

        return new StoredActivity(activity, _store, _options, _logger);
    }

    public async Task<T> TrackDependencyAsync<T>(string dependencyName, string operationName, Func<Task<T>> operation)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = StartActivity($"{dependencyName}.{operationName}");
        try
        {
            var result = await operation();
            await SaveDependencyAsync(dependencyName, operationName, started, true, null);
            return result;
        }
        catch (Exception ex)
        {
            await SaveDependencyAsync(dependencyName, operationName, started, false, ex);
            throw;
        }
    }

    public async Task TrackDependencyAsync(string dependencyName, string operationName, Func<Task> operation) =>
        await TrackDependencyAsync(dependencyName, operationName, async () => { await operation(); return true; });

    private async Task SaveDependencyAsync(string dependencyName, string operationName, long started, bool success, Exception? ex)
    {
        try
        {
            var activity = Activity.Current;
            await _store.SaveDependencyAsync(new AlgosDependencyLog
            {
                ApplicationName = _options.ApplicationName,
                Environment = _options.EnvironmentName,
                DependencyName = dependencyName,
                OperationName = operationName,
                DurationMs = ElapsedMilliseconds(started),
                Success = success,
                Severity = success ? AlgosSeverity.Information : AlgosSeverity.Error,
                Message = ex?.Message,
                TraceId = activity?.TraceId.ToString(),
                SpanId = activity?.SpanId.ToString(),
                ParentSpanId = activity?.ParentSpanId.ToString()
            });
        }
        catch (Exception saveEx)
        {
            _logger.LogDebug(saveEx, "Algos.Insights dependency logging failed.");
        }
    }

    private void SafeFire(Func<Task> action)
    {
        _ = Task.Run(async () =>
        {
            try { await action(); }
            catch (Exception ex) { _logger.LogDebug(ex, "Algos.Insights manual logging failed."); }
        });
    }

    private sealed class StoredActivity : Activity
    {
        private readonly Activity _inner;
        private readonly IAlgosInsightsStore _store;
        private readonly AlgosInsightsOptions _options;
        private readonly ILogger _logger;
        private readonly long _started = Stopwatch.GetTimestamp();

        public StoredActivity(Activity inner, IAlgosInsightsStore store, AlgosInsightsOptions options, ILogger logger) : base(inner.OperationName)
        {
            _inner = inner;
            _store = store;
            _options = options;
            _logger = logger;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                _inner.Dispose();
                _store.SaveTraceAsync(new AlgosTraceLog
                {
                    ApplicationName = _options.ApplicationName,
                    Environment = _options.EnvironmentName,
                    OperationName = _inner.OperationName,
                    Message = _inner.OperationName,
                    DurationMs = ElapsedMilliseconds(_started),
                    TraceId = _inner.TraceId.ToString(),
                    SpanId = _inner.SpanId.ToString(),
                    ParentSpanId = _inner.ParentSpanId.ToString(),
                    IsError = _inner.Status == ActivityStatusCode.Error
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Algos.Insights span logging failed.");
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }

    private static long ElapsedMilliseconds(long started) =>
        (long)((Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency);
}
