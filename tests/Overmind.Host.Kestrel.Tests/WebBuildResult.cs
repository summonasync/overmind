namespace Overmind.Host.Kestrel.Tests;

public sealed record WebBuildResult(bool Succeeded, string LogPath, string? Error);
