namespace Overmind.Mcp;

public static class ApiBaseUrl
{
    public const string DefaultBaseUrl = "http://127.0.0.1:4761";

    public static string Resolve()
    {
        var fromEnv = Environment.GetEnvironmentVariable("OVERMIND_API_URL");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim().TrimEnd('/');
        }

        var fromPortFile = ResolveFromPortFile();
        if (fromPortFile is not null)
        {
            return fromPortFile;
        }

        return DefaultBaseUrl;
    }

    private static string? ResolveFromPortFile()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".overmind",
                "host.port");

            if (!File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path).Trim();
            return int.TryParse(text, out var port) && port > 0 && port <= 65535
                ? $"http://127.0.0.1:{port}"
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
