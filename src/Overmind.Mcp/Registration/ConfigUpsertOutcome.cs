namespace Overmind.Mcp.Registration;

public enum ConfigUpsertOutcome
{
    Added = 0,
    Updated = 1,
    Unchanged = 2,
    Removed = 3,
    NotPresent = 4,
    Failed = 5,
}
