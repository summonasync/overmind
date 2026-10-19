namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireEngineRunResult(
    bool Started,
    int? ExitCode,
    string StandardOutput,
    string StandardError)
{
    public static readonly GrimoireEngineRunResult NotStarted = new(false, null, string.Empty, string.Empty);

    public bool Succeeded => Started && ExitCode == 0;
}
