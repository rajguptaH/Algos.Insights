# Algos.Insights

Production-ready observability for ASP.NET Core applications.

`Algos.Insights` is a reusable .NET package that captures requests, logs, exceptions, metrics, traces, dependency calls, and feature/module usage, then presents them in a secure built-in Razor Pages dashboard.

It is designed for teams that want practical application insight without standing up a full observability stack on day one.

## Highlights

- Automatic ASP.NET Core request logging
- Secure built-in dashboard powered by Razor Class Library pages
- Request detail drawer with headers, correlation IDs, trace IDs, exceptions, dependencies, and hierarchy
- Feature and module usage analytics
- Metrics for request volume, latency, percentiles, status codes, and error rate
- Manual logging, metrics, tracing, feature tracking, and dependency tracking APIs
- Sensitive data redaction before storage, export, dashboard display, or AI context
- In-memory storage and JSON file storage
- JSON and CSV export
- SMTP alert rules with cooldowns
- AI provider abstraction, disabled by default
- Extension points for Azure Application Insights, Azure Monitor/OpenTelemetry, AWS CloudWatch, and future storage providers

## Package

```txt
NuGet: Algos.Insights
Namespace: Algos.Insights
Targets: .NET 6, .NET 7, .NET 8, .NET 9
Main use case: ASP.NET Core web apps and APIs
```

Author: Raj Narayan Gupta  
GitHub: https://github.com/rajguptaH  
Email: rajnarayan.guptanet@gmail.com

## Installation

```bash
dotnet add package Algos.Insights
```

For local development from this repository, reference the project:

```xml
<ProjectReference Include="..\src\Algos.Insights\Algos.Insights.csproj" />
```

## Quick Start

```csharp
using Algos.Insights.Extensions;
using Algos.Insights.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAlgosInsights(options =>
{
    options.ApplicationName = "My Production API";
    options.EnvironmentName = builder.Environment.EnvironmentName;

    options.EnableAutomaticRequestLogging = true;
    options.EnableRequestBodyLogging = false;
    options.EnableResponseBodyLogging = false;
    options.MaxBodySizeInBytes = 4096;

    options.IgnoreRoutes = new[]
    {
        "/myinsights",
        "/favicon.ico"
    };

    options.Storage.UseInMemory(memory =>
    {
        memory.MaxRequestLogs = 10000;
        memory.MaxExceptionLogs = 5000;
        memory.RetentionHours = 48;
    });

    options.Dashboard.Enabled = true;
    options.Dashboard.Route = "/myinsights";
    options.Dashboard.Title = "Algos Insights";
    options.Dashboard.AuthMode = AlgosInsightsAuthMode.Basic;
    options.Dashboard.Username = builder.Configuration["AlgosInsights:Username"];
    options.Dashboard.Password = builder.Configuration["AlgosInsights:Password"];
    options.Dashboard.EnableDataExport = true;
});

var app = builder.Build();

app.UseAlgosInsights();

app.MapAlgosInsightsDashboard("/myinsights", dashboard =>
{
    dashboard.RequireBasicAuth("admin", "strong-password");
    dashboard.Title = "Algos Insights";
});

app.Run();
```

Open:

```txt
/myinsights
```

## Dashboard

The dashboard is delivered as a Razor Class Library area. Consuming applications do not need to copy views, JavaScript, or CSS files.

Routes:

```txt
/myinsights
/myinsights/requests
/myinsights/features
/myinsights/exceptions
/myinsights/dependencies
/myinsights/traces
```

Dashboard capabilities:

- Overview health score
- Request explorer
- Method, status, search, and sort controls
- Request detail drawer
- Trace ID and correlation ID copy buttons
- Request hierarchy viewer
- Most-used modules
- Least-used modules
- Feature usage counts
- Slow endpoint summary
- Dependency summary
- Exception list
- JSON and CSV exports

The dashboard does not auto-poll. It loads once and refreshes when the user clicks `Refresh`.

## What Gets Captured

Automatic request logging captures:

- HTTP method
- Path and query string
- Status code
- Duration
- Request size
- Response size
- Client IP
- User agent
- Authenticated user name when available
- Trace ID
- Span ID
- Parent span ID
- Correlation ID
- Route name
- Controller/action when available
- Endpoint metadata
- Redacted request headers
- Redacted response headers
- Optional request body
- Optional response body

Request and response body logging are disabled by default.

## Manual Telemetry

Inject `IAlgosInsightsLogger`:

```csharp
using Algos.Insights.Logging;

public sealed class PaymentService
{
    private readonly IAlgosInsightsLogger _insights;

    public PaymentService(IAlgosInsightsLogger insights)
    {
        _insights = insights;
    }

    public async Task ProcessPaymentAsync()
    {
        using var scope = _insights.BeginScope("PaymentProcessing", new
        {
            Module = "Payments",
            Feature = "StripeCheckout"
        });

        _insights.TrackFeatureUsage("Payments", "StripeCheckout", new
        {
            PaymentType = "Card",
            Currency = "USD"
        });

        _insights.TrackEvent("PaymentStarted", new
        {
            Amount = 100,
            Currency = "USD"
        });

        _insights.TrackMetric("payment.amount", 100, new
        {
            Currency = "USD"
        });

        await _insights.TrackDependencyAsync("Stripe API", "CreatePaymentIntent", async () =>
        {
            await Task.Delay(120);
        });
    }
}
```

## Feature Usage

Track features manually:

```csharp
_insights.TrackFeatureUsage("Billing", "Invoice Download");
```

Or with endpoint metadata:

```csharp
using Algos.Insights.Models;

app.MapPost("/api/payments/stripe", () => Results.Ok())
   .WithMetadata(new AlgosFeatureAttribute("Payments", "Stripe Checkout"));
```

The dashboard can then show which modules and features are used most and least.

## Storage

In-memory storage:

```csharp
options.Storage.UseInMemory(memory =>
{
    memory.MaxRequestLogs = 10000;
    memory.MaxExceptionLogs = 5000;
    memory.RetentionHours = 48;
});
```

JSON file storage:

```csharp
options.Storage.UseJsonFile(json =>
{
    json.DirectoryPath = "App_Data/AlgosInsights";
});
```

Future provider targets:

- SQL Server
- PostgreSQL
- SQLite
- MongoDB
- Elasticsearch/OpenSearch

## Exports

Exports are protected by dashboard authentication.

```txt
/myinsights/export/requests.json
/myinsights/export/requests.csv
```

Export output is limited by `Dashboard.MaxExportRows`.

## Security

Security defaults are intentionally conservative:

- Request body logging is off by default
- Response body logging is off by default
- Dashboard route is excluded from request logging
- Sensitive headers and fields are masked
- AI is disabled by default
- Export requires dashboard authentication
- Provider failures should not crash the host app

Default sensitive fields include:

```txt
password, token, authorization, cookie, set-cookie, otp, secret,
access_token, refresh_token, card, cvv, api_key, connectionstring
```

Customize redaction:

```csharp
options.Redaction.MaskFields = new[]
{
    "password",
    "token",
    "authorization",
    "cookie",
    "secret",
    "api_key"
};
```

## Health Checks

`/health` is ignored by default in the package options because most production systems call health endpoints very frequently.

To log health checks, override `IgnoreRoutes` without `/health`:

```csharp
options.IgnoreRoutes = new[]
{
    "/myinsights",
    "/favicon.ico"
};
```

## Alerts

Configure alert rules:

```csharp
options.EmailAlerts.ConfigureSmtp(smtp =>
{
    smtp.Enabled = true;
    smtp.Host = builder.Configuration["Smtp:Host"];
    smtp.Port = 587;
    smtp.Username = builder.Configuration["Smtp:Username"];
    smtp.Password = builder.Configuration["Smtp:Password"];
    smtp.From = "alerts@myapp.com";
    smtp.To = new[] { "admin@myapp.com" };
});

options.Alerts.AddRule(rule =>
{
    rule.Name = "Repeated 500 Errors";
    rule.WhenStatusCodeIs = 500;
    rule.TriggerWhenCountGreaterThan = 10;
    rule.WindowMinutes = 5;
    rule.CooldownMinutes = 20;
    rule.SendEmail = true;
});
```

## AI Assistant

AI is disabled by default. The package currently exposes the abstraction:

```csharp
public interface IAlgosAiProvider
{
    Task<AlgosAiResponse> AskAsync(
        AlgosAiRequest request,
        CancellationToken cancellationToken = default);
}
```

Planned provider targets:

- OpenAI-compatible APIs
- Azure OpenAI
- Custom HTTP endpoints
- Local LLM endpoints

AI context must be redacted before use. API keys are never displayed in the dashboard.

## Architecture

```txt
src/
  Algos.Insights/
    Areas/
      AlgosInsights/
        Pages/
          Index.cshtml
          Requests.cshtml
          Features.cshtml
          Exceptions.cshtml
          Dependencies.cshtml
          Traces.cshtml
    Dashboard/
      Assets/
      Endpoints/
      Security/
    Middleware/
    Models/
    Options/
    Logging/
    Storage/
    Export/
    Alerts/
    AI/
    Redaction/

samples/
  Algos.Insights.SampleApi/
```

## Sample API

Run the sample:

```bash
dotnet run --project samples/Algos.Insights.SampleApi/Algos.Insights.SampleApi.csproj
```

Then open:

```txt
/myinsights
```

Default sample credentials:

```txt
Username: admin
Password: strong-password
```

## Production Recommendations

- Use strong dashboard credentials
- Serve the dashboard only over HTTPS
- Keep body logging disabled unless diagnosing a specific issue
- Keep retention bounded
- Exclude noisy routes unless you really need them
- Disable exports in sensitive environments if required
- Store dashboard credentials in configuration or secret storage
- Treat AI output as advisory only

## Roadmap

- OpenTelemetry exporter integration
- Azure Application Insights provider
- Azure Monitor provider
- AWS CloudWatch provider
- SQL Server storage provider
- PostgreSQL storage provider
- SQLite storage provider
- Rich trace waterfall UI
- Alert history dashboard
- Webhook, Slack, and Teams alert providers
- AI chat provider implementations
- Role/policy-based dashboard authorization

## License

MIT
