using Algos.Insights.Models;

namespace Algos.Insights.Options;

public sealed class AlgosInsightsOptions
{
    public string ApplicationName { get; set; } = "Algos Application";
    public string EnvironmentName { get; set; } = "Production";
    public bool EnableAutomaticRequestLogging { get; set; } = true;
    public bool EnableRequestBodyLogging { get; set; }
    public bool EnableResponseBodyLogging { get; set; }
    public int MaxBodySizeInBytes { get; set; } = 4096;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableTracing { get; set; } = true;
    public bool EnableFeatureUsageTracking { get; set; } = true;
    public bool EnableDependencyTracking { get; set; } = true;
    public string[] IgnoreRoutes { get; set; } = ["/health", "/swagger", "/myinsights", "/favicon.ico"];
    public AlgosRedactionOptions Redaction { get; } = new();
    public AlgosStorageOptions Storage { get; } = new();
    public AlgosDashboardOptions Dashboard { get; } = new();
    public AlgosAlertsOptions Alerts { get; } = new();
    public AlgosEmailAlertOptions EmailAlerts { get; } = new();
    public AlgosAiOptions AI { get; } = new();
    public AlgosAzureApplicationInsightsOptions AzureApplicationInsights { get; } = new();
    public AlgosAwsCloudWatchOptions AwsCloudWatch { get; } = new();

    public void UseAzureApplicationInsights(Action<AlgosAzureApplicationInsightsOptions> configure) => configure(AzureApplicationInsights);
    public void UseAwsCloudWatch(Action<AlgosAwsCloudWatchOptions> configure) => configure(AwsCloudWatch);
}

public sealed class AlgosRedactionOptions
{
    public string[] MaskFields { get; set; } =
    [
        "password", "token", "authorization", "cookie", "set-cookie", "otp", "secret",
        "access_token", "refresh_token", "card", "cvv", "api_key", "connectionstring"
    ];

    public string MaskValue { get; set; } = "***";
}

public sealed class AlgosStorageOptions
{
    internal Action<AlgosInMemoryStorageOptions> InMemoryConfigure { get; private set; } = _ => { };
    internal Action<AlgosJsonFileStorageOptions>? JsonFileConfigure { get; private set; }

    public void UseInMemory(Action<AlgosInMemoryStorageOptions> configure) => InMemoryConfigure = configure;
    public void UseJsonFile(Action<AlgosJsonFileStorageOptions> configure) => JsonFileConfigure = configure;
}

public sealed class AlgosInMemoryStorageOptions
{
    public int MaxRequestLogs { get; set; } = 10000;
    public int MaxExceptionLogs { get; set; } = 5000;
    public int MaxEventLogs { get; set; } = 10000;
    public int MaxMetricLogs { get; set; } = 10000;
    public int MaxTraceLogs { get; set; } = 10000;
    public int MaxFeatureUsageLogs { get; set; } = 10000;
    public int RetentionHours { get; set; } = 48;
}

public sealed class AlgosJsonFileStorageOptions
{
    public string DirectoryPath { get; set; } = "App_Data/AlgosInsights";
}

public enum AlgosInsightsAuthMode { None, Basic }

public sealed class AlgosDashboardOptions
{
    public bool Enabled { get; set; }
    public string Route { get; set; } = "/myinsights";
    public string Title { get; set; } = "Algos Insights";
    public AlgosInsightsAuthMode AuthMode { get; set; } = AlgosInsightsAuthMode.Basic;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableDarkMode { get; set; } = true;
    public bool EnableDataExport { get; set; } = true;
    public bool EnableAiChat { get; set; }
    public int MaxExportRows { get; set; } = 5000;
}

public sealed class AlgosAlertsOptions
{
    public List<AlgosAlertRule> Rules { get; } = [];
    public void AddRule(Action<AlgosAlertRule> configure)
    {
        var rule = new AlgosAlertRule();
        configure(rule);
        Rules.Add(rule);
    }
}

public sealed class AlgosAlertRule
{
    public string Name { get; set; } = "";
    public AlgosSeverity? WhenSeverityIs { get; set; }
    public int? WhenStatusCodeIs { get; set; }
    public int TriggerWhenCountGreaterThan { get; set; } = 1;
    public int WindowMinutes { get; set; } = 5;
    public int CooldownMinutes { get; set; } = 15;
    public bool SendEmail { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastTriggeredUtc { get; set; }
}

public sealed class AlgosEmailAlertOptions
{
    public bool Enabled { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    public string[] To { get; set; } = [];
    public void ConfigureSmtp(Action<AlgosEmailAlertOptions> configure) => configure(this);
}

public enum AlgosAiProvider { OpenAICompatible, AzureOpenAICompatible, CustomHttp }

public sealed class AlgosAiOptions
{
    public bool Enabled { get; set; }
    public AlgosAiProvider Provider { get; set; } = AlgosAiProvider.OpenAICompatible;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? Model { get; set; }
    public bool AllowLogAnalysis { get; set; }
    public bool AllowExceptionAnalysis { get; set; }
    public bool AllowPerformanceSuggestions { get; set; }
    public bool AllowBodyContext { get; set; }
    public int MaxContextItems { get; set; } = 25;
    public void Configure(Action<AlgosAiOptions> configure) => configure(this);
}

public sealed class AlgosAzureApplicationInsightsOptions
{
    public bool Enabled { get; set; }
    public string? ConnectionString { get; set; }
    public bool UseOpenTelemetry { get; set; } = true;
}

public sealed class AlgosAwsCloudWatchOptions
{
    public bool Enabled { get; set; }
    public string? Region { get; set; }
    public string? LogGroupName { get; set; }
}
