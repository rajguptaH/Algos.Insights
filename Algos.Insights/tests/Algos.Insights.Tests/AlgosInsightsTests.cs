using System.Net;
using System.Text;
using Algos.Insights.Extensions;
using Algos.Insights.Logging;
using Algos.Insights.Models;
using Algos.Insights.Redaction;
using Algos.Insights.Storage.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Algos.Insights.Tests;

public sealed class AlgosInsightsTests
{
    [Fact]
    public void AddAlgosInsights_registers_core_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAlgosInsights();
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IAlgosInsightsLogger>());
        Assert.NotNull(provider.GetRequiredService<IAlgosInsightsStore>());
        Assert.NotNull(provider.GetRequiredService<IAlgosRedactor>());
    }

    [Fact]
    public void Redactor_masks_sensitive_values()
    {
        var services = new ServiceCollection();
        services.AddAlgosInsights();
        using var provider = services.BuildServiceProvider();
        var redactor = provider.GetRequiredService<IAlgosRedactor>();
        var values = redactor.Redact(new Dictionary<string, string> { ["Authorization"] = "Bearer abc", ["X-Test"] = "ok" });
        Assert.Equal("***", values["Authorization"]);
        Assert.Equal("ok", values["X-Test"]);
    }

    [Fact]
    public async Task InMemory_store_paginates_requests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAlgosInsights();
        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAlgosInsightsStore>();
        for (var i = 0; i < 3; i++)
        {
            await store.SaveRequestAsync(new AlgosRequestLog { Method = "GET", Path = "/" + i, StatusCode = 200 });
        }

        var page = await store.GetRequestsAsync(new AlgosQuery { Page = 1, PageSize = 2 });
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task Middleware_logs_request_and_preserves_response()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAlgosInsights(o => o.IgnoreRoutes = []);
        var app = builder.Build();
        app.UseAlgosInsights();
        app.MapGet("/hello", () => "world");
        await app.StartAsync();
        try
        {
            var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            var response = await client.GetAsync("/hello");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("world", await response.Content.ReadAsStringAsync());
            var store = app.Services.GetRequiredService<IAlgosInsightsStore>();
            var page = await store.GetRequestsAsync(new AlgosQuery());
            Assert.Contains(page.Items, x => x.Path == "/hello");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Dashboard_requires_basic_auth()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAlgosInsights(o =>
        {
            o.Dashboard.Enabled = true;
            o.Dashboard.Username = "admin";
            o.Dashboard.Password = "pw";
        });
        var app = builder.Build();
        app.MapAlgosInsightsDashboard("/myinsights");
        await app.StartAsync();
        try
        {
            var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/myinsights")).StatusCode);
            var request = new HttpRequestMessage(HttpMethod.Get, "/myinsights");
            request.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:pw")));
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
