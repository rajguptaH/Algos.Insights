using System.Text;
using System.Text.Json;
using Algos.Insights.Models;
using Algos.Insights.Options;
using Algos.Insights.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace Algos.Insights.Export;

public interface IAlgosExportService
{
    Task<string> ExportRequestsJsonAsync(AlgosQuery query, CancellationToken cancellationToken = default);
    Task<string> ExportRequestsCsvAsync(AlgosQuery query, CancellationToken cancellationToken = default);
}

public sealed class AlgosExportService : IAlgosExportService
{
    private readonly IAlgosInsightsStore _store;
    private readonly AlgosInsightsOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public AlgosExportService(IAlgosInsightsStore store, IOptions<AlgosInsightsOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    public async Task<string> ExportRequestsJsonAsync(AlgosQuery query, CancellationToken cancellationToken = default)
    {
        var page = await _store.GetRequestsAsync(query with { PageSize = _options.Dashboard.MaxExportRows }, cancellationToken);
        return JsonSerializer.Serialize(page.Items, JsonOptions);
    }

    public async Task<string> ExportRequestsCsvAsync(AlgosQuery query, CancellationToken cancellationToken = default)
    {
        var page = await _store.GetRequestsAsync(query with { PageSize = _options.Dashboard.MaxExportRows }, cancellationToken);
        var csv = new StringBuilder();
        csv.AppendLine("TimestampUtc,Method,Path,StatusCode,DurationMs,TraceId,UserId");
        foreach (var item in page.Items)
        {
            csv.AppendLine($"{item.TimestampUtc:u},{Esc(item.Method)},{Esc(item.Path)},{item.StatusCode},{item.DurationMs},{Esc(item.TraceId)},{Esc(item.UserId)}");
        }

        return csv.ToString();
    }

    private static string Esc(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
