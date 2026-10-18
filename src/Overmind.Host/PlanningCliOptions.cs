namespace Overmind.Host;

internal sealed record PlanningCliOptions(string? SessionId, string Alias, string Vendor)
{
    public const string DefaultAlias = "agent";
    public const string DefaultVendor = "unknown";

    public static PlanningCliOptions FromEnvironment() => new(
        Trimmed(Environment.GetEnvironmentVariable("OVERMIND_SESSION_ID")),
        Trimmed(Environment.GetEnvironmentVariable("OVERMIND_ALIAS")) ?? DefaultAlias,
        Trimmed(Environment.GetEnvironmentVariable("OVERMIND_VENDOR")) ?? DefaultVendor);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
