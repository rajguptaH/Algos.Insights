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
