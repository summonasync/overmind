namespace Overmind.Mcp;

public sealed record ProjectPin(Guid? ProjectId, string? FilePath)
{
    public bool IsUnparsable => ProjectId is null && FilePath is not null;
}
