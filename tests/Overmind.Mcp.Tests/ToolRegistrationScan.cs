using Shouldly;

namespace Overmind.Mcp.Tests;

internal static class ToolRegistrationScan
{
    private const string ProgramSource = "src/Overmind.Mcp/McpProfileRegistration.cs";
    private const string RegistrationMarker = ".WithTools<";

    public static IReadOnlyList<string> RegisteredToolTypeNames()
    {
        var text = File.ReadAllText(ProgramPath());
        var names = new List<string>();
        var cursor = 0;

        while ((cursor = text.IndexOf(RegistrationMarker, cursor, StringComparison.Ordinal)) >= 0)
        {
            var open = text.IndexOf('<', cursor);
            var close = open < 0 ? -1 : text.IndexOf('>', open);
            close.ShouldBeGreaterThan(open, $"unparsable {RegistrationMarker} at offset {cursor} of {ProgramSource}");
            names.Add(text[(open + 1)..close]);
            cursor = close;
        }

        return names;
    }

    private static string ProgramPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, ProgramSource)))
        {
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        dir.ShouldNotBeNull($"{ProgramSource} not found above the test output directory");
        return Path.Combine(dir!, ProgramSource);
    }
}
