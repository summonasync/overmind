using Shouldly;

namespace Overmind.Mcp.Tests;

internal static class ContractsDoc
{
    private const string DocSource = "docs/mcp-tool-contracts.md";
    private static string? _text;

    public static string Section(string toolName)
    {
        var text = _text ??= File.ReadAllText(DocPath());
        var anchor = $"\n## {toolName}\n";
        var start = text.IndexOf(anchor, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0,
            $"{toolName} has no anchor section in {DocSource} — its Tier-2 prose has no home to assert against");

        var rest = text[(start + anchor.Length)..];
        var following = rest.IndexOf("\n## ", StringComparison.Ordinal);
        var section = ReFlow(following < 0 ? rest : rest[..following]);

        section.Length.ShouldBeGreaterThan(0, $"the {toolName} section of {DocSource} is empty");
        return section;
    }

    public static string Preamble()
    {
        var text = _text ??= File.ReadAllText(DocPath());
        return ReFlow(text[..text.IndexOf("\n## ", StringComparison.Ordinal)]);
    }

    // Markdown wraps paragraphs at column boundaries and leans on `emphasis`; assertions quote
    // the prose, not the rendering.
    private static string ReFlow(string raw) =>
        string.Join(' ', raw.Replace("`", string.Empty)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    private static string DocPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, DocSource)))
        {
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        dir.ShouldNotBeNull($"{DocSource} not found above the test output directory");
        return Path.Combine(dir!, DocSource);
    }
}
