using System.Reflection;
using System.Text;
using System.Text.Json;
using Algos.Insights.AI;
using Algos.Insights.Dashboard.Security;
using Algos.Insights.Export;
using Algos.Insights.Models;
using Algos.Insights.Options;
using Algos.Insights.Storage.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Algos.Insights.Dashboard.Endpoints;

public sealed class AlgosDashboardBuilder
{
    internal string? Username { get; private set; }
    internal string? Password { get; private set; }
    internal string? TitleValue { get; private set; }

    public string? Title { get => TitleValue; set => TitleValue = value; }

    public void RequireBasicAuth(string username, string password)
    {
        Username = username;
        Password = password;
    }
}

public static class AlgosDashboardEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly HashSet<string> DashboardAssets = new(StringComparer.OrdinalIgnoreCase)
    {
        "dashboard.css",
        "dashboard-core.js",
        "overview.js",
        "requests.js",
        "features.js",
        "exceptions.js",
        "dependencies.js",
        "traces.js"
    };

    public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder endpoints, string route, Action<AlgosDashboardBuilder>? configure = null)
    {
        var builder = new AlgosDashboardBuilder();
        configure?.Invoke(builder);
        var root = "/" + route.Trim('/');

        endpoints.MapGet(root + "/api/overview", GetOverview);
        endpoints.MapGet(root + "/api/requests", GetRequests);
        endpoints.MapGet(root + "/api/requests/{id}", GetRequest);
        endpoints.MapGet(root + "/api/exceptions", GetExceptions);
        endpoints.MapGet(root + "/api/features", GetFeatures);
        endpoints.MapGet(root + "/api/dependencies", GetDependencies);
        endpoints.MapGet(root + "/api/analytics", GetAnalytics);
        endpoints.MapGet(root + "/api/traces/{traceId}", GetTrace);
        endpoints.MapGet(root + "/export/requests.json", ExportRequestsJson);
        endpoints.MapGet(root + "/export/requests.csv", ExportRequestsCsv);
        endpoints.MapPost(root + "/api/ai", AskAi);
        endpoints.MapGet(root + "/assets/{file}", ServeAsset);
        endpoints.MapRazorPages();

        endpoints.ServiceProvider.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value.Dashboard.Route = root;
        if (builder.Username is not null)
        {
            var options = endpoints.ServiceProvider.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value.Dashboard;
            options.Username = builder.Username;
            options.Password = builder.Password;
            options.AuthMode = AlgosInsightsAuthMode.Basic;
        }

        if (builder.TitleValue is not null)
        {
            endpoints.ServiceProvider.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value.Dashboard.Title = builder.TitleValue;
        }

        return endpoints;
    }

    private static async Task GetOverview(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var store = context.RequestServices.GetRequiredService<IAlgosInsightsStore>();
        var requests = await store.GetRequestsAsync(new AlgosQuery { PageSize = 500 });
        var exceptions = await store.GetExceptionsAsync(new AlgosQuery { PageSize = 50 });
        var features = await store.GetFeatureUsageAsync(new AlgosQuery { PageSize = 500 });
        var dependencies = await store.GetDependenciesAsync(new AlgosQuery { PageSize = 500 });
        var total = requests.TotalCount;
        var errors = requests.Items.Count(x => x.StatusCode >= 500);
        var avg = requests.Items.Count == 0 ? 0 : requests.Items.Average(x => x.DurationMs);
        var p95 = Percentile(requests.Items.Select(x => x.DurationMs), 0.95);
        var statusCodes = requests.Items.GroupBy(x => x.StatusCode).OrderBy(x => x.Key).Select(x => new { statusCode = x.Key, count = x.Count() });
        var slowest = requests.Items.OrderByDescending(x => x.DurationMs).Take(8);
        var topEndpoints = requests.Items.GroupBy(x => x.Path).Select(x => new { path = x.Key, count = x.Count(), avgMs = Math.Round(x.Average(r => r.DurationMs), 1), errors = x.Count(r => r.StatusCode >= 500) }).OrderByDescending(x => x.count).Take(8);
        var topModules = features.Items.GroupBy(x => string.IsNullOrWhiteSpace(x.ModuleName) ? "Unassigned" : x.ModuleName).Select(x => new { module = x.Key, count = x.Count(), avgMs = Math.Round(x.Where(f => f.DurationMs.HasValue).Select(f => (double)f.DurationMs!.Value).DefaultIfEmpty(0).Average(), 1) }).OrderByDescending(x => x.count).Take(8);
        await context.Response.WriteAsJsonAsync(new
        {
            total,
            errors,
            errorRate = total == 0 ? 0 : Math.Round(errors * 100d / total, 2),
            avg = Math.Round(avg, 1),
            p50 = Percentile(requests.Items.Select(x => x.DurationMs), 0.50),
            p95,
            p99 = Percentile(requests.Items.Select(x => x.DurationMs), 0.99),
            statusCodes,
            slowest,
            topEndpoints,
            topModules,
            dependencies = dependencies.Items.GroupBy(x => x.DependencyName).Select(x => new { name = x.Key, count = x.Count(), failures = x.Count(d => !d.Success), avgMs = Math.Round(x.Average(d => d.DurationMs), 1) }).OrderByDescending(x => x.avgMs).Take(8),
            recentCritical = exceptions.Items.Take(5)
        }, JsonOptions);
    }

    private static async Task GetRequests(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var store = context.RequestServices.GetRequiredService<IAlgosInsightsStore>();
        await context.Response.WriteAsJsonAsync(await store.GetRequestsAsync(Query(context)), JsonOptions);
    }

    private static async Task GetRequest(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var id = context.Request.RouteValues["id"]?.ToString() ?? "";
        var store = context.RequestServices.GetRequiredService<IAlgosInsightsStore>();
        var requests = await store.GetRequestsAsync(new AlgosQuery { PageSize = options.Dashboard.MaxExportRows });
        var request = requests.Items.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (request is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var exceptions = await store.GetExceptionsAsync(new AlgosQuery { PageSize = 500 });
        var dependencies = await store.GetDependenciesAsync(new AlgosQuery { PageSize = 500 });
        await context.Response.WriteAsJsonAsync(new
        {
            request,
            exceptions = exceptions.Items.Where(x => x.TraceId == request.TraceId),
            dependencies = dependencies.Items.Where(x => x.TraceId == request.TraceId),
            trace = string.IsNullOrWhiteSpace(request.TraceId) ? null : await store.GetTraceTreeAsync(request.TraceId)
        }, JsonOptions);
    }

    private static async Task GetExceptions(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var store = context.RequestServices.GetRequiredService<IAlgosInsightsStore>();
        await context.Response.WriteAsJsonAsync(await store.GetExceptionsAsync(Query(context)), JsonOptions);
    }

    private static async Task GetFeatures(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var store = context.RequestServices.GetRequiredService<IAlgosInsightsStore>();
        await context.Response.WriteAsJsonAsync(await store.GetFeatureUsageAsync(Query(context)), JsonOptions);
    }

    private static async Task GetDependencies(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var store = context.RequestServices.GetRequiredService<IAlgosInsightsStore>();
        await context.Response.WriteAsJsonAsync(await store.GetDependenciesAsync(Query(context)), JsonOptions);
    }

    private static async Task GetAnalytics(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var store = context.RequestServices.GetRequiredService<IAlgosInsightsStore>();
        var features = (await store.GetFeatureUsageAsync(new AlgosQuery { PageSize = options.Dashboard.MaxExportRows })).Items;
        var requests = (await store.GetRequestsAsync(new AlgosQuery { PageSize = options.Dashboard.MaxExportRows })).Items;
        var modules = features.GroupBy(x => string.IsNullOrWhiteSpace(x.ModuleName) ? "Unassigned" : x.ModuleName)
            .Select(x => new
            {
                name = x.Key,
                count = x.Count(),
                users = x.Select(f => f.UserId).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().Count(),
                avgMs = Math.Round(x.Where(f => f.DurationMs.HasValue).Select(f => (double)f.DurationMs!.Value).DefaultIfEmpty(0).Average(), 1),
                features = x.GroupBy(f => string.IsNullOrWhiteSpace(f.FeatureName) ? "Unassigned" : f.FeatureName).Select(f => new { name = f.Key, count = f.Count() }).OrderByDescending(f => f.count)
            })
            .OrderByDescending(x => x.count)
            .ToArray();
        var leastUsed = modules.OrderBy(x => x.count).Take(8);
        var trends = features.GroupBy(x => x.TimestampUtc.UtcDateTime.Date).OrderBy(x => x.Key).Select(x => new { date = x.Key.ToString("yyyy-MM-dd"), count = x.Count() });
        var routeModules = requests.GroupBy(x => x.Module ?? "Unassigned").Select(x => new { module = x.Key, requests = x.Count(), errors = x.Count(r => r.StatusCode >= 500), avgMs = Math.Round(x.Average(r => r.DurationMs), 1) }).OrderByDescending(x => x.requests);
        await context.Response.WriteAsJsonAsync(new { modules, leastUsed, trends, routeModules }, JsonOptions);
    }

    private static async Task GetTrace(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var traceId = context.Request.RouteValues["traceId"]?.ToString() ?? "";
        var store = context.RequestServices.GetRequiredService<IAlgosInsightsStore>();
        await context.Response.WriteAsJsonAsync(await store.GetTraceTreeAsync(traceId), JsonOptions);
    }

    private static async Task ExportRequestsJson(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard) || !options.Dashboard.EnableDataExport) return;
        var export = context.RequestServices.GetRequiredService<IAlgosExportService>();
        context.Response.ContentType = "application/json";
        context.Response.Headers.ContentDisposition = "attachment; filename=algos-requests.json";
        await context.Response.WriteAsync(await export.ExportRequestsJsonAsync(Query(context)));
    }

    private static async Task ExportRequestsCsv(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard) || !options.Dashboard.EnableDataExport) return;
        var export = context.RequestServices.GetRequiredService<IAlgosExportService>();
        context.Response.ContentType = "text/csv";
        context.Response.Headers.ContentDisposition = "attachment; filename=algos-requests.csv";
        await context.Response.WriteAsync(await export.ExportRequestsCsvAsync(Query(context)));
    }

    private static async Task AskAi(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard) || !options.AI.Enabled || !options.Dashboard.EnableAiChat) return;
        var payload = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(context.Request.Body, cancellationToken: context.RequestAborted);
        var ai = context.RequestServices.GetRequiredService<IAlgosAiProvider>();
        var response = await ai.AskAsync(new AlgosAiRequest(payload?.GetValueOrDefault("question") ?? "", new Dictionary<string, object?>()), context.RequestAborted);
        await context.Response.WriteAsJsonAsync(response, JsonOptions);
    }

    private static async Task ServeAsset(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var file = context.Request.RouteValues["file"]?.ToString() ?? "";
        if (!DashboardAssets.Contains(file))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = file.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? "text/css; charset=utf-8" : "application/javascript; charset=utf-8";
        await context.Response.WriteAsync(await ReadResourceAsync("Algos.Insights.Dashboard.Assets." + file));
    }

    private static bool Authorize(HttpContext context, AlgosDashboardOptions options)
    {
        if (AlgosDashboardAuth.IsAuthorized(context, options))
        {
            return true;
        }

        AlgosDashboardAuth.Challenge(context);
        return false;
    }

    private static AlgosQuery Query(HttpContext context) => new()
    {
        Page = int.TryParse(context.Request.Query["page"], out var p) ? p : 1,
        PageSize = int.TryParse(context.Request.Query["pageSize"], out var s) ? s : 50,
        Search = context.Request.Query["search"]
    };

    private static double Percentile(IEnumerable<long> values, double percentile)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        if (sorted.Length == 0) return 0;
        return sorted[(int)Math.Clamp(Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)];
    }

    private static async Task<string> ReadResourceAsync(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Dashboard resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static string HtmlEncode(string value) => System.Net.WebUtility.HtmlEncode(value);
}
