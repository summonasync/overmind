namespace Overmind.Infrastructure.Planning;

public static class RelocationCompanionSteps
{
    public const string GrimoireRegistry =
        "grimoire projects registry — repoint the project alias at the new absolute path (projects Add/Update); " +
        "Overmind never writes a foreign store.";

    public const string SqueezrWorkspace =
        "squeezr workspace — re-add and re-index the moved repo (squeezr add <new-path> --name <repo>, then squeezr index).";

    public static IReadOnlyList<string> All { get; } = [GrimoireRegistry, SqueezrWorkspace];

    public static string DescribeNotification(
        string projectName, string previousDiskPath, string newDiskPath, int pinsRemapped) =>
        $"Project '{projectName}' relocated from '{previousDiskPath}' to '{newDiskPath}' with {pinsRemapped} " +
        $"worktree pin(s) remapped. Manual steps outside overmind.db: {GrimoireRegistry} | {SqueezrWorkspace}";
}
