namespace Algos.Insights.Redaction;

public interface IAlgosRedactor
{
    string Redact(string? key, string? value);
    Dictionary<string, string> Redact(IDictionary<string, string> values);
    Dictionary<string, object?> RedactObject(object? value);
}
