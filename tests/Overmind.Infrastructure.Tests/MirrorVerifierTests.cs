using System.Security.Cryptography;
using System.Text;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class MirrorVerifierTests : IDisposable
{
    private readonly string _sandboxDir;

    public MirrorVerifierTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-mirror-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sandboxDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task Verify_EveryFileMatchesRow_Passes()
    {
        var root = CreateDirectory("candidate");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "a.md"), "alpha\n");
        await File.WriteAllTextAsync(Path.Combine(root, "b.md"), "beta\n");
        var rows = new[]
        {
            new MirrorFileRow("docs/a.md", Sha256Of("alpha\n")),
            new MirrorFileRow("b.md", Sha256Of("beta\n")),
        };

        var result = await MirrorVerifier.VerifyAsync(root, rows, CancellationToken.None);

        result.Passed.ShouldBeTrue();
        result.Total.ShouldBe(2);
        result.Drifted.ShouldBeEmpty();
        result.Missing.ShouldBeEmpty();
    }

    [Fact]
    public async Task Verify_DriftedAndMissingFiles_ListedSeparately()
    {
        var root = CreateDirectory("candidate");
        await File.WriteAllTextAsync(Path.Combine(root, "kept.md"), "unchanged\n");
        await File.WriteAllTextAsync(Path.Combine(root, "edited.md"), "hand edit\n");
        var rows = new[]
        {
            new MirrorFileRow("kept.md", Sha256Of("unchanged\n")),
            new MirrorFileRow("edited.md", Sha256Of("original\n")),
            new MirrorFileRow("gone.md", Sha256Of("gone\n")),
        };

        var result = await MirrorVerifier.VerifyAsync(root, rows, CancellationToken.None);

        result.Passed.ShouldBeFalse();
        result.Total.ShouldBe(3);
        result.Drifted.ShouldHaveSingleItem().ShouldBe("edited.md");
        result.Missing.ShouldHaveSingleItem().ShouldBe("gone.md");
    }

    [Fact]
    public async Task Verify_BacklogIndexRow_ComparesManagedBlockInsteadOfWholeFile()
    {
        var root = CreateDirectory("candidate");
        var block = "feature: 031-mirror\n- spec kept here";
        var backlog = $"<repo intro>\n\n{ManagedBlock.StartMarker}\n{block}\n{ManagedBlock.EndMarker}\n\n<repo outro>\n";
        await File.WriteAllTextAsync(Path.Combine(root, "BACKLOG.md"), backlog);
        var rows = new[] { new MirrorFileRow("BACKLOG.md", ContentHash.Sha256(block)) };

        var result = await MirrorVerifier.VerifyAsync(root, rows, CancellationToken.None);

        result.Passed.ShouldBeTrue();

        await File.WriteAllTextAsync(Path.Combine(root, "BACKLOG.md"), "<no block>\n");
        var broken = await MirrorVerifier.VerifyAsync(root, rows, CancellationToken.None);
        broken.Passed.ShouldBeFalse();
        broken.Drifted.ShouldHaveSingleItem().ShouldBe("BACKLOG.md");
    }

    [Fact]
    public async Task Verify_RowHashIsCaseInsensitiveHex_MatchesLowercaseStoredHash()
    {
        var root = CreateDirectory("candidate");
        await File.WriteAllTextAsync(Path.Combine(root, "a.md"), "alpha\n");
        var rows = new[] { new MirrorFileRow("a.md", Sha256Of("alpha\n").ToLowerInvariant()) };

        var result = await MirrorVerifier.VerifyAsync(root, rows, CancellationToken.None);

        result.Passed.ShouldBeTrue();
    }

    [Fact]
    public void Describe_UnderLimitPaths_ListsEveryFailure()
    {
        var result = new MirrorVerificationResult(3, ["edited.md"], ["gone.md"]);

        var detail = result.Describe();

        detail.ShouldBe(
            "Mirror verification failed: 2 of 3 files drifted or missing at the candidate path. " +
            "Files: edited.md, gone.md.");
    }

    [Fact]
    public void Describe_OverLimitPaths_ListsFirstTenAndCountsTheRest()
    {
        var drifted = Enumerable.Range(1, 13).Select(i => $"BACKLOG/f{i:00}/spec.md").ToList();
        var result = new MirrorVerificationResult(13, drifted, []);

        var detail = result.Describe();

        detail.ShouldStartWith("Mirror verification failed: 13 of 13 files drifted or missing at the candidate path.");
        detail.ShouldContain("BACKLOG/f01/spec.md");
        detail.ShouldContain("BACKLOG/f10/spec.md");
        detail.ShouldNotContain("BACKLOG/f11/spec.md");
        detail.ShouldEndWith("and 3 more.");
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_sandboxDir, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Sha256Of(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
