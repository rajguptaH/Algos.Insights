using Algos.Insights.Dashboard.Security;
using Algos.Insights.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Algos.Insights.Dashboard.Pages;

public abstract class DashboardPageModel : PageModel
{
    private readonly AlgosInsightsOptions _options;

    protected DashboardPageModel(IOptions<AlgosInsightsOptions> options)
    {
        _options = options.Value;
    }

    public string Route => _options.Dashboard.Route;

    protected IActionResult DashboardPage()
    {
        if (!AlgosDashboardAuth.IsAuthorized(HttpContext, _options.Dashboard))
        {
            AlgosDashboardAuth.Challenge(HttpContext);
            return new EmptyResult();
        }

        return Page();
    }
}

public sealed class OverviewModel : DashboardPageModel
{
    public OverviewModel(IOptions<AlgosInsightsOptions> options) : base(options) { }
    public IActionResult OnGet() => DashboardPage();
}
