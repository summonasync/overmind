namespace Overmind.Mcp.Registration;

public static class OnboardBlockContent
{
    private const string UnknownProjectName = "unknown";
    private const string UnresolvedProjectId = "resolved on first overmind_onboard call";

    public static string Render(OnboardProjectInfo? project)
    {
        var name = project?.ProjectName ?? UnknownProjectName;
        var id = project?.ProjectId ?? UnresolvedProjectId;

        return
            $"This repository is Overmind project '{name}' (id: {id}). " +
            "Planning and shared context go through the overmind MCP tools — call `overmind_onboard` first. " +
            "This block overrides any file-based ceremony instructions in this file." +
            "\n\n" +
            "Escape hatch: set OVERMIND_PROJECT_ID to a valid project guid to skip cwd matching " +
            "for symlinked or worktree layouts where the resolved cwd differs from the registered project path.";
    }
}
