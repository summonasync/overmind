namespace Overmind.Host;

public static class OvermindPaths
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".overmind");

    public static string DatabasePath => Path.Combine(DataDirectory, "overmind.db");

    public static string PortFilePath => Path.Combine(DataDirectory, "host.port");
}
