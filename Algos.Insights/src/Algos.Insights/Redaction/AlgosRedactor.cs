using System.Reflection;
using Algos.Insights.Options;
using Microsoft.Extensions.Options;

namespace Algos.Insights.Redaction;

public sealed class AlgosRedactor : IAlgosRedactor
{
    private readonly AlgosInsightsOptions _options;

    public AlgosRedactor(IOptions<AlgosInsightsOptions> options) => _options = options.Value;

    public string Redact(string? key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return IsSensitive(key) ? _options.Redaction.MaskValue : value;
    }

    public Dictionary<string, string> Redact(IDictionary<string, string> values) =>
        values.ToDictionary(k => k.Key, v => Redact(v.Key, v.Value), StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, object?> RedactObject(object? value)
    {
        if (value is null)
        {
            return [];
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(k => k.Key, v => RedactValue(v.Key, v.Value), StringComparer.OrdinalIgnoreCase);
        }

        return value.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name, p => RedactValue(p.Name, p.GetValue(value)), StringComparer.OrdinalIgnoreCase);
    }

    private object? RedactValue(string key, object? value) => IsSensitive(key) ? _options.Redaction.MaskValue : value;

    private bool IsSensitive(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        _options.Redaction.MaskFields.Any(mask => key.Contains(mask, StringComparison.OrdinalIgnoreCase));
}
