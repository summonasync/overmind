namespace Overmind.Host.Api;

public static class SessionHeader
{
    public const string Name = "X-Overmind-Session";

    public static bool TryRead(HttpRequest request, out Guid sessionId)
    {
        sessionId = default;
        if (!request.Headers.TryGetValue(Name, out var raw) || raw.Count == 0)
        {
            return false;
        }

        return Guid.TryParse(raw[^1], out sessionId);
    }
}
