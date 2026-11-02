namespace Overmind.Infrastructure.Grimoire;

public enum GrimoireCompactionOutcome
{
    Completed,
    GrimoireUnavailable,
    ProviderMissing,
    CandidateSourceFailed
}

public sealed record GrimoireCompactionPassResult(
    GrimoireCompactionOutcome Outcome,
    string? Reason,
    int GroupsSeen,
    int FactsWritten,
    int EntriesForgotten,
    int GroupsSkipped)
{
    public static GrimoireCompactionPassResult Failed(GrimoireCompactionOutcome outcome, string reason) =>
        new(outcome, reason, 0, 0, 0, 0);
}
