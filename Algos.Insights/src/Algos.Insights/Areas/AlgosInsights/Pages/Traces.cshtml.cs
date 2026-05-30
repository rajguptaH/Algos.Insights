using Algos.Insights.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Algos.Insights.Dashboard.Pages;

public sealed class TracesModel : DashboardPageModel
{
    public TracesModel(IOptions<AlgosInsightsOptions> options) : base(options) { }
    public string TraceId { get; private set; } = "";
    public IActionResult OnGet(string? traceId)
    {
        TraceId = traceId ?? "";
        return DashboardPage();
    }
}
