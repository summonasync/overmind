using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class SessionIdentityFileWriterTests : IDisposable
{
    private static readonly Guid SessionId = Guid.Parse("5c1e0000-0000-0000-0000-000000000001");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "overmind-identity-writer", Guid.NewGuid().ToString("N"));

    private readonly string _repo;

    public SessionIdentityFileWriterTests()
    {
        _repo = Path.Combine(_root, "repo");
        Directory.CreateDirectory(Path.Combine(_repo, ".git"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private SessionIdentityFileWriter Writer(int? parentPid) =>
        new(new FixedParentPid(parentPid), _root, Workspace());

    private WorkspaceIdentityFile Workspace() => new(_root, _repo);

    private string TargetPath(int parentPid) =>
        Path.Combine(_root, "identity", $"parent-{parentPid}.json");

    [Fact]
    public void Write_CreatesTheIdentityFileWithTheContractPayload()
    {
        Writer(777).Write("van-darkholme", "claude", SessionId);

        var payload = JsonNode.Parse(File.ReadAllText(TargetPath(777)))!;
        payload["alias"]!.GetValue<string>().ShouldBe("van-darkholme");
        payload["vendor"]!.GetValue<string>().ShouldBe("claude");
        payload["sessionId"]!.GetValue<string>().ShouldBe(SessionId.ToString());
        DateTimeOffset.Parse(payload["registeredAt"]!.GetValue<string>())
            .ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Write_RecordsTheWorkspaceFileAlongsideTheParentFile()
    {
        Writer(777).Write("van-darkholme", "claude", SessionId);

        var saved = Workspace().TryRead();
        saved!.Alias.ShouldBe("van-darkholme");
        saved.Vendor.ShouldBe("claude");
        saved.SessionId.ShouldBe(SessionId);
    }

    [Fact]
    public void Write_PlaceholderIdentity_KeepsTheSavedWorkspaceIdentity()
    {
        Writer(777).Write("van-darkholme", "claude", SessionId);

        Writer(777).Write("agent", "unknown", Guid.Parse("5c1e0000-0000-0000-0000-000000000002"));

        var saved = Workspace().TryRead();
        saved!.Alias.ShouldBe("van-darkholme");
        saved.SessionId.ShouldBe(SessionId);
        JsonNode.Parse(File.ReadAllText(TargetPath(777)))!["alias"]!.GetValue<string>().ShouldBe("agent");
    }

    [Fact]
    public void Write_UnresolvableParentProcess_WritesOnlyTheWorkspaceFile()
    {
        Writer(null).Write("van-darkholme", "claude", SessionId);

        Workspace().TryRead()!.Alias.ShouldBe("van-darkholme");
        Directory.GetFiles(Path.Combine(_root, "identity"), "parent-*.json").ShouldBeEmpty();
    }

    [Fact]
    public void Write_RewritesTheExistingFile_WithTheNewIdentity()
    {
        Writer(777).Write("before", "claude", SessionId);
        Writer(777).Write("after", "qwen", SessionId);

        var payload = JsonNode.Parse(File.ReadAllText(TargetPath(777)))!;
        payload["alias"]!.GetValue<string>().ShouldBe("after");
        payload["vendor"]!.GetValue<string>().ShouldBe("qwen");
        Directory.GetFiles(Path.Combine(_root, "identity"), "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public void Write_MoveFailure_CleansTheTempFile_AndDoesNotThrow()
    {
        Directory.CreateDirectory(TargetPath(777));

        Should.NotThrow(() => Writer(777).Write("van-darkholme", "claude", SessionId));

        Directory.GetFiles(Path.Combine(_root, "identity"), "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public void Write_UnusableRoot_DoesNotThrow()
    {
        var brokenRoot = Path.Combine(Path.GetTempPath(), $"overmind-identity-broken-{Guid.NewGuid():N}");
        File.WriteAllText(brokenRoot, "not a directory");

        try
        {
            Should.NotThrow(() => new SessionIdentityFileWriter(
                new FixedParentPid(777), brokenRoot, new WorkspaceIdentityFile(brokenRoot, _repo))
                .Write("van-darkholme", "claude", SessionId));
        }
        finally
        {
            File.Delete(brokenRoot);
        }
    }

    private sealed class FixedParentPid(int? pid) : IParentProcessIdSource
    {
        public int? Resolve() => pid;
    }
}
