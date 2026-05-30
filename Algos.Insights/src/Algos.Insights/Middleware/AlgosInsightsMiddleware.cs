using System.Diagnostics;
using System.Text;
using Algos.Insights.Alerts;
using Algos.Insights.Models;
using Algos.Insights.Options;
using Algos.Insights.Redaction;
using Algos.Insights.Storage.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Algos.Insights.Middleware;

public sealed class AlgosInsightsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AlgosInsightsOptions _options;
    private readonly IAlgosInsightsStore _store;
    private readonly IAlgosRedactor _redactor;
    private readonly IAlgosAlertService _alerts;
    private readonly ILogger<AlgosInsightsMiddleware> _logger;

    public AlgosInsightsMiddleware(RequestDelegate next, IOptions<AlgosInsightsOptions> options, IAlgosInsightsStore store, IAlgosRedactor redactor, IAlgosAlertService alerts, ILogger<AlgosInsightsMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _store = store;
        _redactor = redactor;
        _alerts = alerts;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.EnableAutomaticRequestLogging || ShouldIgnore(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        var originalBody = context.Response.Body;
        string? requestBody = null;
        string? responseBody = null;
        Exception? exception = null;

        try
        {
            requestBody = await ReadRequestBodyAsync(context);
            using var responseBuffer = _options.EnableResponseBodyLogging ? new MemoryStream() : null;
            if (responseBuffer is not null)
            {
                context.Response.Body = responseBuffer;
            }

            await _next(context);

            if (responseBuffer is not null)
            {
                responseBody = await ReadResponseBodyAsync(responseBuffer, originalBody);
            }
        }
        catch (Exception ex)
        {
            exception = ex;
            await SaveExceptionAsync(context, ex);
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
            await SaveRequestAsync(context, started, requestBody, responseBody, exception);
        }
    }

    private bool ShouldIgnore(PathString path) =>
        _options.IgnoreRoutes.Any(route => path.StartsWithSegments(route, StringComparison.OrdinalIgnoreCase)) ||
        path.StartsWithSegments(_options.Dashboard.Route, StringComparison.OrdinalIgnoreCase);

    private async Task<string?> ReadRequestBodyAsync(HttpContext context)
    {
        if (!_options.EnableRequestBodyLogging || context.Request.ContentLength is <= 0 or null || context.Request.ContentLength > _options.MaxBodySizeInBytes)
        {
            return null;
        }

        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        return body;
    }

    private async Task<string?> ReadResponseBodyAsync(MemoryStream responseBuffer, Stream originalBody)
    {
        if (responseBuffer.Length > _options.MaxBodySizeInBytes)
        {
            responseBuffer.Position = 0;
            await responseBuffer.CopyToAsync(originalBody);
            return null;
        }

        responseBuffer.Position = 0;
        var body = await new StreamReader(responseBuffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        responseBuffer.Position = 0;
        await responseBuffer.CopyToAsync(originalBody);
        return body;
    }

    private async Task SaveRequestAsync(HttpContext context, long started, string? requestBody, string? responseBody, Exception? exception)
    {
        try
        {
            var endpoint = context.GetEndpoint();
            var feature = endpoint?.Metadata.GetMetadata<AlgosFeatureAttribute>();
            var activity = Activity.Current;
            var headers = context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            var responseHeaders = context.Response.Headers.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            var log = new AlgosRequestLog
            {
                ApplicationName = _options.ApplicationName,
                Environment = _options.EnvironmentName,
                Severity = exception is not null || context.Response.StatusCode >= 500 ? AlgosSeverity.Error : AlgosSeverity.Information,
                Category = "Request",
                Module = feature?.Module,
                Feature = feature?.Feature,
                Message = $"{context.Request.Method} {context.Request.Path}",
                Method = context.Request.Method,
                Path = context.Request.Path,
                QueryString = context.Request.QueryString.Value,
                StatusCode = context.Response.StatusCode,
                DurationMs = ElapsedMilliseconds(started),
                RequestSize = context.Request.ContentLength,
                ResponseSize = context.Response.ContentLength,
                ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                RouteName = endpoint?.DisplayName,
                Controller = context.GetRouteValue("controller")?.ToString(),
                Action = context.GetRouteValue("action")?.ToString(),
                RequestHeaders = _redactor.Redact(headers),
                ResponseHeaders = _redactor.Redact(responseHeaders),
                RequestBody = requestBody,
                ResponseBody = responseBody,
                TraceId = activity?.TraceId.ToString() ?? context.TraceIdentifier,
                SpanId = activity?.SpanId.ToString(),
                ParentSpanId = activity?.ParentSpanId.ToString(),
                CorrelationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlation) ? correlation.ToString() : context.TraceIdentifier,
                UserId = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null
            };

            await _store.SaveRequestAsync(log);
            await _alerts.EvaluateRequestAsync(log);

            if (feature is not null && _options.EnableFeatureUsageTracking)
            {
                await _store.SaveFeatureUsageAsync(new AlgosFeatureUsageLog
                {
                    ApplicationName = _options.ApplicationName,
                    Environment = _options.EnvironmentName,
                    Module = feature.Module,
                    Feature = feature.Feature,
                    ModuleName = feature.Module,
                    FeatureName = feature.Feature,
                    DurationMs = log.DurationMs,
                    TraceId = log.TraceId,
                    SpanId = log.SpanId,
                    UserId = log.UserId,
                    Message = $"{feature.Module}/{feature.Feature}"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Algos.Insights request logging failed.");
        }
    }

    private async Task SaveExceptionAsync(HttpContext context, Exception exception)
    {
        try
        {
            var activity = Activity.Current;
            var log = new AlgosExceptionLog
            {
                ApplicationName = _options.ApplicationName,
                Environment = _options.EnvironmentName,
                Severity = AlgosSeverity.Error,
                Category = "Request",
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                Message = exception.Message,
                StackTrace = exception.ToString(),
                TraceId = activity?.TraceId.ToString() ?? context.TraceIdentifier,
                SpanId = activity?.SpanId.ToString(),
                ParentSpanId = activity?.ParentSpanId.ToString(),
                CorrelationId = context.TraceIdentifier,
                UserId = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null
            };

            await _store.SaveExceptionAsync(log);
            await _alerts.EvaluateExceptionAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Algos.Insights exception logging failed.");
        }
    }

    private static long ElapsedMilliseconds(long started) =>
        (long)((Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency);
}
