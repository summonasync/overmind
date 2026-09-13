namespace Overmind.Infrastructure.Planning;

public sealed record BacklogImportRequest(string BacklogPath, Guid ProjectId);
