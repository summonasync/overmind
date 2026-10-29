namespace Overmind.Mcp;

public sealed record SessionIdentitySnapshot(string Alias, string Vendor, Guid SessionId, DateTime RegisteredAt);
