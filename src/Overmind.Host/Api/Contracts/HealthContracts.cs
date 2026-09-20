using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record HealthResponse(string Status, VectorSearchHealthReport VectorSearch, string Version);
