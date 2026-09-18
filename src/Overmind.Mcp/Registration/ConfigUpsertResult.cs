namespace Overmind.Mcp.Registration;

public sealed record ConfigUpsertResult
{
    public required ConfigUpsertOutcome Outcome { get; init; }
    public string? Error { get; init; }

    public static ConfigUpsertResult Success(ConfigUpsertOutcome outcome)
    {
        return new ConfigUpsertResult { Outcome = outcome };
    }

    public static ConfigUpsertResult Failure(string error)
    {
        return new ConfigUpsertResult { Outcome = ConfigUpsertOutcome.Failed, Error = error };
    }
}
