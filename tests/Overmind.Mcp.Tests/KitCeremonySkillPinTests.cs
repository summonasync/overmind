using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class KitCeremonySkillPinTests
{
    private const string PinMatrixLine =
        "| Feature code discovery + explicit pin (read-only scan for worktrees and branches carrying the slug or its " +
        "NNN prefix; a pin `planning_pin_feature_code` overrides the convention — pinned locations list first flagged " +
        "`pinned`, convention hits follow deduplicated, a pin missing locally is reported stale, never auto-cleared) | " +
        "`planning_feature_code`, `planning_pin_feature_code` | 126, 134 |";

    [Theory]
    [InlineData(".claude")]
    [InlineData(".codex")]
    [InlineData(".kimi-code")]
    [InlineData(".qwen")]
    [InlineData(".gemini/antigravity")]
    public void CeremonySkill_CarriesPinMatrixLine(string port)
    {
        var skill = File.ReadAllText(Path.Combine(
            KitRoot(), "src", "OvermindCrewKit", port, "skills", "overmind-ceremony", "SKILL.md"));

        skill.ShouldContain(PinMatrixLine);
    }

    private static string KitRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "OvermindCrewKit")))
        {
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        dir.ShouldNotBeNull("OvermindCrewKit not found above the test directory");
        return dir;
    }
}
