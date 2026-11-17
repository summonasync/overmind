using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class WorkspaceIdentityFileTests : IDisposable
{
    private static readonly Guid SessionId = Guid.Parse("5c1e0000-0000-0000-0000-000000000005");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "overmind-workspace-identity", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WorkspaceIdentityFile IdentityFile(string repo) => new(_root, repo);

    private string Repo(string name)
    {
        var repo = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        return repo;
    }

    private string IdentityDirectory() => Path.Combine(_root, "identity");

    [Fact]
    public void Write_ThenRead_RoundTripsTheSnapshot()
    {
        var repo = Repo("alpha");
        var identity = IdentityFile(repo);

        identity.Write(new SessionIdentitySnapshot("mark-wolff", "claude", SessionId, DateTime.UtcNow));

        var saved = identity.TryRead();
        saved!.Alias.ShouldBe("mark-wolff");
        saved.Vendor.ShouldBe("claude");
        saved.SessionId.ShouldBe(SessionId);
        saved.RegisteredAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void TryRead_NoFileWritten_ReturnsNull()
    {
        IdentityFile(Repo("alpha")).TryRead().ShouldBeNull();
    }

    [Fact]
    public void TryRead_PlaceholderAlias_ReturnsNull()
    {
        var identity = IdentityFile(Repo("alpha"));
        identity.Write(new SessionIdentitySnapshot("agent", "claude", SessionId, DateTime.UtcNow));

        identity.TryRead().ShouldBeNull();
    }

    [Fact]
    public void TryRead_BlankVendor_ReturnsNull()
    {
        var identity = IdentityFile(Repo("alpha"));
        identity.Write(new SessionIdentitySnapshot("mark-wolff", "claude", SessionId, DateTime.UtcNow));
        File.WriteAllText(
            IdentityFilePath(),
            """{"alias":"mark-wolff","vendor":"  ","sessionId":"5c1e0000-0000-0000-0000-000000000005"}""");

        identity.TryRead().ShouldBeNull();
    }

    [Fact]
    public void TryRead_CorruptPayload_ReturnsNull()
    {
        var identity = IdentityFile(Repo("alpha"));
        identity.Write(new SessionIdentitySnapshot("mark-wolff", "claude", SessionId, DateTime.UtcNow));
        File.WriteAllText(IdentityFilePath(), "not even json");

        identity.TryRead().ShouldBeNull();
    }

    [Fact]
    public void Write_LeavesNoTempFileBehind()
    {
        var identity = IdentityFile(Repo("alpha"));

        identity.Write(new SessionIdentitySnapshot("mark-wolff", "claude", SessionId, DateTime.UtcNow));

        Directory.GetFiles(IdentityDirectory(), "*.tmp").ShouldBeEmpty();
        Directory.GetFiles(IdentityDirectory(), "workspace-*.json").Length.ShouldBe(1);
    }

    [Fact]
    public void Read_FromANestedWorkingDirectory_FindsTheRepositoryRootIdentity()
    {
        var repo = Repo("alpha");
        IdentityFile(repo).Write(new SessionIdentitySnapshot("mark-wolff", "claude", SessionId, DateTime.UtcNow));

        var saved = IdentityFile(Path.Combine(repo, "src", "deep")).TryRead();

        saved!.Alias.ShouldBe("mark-wolff");
        Directory.GetFiles(IdentityDirectory(), "workspace-*.json").Length.ShouldBe(1);
    }

    [Fact]
    public void Read_OtherRepositoryWorkspace_DoesNotSeeTheIdentity()
    {
        var saved = IdentityFile(Repo("beta"));
        IdentityFile(Repo("alpha")).Write(new SessionIdentitySnapshot("mark-wolff", "claude", SessionId, DateTime.UtcNow));

        saved.TryRead().ShouldBeNull();
    }

    [Fact]
    public void Write_SecondRegistrationOfTheSameWorkspace_ReplacesTheIdentity()
    {
        var identity = IdentityFile(Repo("alpha"));
        identity.Write(new SessionIdentitySnapshot("mark-wolff", "claude", SessionId, DateTime.UtcNow));
        identity.Write(new SessionIdentitySnapshot(
            "boss-of-the-gym", "qwen", Guid.Parse("5c1e0000-0000-0000-0000-000000000006"), DateTime.UtcNow));

        var payload = JsonNode.Parse(File.ReadAllText(IdentityFilePath()))!;
        payload["alias"]!.GetValue<string>().ShouldBe("boss-of-the-gym");
        payload["vendor"]!.GetValue<string>().ShouldBe("qwen");
    }

    private string IdentityFilePath() =>
        Directory.GetFiles(IdentityDirectory(), "workspace-*.json").Single();
}
