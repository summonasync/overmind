namespace Overmind.Host.Api.Contracts;

public sealed record HealthResponse(string Status, string VectorSearch, string Version);
