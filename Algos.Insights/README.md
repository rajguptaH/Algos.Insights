# Algos.Insights

`Algos.Insights` is a production-safe ASP.NET Core observability package for request logging, metrics, tracing, feature usage analytics, exceptions, exports, alerts, and a secure built-in dashboard.

Author: Raj Narayan Gupta  
GitHub: https://github.com/rajguptaH  
Email: rajnarayan.guptanet@gmail.com

## Install

```bash
dotnet add package Algos.Insights
```

## Quick Start

```csharp
builder.Services.AddAlgosInsights(options =>
{
    options.ApplicationName = "My Production API";
    options.EnvironmentName = builder.Environment.EnvironmentName;
    options.EnableAutomaticRequestLogging = true;
    options.EnableRequestBodyLogging = false;
    options.EnableResponseBodyLogging = false;

    options.Storage.UseInMemory(memory =>
    {
        memory.MaxRequestLogs = 10000;
        memory.RetentionHours = 48;
    });

    options.Dashboard.Enabled = true;
    options.Dashboard.Route = "/myinsights";
    options.Dashboard.Username = builder.Configuration["AlgosInsights:Username"];
    options.Dashboard.Password = builder.Configuration["AlgosInsights:Password"];
});

app.UseAlgosInsights();
app.MapAlgosInsightsDashboard("/myinsights", dashboard =>
{
    dashboard.RequireBasicAuth("admin", "strong-password");
    dashboard.Title = "Algos Insights";
});
```

## Manual Logging

Inject `IAlgosInsightsLogger` and call `TrackEvent`, `TrackMetric`, `TrackFeatureUsage`, `TrackException`, `StartActivity`, or `TrackDependencyAsync`.

## Security Defaults

Request and response body logging are disabled by default. Sensitive fields such as authorization headers, cookies, tokens, passwords, secrets, OTPs, card values, CVV, API keys, and connection strings are masked before storage, export, dashboard display, or AI context use. The dashboard uses Basic Auth in the first version and should be explicitly configured before production use.

## Dashboard

The first version includes:

- Overview
- Request logs
- Request details endpoint surface
- Trace hierarchy API
- Feature usage
- Exceptions
- JSON and CSV request export

## Storage

Included providers:

- In-memory store
- JSON file append store

Future providers are planned for SQL Server, PostgreSQL, SQLite, MongoDB, Elasticsearch, and OpenSearch.

## Providers

The package exposes provider abstractions for Azure Application Insights, Azure Monitor/OpenTelemetry, AWS CloudWatch, console, and custom exporters. The first version keeps cloud providers as extension points with TODO markers so the core package stays lightweight and safe.

## AI Assistant

AI is disabled by default. The `IAlgosAiProvider` abstraction is included for OpenAI-compatible, Azure OpenAI-compatible, custom HTTP, and local LLM providers. Always redact context before sending it to AI and never expose API keys in the dashboard.

## Alerts

The alert service supports rule evaluation and cooldowns. SMTP/webhook/Slack/Teams delivery providers are intended future extensions.

## Production Recommendations

- Keep body logging disabled unless temporarily diagnosing a specific issue.
- Use strong dashboard credentials and terminate TLS at the host.
- Exclude health checks, Swagger, static assets, and the dashboard route from logging.
- Use bounded retention and export limits.
- Treat AI responses as advisory.

## Roadmap

- OpenTelemetry exporters
- Azure Application Insights provider
- AWS CloudWatch provider
- SQL and document database storage providers
- Rich trace waterfall
- Alert delivery providers
- AI chat provider implementations

## License

MIT
