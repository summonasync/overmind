namespace Overmind.Core;

public sealed record PlanningGitHygieneOutcome(string Status, string? RepositoryRoot = null, string? Detail = null);
