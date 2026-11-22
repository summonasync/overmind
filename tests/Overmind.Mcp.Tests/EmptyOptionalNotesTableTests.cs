using System.Text.Json;
using Overmind.Mcp.ToolBinding;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

// 266/T3 row 3, fix round after review-266-04 (BLOCKER): the table asserted a default that the door
// never had — "overmind_replies.since → replies from the last 7 days", while
// NotificationsService.cs:119-138 filters by since ONLY when it is present. A param table that
// restates door behaviour is a rot surface, so the row is dropped and only value substitutions stay
// nameable. These pins are what make the mistake hard to repeat: the note text is pinned against the
// code it describes, at the line the row cites.
public class EmptyOptionalNotesTableTests
{
    [Fact]
    public void Rows_ExistOnlyWhereAValueIsActuallySubstituted()
    {
        EmptyOptionalDefaultNotes.Lookup("search", "mode").ShouldBe("mode=fulltext");
        EmptyOptionalDefaultNotes.Lookup("planning_create_feature", "slug")
            .ShouldBe("the generated '{NNN}-{title-derived}' slug");
    }

    // The anti-rot half of row 3: `since` has no default to name — the door answers with an
    // unbounded window capped at NotificationsService.ListCapacity rows, newest first. Naming that
    // as a "default" is exactly what went wrong once, so the generic note must say what happens
    // (dropped, as if omitted) and must never smuggle a window or a day count back in.
    [Fact]
    public void Since_HasNoRow_AndTheGenericNoteClaimsNoValue()
    {
        EmptyOptionalDefaultNotes.Lookup("overmind_replies", "since").ShouldBeNull();

        var note = EmptyOptionalDefaultNotes.Note("since", null);
        note.ShouldContain("'since' arrived empty and was dropped");
        note.ShouldContain("the call goes out as if you had omitted it");
        note.ShouldNotContain("days");
        note.ShouldNotContain("week");
        note.ShouldNotContain("bound");
    }

    [Theory]
    [InlineData("src/Overmind.Mcp/OvermindTools.cs", 360, "string.IsNullOrWhiteSpace(mode) ? \"fulltext\"")]
    [InlineData("src/Overmind.Mcp/OvermindTools.cs", 362, "mode={Uri.EscapeDataString(effectiveMode)")]
    [InlineData("src/Overmind.Mcp/Planning/PlanningFeatureTools.cs", 170, "!string.IsNullOrWhiteSpace(slug)")]
    [InlineData("src/Overmind.Infrastructure/Planning/PlanningFeatureService.cs", 69, "string.IsNullOrWhiteSpace(slug)")]
    [InlineData("src/Overmind.Infrastructure/Planning/PlanningFeatureService.cs", 71, "FeatureSlugGenerator.Compose")]
    public void CitedLines_StillSayWhatTheTableClaims(string repoPath, int line, string expected)
    {
        var lines = File.ReadAllLines(RepoFile(repoPath));

        // A failing line here means the cited code moved or changed shape: re-read the site and
        // decide the row again — the check row 3 skipped for overmind_replies.since.
        lines[line - 1].ShouldContain(expected);
    }

    private static string RepoFile(string relative)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Overmind.sln")))
        {
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        dir.ShouldNotBeNull($"{relative} not reachable above the test output directory");
        return Path.Combine(dir!, relative);
    }
}
