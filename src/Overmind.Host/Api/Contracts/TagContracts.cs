namespace Overmind.Host.Api.Contracts;

public sealed record TagResponse(Guid Id, string Name, string? Color);

public sealed record UpdateTagRequest(string? Color);
