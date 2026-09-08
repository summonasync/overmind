namespace Overmind.Host.Api.Contracts;

public sealed record BrowseFolderResponse(string Path);

public sealed record TreeRootResponse(string Name, string Path);

public sealed record TreeRootsResponse(TreeRootResponse[] Roots);

public sealed record DirectoryEntryResponse(string Name, string Path, bool Accessible, Guid? ProjectId, string? ProjectName);

public sealed record ListDirectoryResponse(string Path, bool Truncated, DirectoryEntryResponse[] Entries);
