namespace Overmind.Mcp;

public sealed class SessionIdentityFileWriter(
    IParentProcessIdSource parentProcessId,
    string? rootDirectory = null,
    WorkspaceIdentityFile? workspaceIdentity = null)
{
    private readonly WorkspaceIdentityFile _workspaceIdentity =
        workspaceIdentity ?? new WorkspaceIdentityFile(rootDirectory);

    public void Write(string alias, string vendor, Guid sessionId)
    {
        var snapshot = new SessionIdentitySnapshot(alias, vendor, sessionId, DateTime.UtcNow);
        _workspaceIdentity.Write(snapshot);

        if (parentProcessId.Resolve() is not { } parentPid)
        {
            return;
        }

        SessionIdentityFiles.Write(
            Path.Combine(SessionIdentityFiles.IdentityDirectory(rootDirectory), $"parent-{parentPid}.json"),
            snapshot);
    }
}
