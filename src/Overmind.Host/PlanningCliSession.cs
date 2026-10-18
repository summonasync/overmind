using System.Text.Json;

namespace Overmind.Host;

internal sealed class PlanningCliSession(PlanningCliClient client, PlanningCliOptions options)
{
    public const string SessionsPath = "/api/agent/sessions";

    public string Alias => options.Alias;

    public string Vendor => options.Vendor;

    public async Task ResolveAsync(bool required, CancellationToken ct)
    {
        if (client.SessionId is not null)
        {
            return;
        }

        if (Guid.TryParse(options.SessionId, out var fromEnvironment))
        {
            client.SessionId = fromEnvironment;
            return;
        }

        if (!required)
        {
            return;
        }

        var response = await client.PostAsync(
            SessionsPath, new SessionRegistration(options.Alias, options.Vendor), ct);
        if (string.IsNullOrEmpty(response.Body))
        {
            throw new PlanningCliApiException(
                response.StatusCode, "Session registration returned no session id — a draft mutation needs an agent session.");
        }

        using var document = JsonDocument.Parse(response.Body);
        if (document.RootElement.TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.String
            && Guid.TryParse(id.GetString(), out var sessionId))
        {
            client.SessionId = sessionId;
            return;
        }

        throw new PlanningCliApiException(
            response.StatusCode, "Session registration returned no session id — a draft mutation needs an agent session.");
    }

    private sealed record SessionRegistration(string Alias, string Vendor);
}
