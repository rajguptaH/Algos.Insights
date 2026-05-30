using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Algos.Insights.Options;
using Microsoft.AspNetCore.Http;

namespace Algos.Insights.Dashboard.Security;

public static class AlgosDashboardAuth
{
    public static bool IsAuthorized(HttpContext context, AlgosDashboardOptions options)
    {
        if (options.AuthMode == AlgosInsightsAuthMode.None)
        {
            return true;
        }

        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var header) ||
            !string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(header.Parameter))
        {
            return false;
        }

        var bytes = Convert.FromBase64String(header.Parameter);
        var parts = Encoding.UTF8.GetString(bytes).Split(':', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        var username = options.Username ?? "admin";
        var password = options.Password ?? "strong-password";
        var ok = FixedTimeEquals(parts[0], username) && FixedTimeEquals(parts[1], password);
        if (ok)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, username)], "AlgosBasic"));
        }

        return ok;
    }

    public static void Challenge(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"Algos Insights\"";
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
