namespace Overmind.Host.Api.Contracts;

public sealed record TreeRootResponse(string Name, string Path, bool ContainsProjects, IReadOnlyList<string> ProjectNames);

public sealed record TreeRootsResponse(TreeRootResponse[] Roots);

public sealed record AddTreeRootRequest(string Path);

public sealed record DirectoryEntryResponse(string Name, string Path, bool Accessible, Guid? ProjectId, string? ProjectName);

public sealed record ListDirectoryResponse(string Path, bool Truncated, DirectoryEntryResponse[] Entries, bool Accessible);

public sealed record BrowseRootEntry(string Name, string Path, string Kind);

public sealed record BrowseRootsResponse(BrowseRootEntry[] Roots);
