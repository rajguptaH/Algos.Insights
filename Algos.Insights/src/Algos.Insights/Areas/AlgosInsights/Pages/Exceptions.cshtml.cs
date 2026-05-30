using Algos.Insights.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Algos.Insights.Dashboard.Pages;

public sealed class ExceptionsModel : DashboardPageModel
{
    public ExceptionsModel(IOptions<AlgosInsightsOptions> options) : base(options) { }
    public IActionResult OnGet() => DashboardPage();
}
