namespace Overmind.Api.Tests;

internal static class TestPaths
{
    public static string DiskPath(string name) => Path.Combine(Path.GetTempPath(), "overmind-test", name);
}
