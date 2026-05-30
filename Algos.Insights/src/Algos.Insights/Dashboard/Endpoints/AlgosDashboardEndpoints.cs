using System.Net;
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

    public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder endpoints, string route, Action<AlgosDashboardBuilder>? configure = null)
    {
        var builder = new AlgosDashboardBuilder();
        configure?.Invoke(builder);
        var root = "/" + route.Trim('/');

        endpoints.MapGet(root, async context => await RenderPage(context, "overview"));
        endpoints.MapGet(root + "/requests", async context => await RenderPage(context, "requests"));
        endpoints.MapGet(root + "/requests/{id}", async context => await RenderPage(context, "request"));
        endpoints.MapGet(root + "/traces/{traceId}", async context => await RenderPage(context, "trace"));
        endpoints.MapGet(root + "/features", async context => await RenderPage(context, "features"));
        endpoints.MapGet(root + "/exceptions", async context => await RenderPage(context, "exceptions"));
        endpoints.MapGet(root + "/api/overview", GetOverview);
        endpoints.MapGet(root + "/api/requests", GetRequests);
        endpoints.MapGet(root + "/api/exceptions", GetExceptions);
        endpoints.MapGet(root + "/api/features", GetFeatures);
        endpoints.MapGet(root + "/api/traces/{traceId}", GetTrace);
        endpoints.MapGet(root + "/export/requests.json", ExportRequestsJson);
        endpoints.MapGet(root + "/export/requests.csv", ExportRequestsCsv);
        endpoints.MapPost(root + "/api/ai", AskAi);

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

    private static async Task RenderPage(HttpContext context, string page)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard))
        {
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(Html(options.Dashboard.Title, options.Dashboard.Route, page));
    }

    private static async Task GetOverview(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var store = context.RequestServices.GetRequiredService<IAlgosInsightsStore>();
        var requests = await store.GetRequestsAsync(new AlgosQuery { PageSize = 500 });
        var exceptions = await store.GetExceptionsAsync(new AlgosQuery { PageSize = 50 });
        var total = requests.TotalCount;
        var errors = requests.Items.Count(x => x.StatusCode >= 500);
        var avg = requests.Items.Count == 0 ? 0 : requests.Items.Average(x => x.DurationMs);
        var p95 = Percentile(requests.Items.Select(x => x.DurationMs), 0.95);
        await context.Response.WriteAsJsonAsync(new { total, errors, errorRate = total == 0 ? 0 : Math.Round(errors * 100d / total, 2), avg, p95, recentCritical = exceptions.Items.Take(5) }, JsonOptions);
    }

    private static async Task GetRequests(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AlgosInsightsOptions>>().Value;
        if (!Authorize(context, options.Dashboard)) return;
        var store = context.RequestServices.GetRequiredService<IAlgosInsightsStore>();
        await context.Response.WriteAsJsonAsync(await store.GetRequestsAsync(Query(context)), JsonOptions);
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

    private static string Html(string title, string route, string page)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        return $$"""
<!doctype html>
<html lang="en" class="dark">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{safeTitle}}</title>
  <script src="https://cdn.tailwindcss.com"></script>
  <script>tailwind.config={darkMode:'class'}</script>
</head>
<body class="bg-slate-950 text-slate-100">
  <div class="min-h-screen lg:flex">
    <aside class="border-b border-slate-800 bg-slate-900/80 p-4 lg:min-h-screen lg:w-64 lg:border-b-0 lg:border-r">
      <div class="text-xl font-semibold">{{safeTitle}}</div>
      <nav class="mt-6 grid gap-2 text-sm">
        <a class="rounded px-3 py-2 hover:bg-slate-800" href="{{route}}">Overview</a>
        <a class="rounded px-3 py-2 hover:bg-slate-800" href="{{route}}/requests">Request Logs</a>
        <a class="rounded px-3 py-2 hover:bg-slate-800" href="{{route}}/features">Feature Usage</a>
        <a class="rounded px-3 py-2 hover:bg-slate-800" href="{{route}}/exceptions">Exceptions</a>
      </nav>
    </aside>
    <main class="flex-1 p-4 lg:p-8">
      <div class="mb-6 flex flex-wrap items-center justify-between gap-3">
        <h1 class="text-2xl font-semibold capitalize">{{page}}</h1>
        <div class="flex gap-2 text-sm">
          <a class="rounded bg-emerald-600 px-3 py-2 hover:bg-emerald-500" href="{{route}}/export/requests.json">JSON</a>
          <a class="rounded bg-sky-600 px-3 py-2 hover:bg-sky-500" href="{{route}}/export/requests.csv">CSV</a>
        </div>
      </div>
      <section id="stats" class="grid gap-3 md:grid-cols-4"></section>
      <section class="mt-6 overflow-hidden rounded-lg border border-slate-800 bg-slate-900">
        <div class="border-b border-slate-800 p-4">
          <input id="search" class="w-full rounded border border-slate-700 bg-slate-950 px-3 py-2 text-sm" placeholder="Search">
        </div>
        <div id="content" class="overflow-x-auto"></div>
      </section>
    </main>
  </div>
<script>
const route = '{{route}}';
const page = '{{page}}';
const fmt = v => v ?? '';
async function json(url){ const r = await fetch(url); if(!r.ok) throw new Error(r.status); return await r.json(); }
function stat(label,value){ return `<div class="rounded-lg border border-slate-800 bg-slate-900 p-4"><div class="text-xs uppercase text-slate-400">${label}</div><div class="mt-2 text-2xl font-semibold">${value}</div></div>`; }
function table(rows, cols){ return `<table class="min-w-full text-sm"><thead class="bg-slate-950 text-left text-slate-400"><tr>${cols.map(c=>`<th class="px-4 py-3">${c}</th>`).join('')}</tr></thead><tbody>${rows}</tbody></table>`; }
async function load(){
  const overview = await json(route + '/api/overview');
  document.querySelector('#stats').innerHTML = stat('Requests', overview.total) + stat('Errors', overview.errors) + stat('Error rate', overview.errorRate + '%') + stat('P95', overview.p95 + 'ms');
  let endpoint = page === 'exceptions' ? '/api/exceptions' : page === 'features' ? '/api/features' : '/api/requests';
  const data = await json(route + endpoint + '?search=' + encodeURIComponent(document.querySelector('#search').value));
  const rows = data.items.map(x => page === 'exceptions'
    ? `<tr class="border-t border-slate-800"><td class="px-4 py-3">${fmt(x.timestampUtc)}</td><td class="px-4 py-3">${fmt(x.exceptionType)}</td><td class="px-4 py-3">${fmt(x.message)}</td><td class="px-4 py-3">${fmt(x.traceId)}</td></tr>`
    : page === 'features'
    ? `<tr class="border-t border-slate-800"><td class="px-4 py-3">${fmt(x.timestampUtc)}</td><td class="px-4 py-3">${fmt(x.moduleName)}</td><td class="px-4 py-3">${fmt(x.featureName)}</td><td class="px-4 py-3">${fmt(x.userId)}</td></tr>`
    : `<tr class="border-t border-slate-800"><td class="px-4 py-3">${fmt(x.timestampUtc)}</td><td class="px-4 py-3">${fmt(x.method)}</td><td class="px-4 py-3">${fmt(x.path)}</td><td class="px-4 py-3">${fmt(x.statusCode)}</td><td class="px-4 py-3">${fmt(x.durationMs)}ms</td><td class="px-4 py-3">${fmt(x.traceId)}</td></tr>`).join('');
  const cols = page === 'exceptions' ? ['Time','Type','Message','Trace'] : page === 'features' ? ['Time','Module','Feature','User'] : ['Time','Method','Path','Status','Duration','Trace'];
  document.querySelector('#content').innerHTML = table(rows || '<tr><td class="px-4 py-8 text-slate-400" colspan="6">No data yet.</td></tr>', cols);
}
document.querySelector('#search').addEventListener('input', () => load());
load().catch(e => document.querySelector('#content').innerHTML = `<div class="p-6 text-rose-300">${e.message}</div>`);
</script>
</body>
</html>
""";
    }
}
