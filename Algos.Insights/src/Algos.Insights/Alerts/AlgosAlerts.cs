using Algos.Insights.Models;
using Algos.Insights.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Algos.Insights.Alerts;

public interface IAlgosAlertService
{
    Task EvaluateRequestAsync(AlgosRequestLog request, CancellationToken cancellationToken = default);
    Task EvaluateExceptionAsync(AlgosExceptionLog exception, CancellationToken cancellationToken = default);
}

public sealed class AlgosAlertService : IAlgosAlertService
{
    private readonly AlgosInsightsOptions _options;
    private readonly ILogger<AlgosAlertService> _logger;

    public AlgosAlertService(IOptions<AlgosInsightsOptions> options, ILogger<AlgosAlertService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task EvaluateRequestAsync(AlgosRequestLog request, CancellationToken cancellationToken = default)
    {
        foreach (var rule in _options.Alerts.Rules.Where(x => x.Enabled && x.WhenStatusCodeIs == request.StatusCode))
        {
            await TriggerAsync(rule, $"Request alert: {request.Method} {request.Path} returned {request.StatusCode}", cancellationToken);
        }
    }

    public async Task EvaluateExceptionAsync(AlgosExceptionLog exception, CancellationToken cancellationToken = default)
    {
        foreach (var rule in _options.Alerts.Rules.Where(x => x.Enabled && x.WhenSeverityIs == exception.Severity))
        {
            await TriggerAsync(rule, $"Exception alert: {exception.ExceptionType} {exception.Message}", cancellationToken);
        }
    }

    private async Task TriggerAsync(AlgosAlertRule rule, string message, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (rule.LastTriggeredUtc.HasValue && now - rule.LastTriggeredUtc.Value < TimeSpan.FromMinutes(rule.CooldownMinutes))
        {
            return;
        }

        rule.LastTriggeredUtc = now;
        _logger.LogWarning("{RuleName}: {Message}", rule.Name, message);
        if (rule.SendEmail)
        {
            await SendEmailAsync(rule.Name, message, cancellationToken);
        }
    }

    private async Task SendEmailAsync(string subject, string body, CancellationToken cancellationToken)
    {
        var email = _options.EmailAlerts;
        if (!email.Enabled || string.IsNullOrWhiteSpace(email.Host) || string.IsNullOrWhiteSpace(email.From) || email.To.Length == 0)
        {
            return;
        }

        try
        {
            using var client = new SmtpClient(email.Host, email.Port)
            {
                EnableSsl = true
            };

            if (!string.IsNullOrWhiteSpace(email.Username))
            {
                client.Credentials = new NetworkCredential(email.Username, email.Password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(email.From),
                Subject = "[Algos.Insights] " + subject,
                Body = body
            };

            foreach (var recipient in email.To)
            {
                message.To.Add(recipient);
            }

            using var registration = cancellationToken.Register(client.SendAsyncCancel);
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Algos.Insights SMTP alert delivery failed.");
        }
    }
}
