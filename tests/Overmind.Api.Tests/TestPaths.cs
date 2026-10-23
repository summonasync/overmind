namespace Overmind.Api.Tests;

internal static class TestPaths
{
    public static string DiskPath(string name) => Path.Combine(Path.GetTempPath(), "overmind-test", name);

    public static string RegisteredDiskPath(string name) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "overmind-test", name);
}
