using Algos.Insights.Extensions;
using Algos.Insights.Logging;
using Algos.Insights.Models;
using Algos.Insights.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAlgosInsights(options =>
{
    options.ApplicationName = "Algos Insights Sample API";
    options.EnvironmentName = builder.Environment.EnvironmentName;
    options.EnableAutomaticRequestLogging = true;
    options.EnableMetrics = true;
    options.EnableTracing = true;
    options.EnableFeatureUsageTracking = true;
    options.IgnoreRoutes = ["/myinsights", "/favicon.ico"];

    options.Storage.UseInMemory(memory =>
    {
        memory.MaxRequestLogs = 10000;
        memory.MaxExceptionLogs = 5000;
        memory.RetentionHours = 48;
    });

    options.Alerts.AddRule(rule =>
    {
        rule.Name = "Critical Exceptions";
        rule.WhenSeverityIs = AlgosSeverity.Error;
        rule.CooldownMinutes = 15;
        rule.SendEmail = true;
    });

    options.AI.Configure(ai =>
    {
        ai.Enabled = false;
        ai.MaxContextItems = 25;
    });

    options.Dashboard.Enabled = true;
    options.Dashboard.Route = "/myinsights";
    options.Dashboard.Title = "Algos Insights";
    options.Dashboard.AuthMode = AlgosInsightsAuthMode.Basic;
    options.Dashboard.Username = "admin";
    options.Dashboard.Password = "strong-password";
    options.Dashboard.EnableDarkMode = true;
    options.Dashboard.EnableDataExport = true;
});

var app = builder.Build();

app.UseAlgosInsights();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/orders/{id:int}", (int id, IAlgosInsightsLogger insights) =>
{
    using var span = insights.StartActivity("LoadOrder", new { Module = "Orders", OrderId = id });
    insights.TrackFeatureUsage("Orders", "Order Details", new { OrderId = id });
    insights.TrackMetric("orders.detail.opened", 1, new { Module = "Orders" });
    return Results.Ok(new { id, status = "Paid", total = 129.50m });
}).WithMetadata(new AlgosFeatureAttribute("Orders", "Order Details"));

app.MapPost("/api/payments/stripe", async (IAlgosInsightsLogger insights) =>
{
    using var scope = insights.BeginScope("PaymentProcessing", new { Module = "Payments", Feature = "StripeCheckout" });
    insights.TrackEvent("PaymentStarted", new { Amount = 100, Currency = "USD" });
    await insights.TrackDependencyAsync("Stripe API", "CreatePaymentIntent", async () => await Task.Delay(120));
    insights.TrackEvent("PaymentCompleted");
    return Results.Ok(new { status = "completed" });
}).WithMetadata(new AlgosFeatureAttribute("Payments", "Stripe Checkout"));

app.MapGet("/api/fail", (IAlgosInsightsLogger insights) =>
{
    try
    {
        throw new InvalidOperationException("Sample failure for dashboard testing.");
    }
    catch (Exception ex)
    {
        insights.TrackException(ex, new { Module = "Diagnostics", Severity = "Critical" });
        throw;
    }
});

app.MapAlgosInsightsDashboard("/myinsights", dashboard =>
{
    dashboard.RequireBasicAuth("admin", "strong-password");
    dashboard.Title = "Algos Insights";
});

app.Run();

public partial class Program;
