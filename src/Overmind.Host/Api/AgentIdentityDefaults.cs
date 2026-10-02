namespace Overmind.Host.Api;

public static class AgentIdentityDefaults
{
    public const string Alias = "agent";
    public const string Vendor = "unknown";

    public const string GenericNamingHint =
        "No naming rules are set for this project — invent an alias (max 32 chars) and pass it " +
        "with your vendor when calling planning_claim_feature. The pair alias+vendor is display-only " +
        "(collisions are a UX smell, not an error) and binds the claim to your adapter session.";
}
