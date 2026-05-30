using Algos.Insights.AI;
using Algos.Insights.Alerts;
using Algos.Insights.Dashboard.Endpoints;
using Algos.Insights.Export;
using Algos.Insights.Logging;
using Algos.Insights.Middleware;
using Algos.Insights.Options;
using Algos.Insights.Redaction;
using Algos.Insights.Storage.Abstractions;
using Algos.Insights.Storage.InMemory;
using Algos.Insights.Storage.JsonFile;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Algos.Insights.Extensions;

public static class AlgosInsightsExtensions
{
    public static IServiceCollection AddAlgosInsights(this IServiceCollection services, Action<AlgosInsightsOptions>? configure = null)
    {
        var options = new AlgosInsightsOptions();
        configure?.Invoke(options);
        var memoryOptions = new AlgosInMemoryStorageOptions();
        options.Storage.InMemoryConfigure(memoryOptions);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(memoryOptions));

        if (options.Storage.JsonFileConfigure is not null)
        {
            var jsonOptions = new AlgosJsonFileStorageOptions();
            options.Storage.JsonFileConfigure(jsonOptions);
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jsonOptions));
            services.AddSingleton<IAlgosInsightsStore, JsonFileAlgosInsightsStore>();
        }
        else
        {
            services.AddSingleton<IAlgosInsightsStore, InMemoryAlgosInsightsStore>();
        }

        services.AddSingleton<IAlgosRedactor, AlgosRedactor>();
        services.AddSingleton<IAlgosInsightsLogger, AlgosInsightsLogger>();
        services.AddSingleton<IAlgosExportService, AlgosExportService>();
        services.AddSingleton<IAlgosAlertService, AlgosAlertService>();
        services.AddSingleton<IAlgosAiProvider, DisabledAlgosAiProvider>();
        var dashboardRoute = options.Dashboard.Route.Trim('/');
        services.AddRazorPages(razor =>
            {
                razor.Conventions.AddAreaPageRoute("AlgosInsights", "/Index", dashboardRoute);
                razor.Conventions.AddAreaPageRoute("AlgosInsights", "/Requests", dashboardRoute + "/requests");
                razor.Conventions.AddAreaPageRoute("AlgosInsights", "/Features", dashboardRoute + "/features");
                razor.Conventions.AddAreaPageRoute("AlgosInsights", "/Exceptions", dashboardRoute + "/exceptions");
                razor.Conventions.AddAreaPageRoute("AlgosInsights", "/Dependencies", dashboardRoute + "/dependencies");
                razor.Conventions.AddAreaPageRoute("AlgosInsights", "/Traces", dashboardRoute + "/traces");
            })
            .ConfigureApplicationPartManager(manager =>
            {
                var assembly = typeof(AlgosInsightsExtensions).Assembly;
                var factory = ApplicationPartFactory.GetApplicationPartFactory(assembly);
                foreach (var part in factory.GetApplicationParts(assembly).Where(part => part.GetType().Name.Contains("CompiledRazorAssemblyPart", StringComparison.Ordinal)))
                {
                    if (!manager.ApplicationParts.Any(existing => existing.Name == part.Name && existing.GetType() == part.GetType()))
                    {
                        manager.ApplicationParts.Add(part);
                    }
                }
            });
        return services;
    }

    public static IApplicationBuilder UseAlgosInsights(this IApplicationBuilder app) =>
        app.UseMiddleware<AlgosInsightsMiddleware>();

    public static IEndpointRouteBuilder MapAlgosInsightsDashboard(this IEndpointRouteBuilder endpoints, string? route = null, Action<AlgosDashboardBuilder>? configure = null)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AlgosInsightsOptions>>().Value;
        return endpoints.MapDashboard(route ?? options.Dashboard.Route, configure);
    }
}
