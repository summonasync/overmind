using Overmind.Core;
using Overmind.Infrastructure.Manifest;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ManifestImporterTests : IDisposable
{
    private readonly ManifestImporter _importer = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"overmind-manifest-importer-{Guid.NewGuid():N}");

    public ManifestImporterTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task Import_AllCandidates_PrefersAgents()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "AGENTS.md"), "# agents");
        await File.WriteAllTextAsync(Path.Combine(_root, "CLAUDE.md"), "# claude");
        await File.WriteAllTextAsync(Path.Combine(_root, "QWEN.md"), "# qwen");

        var import = await _importer.ImportAsync(_root, CancellationToken.None);

        import!.SourceFile.ShouldBe("AGENTS.md");
    }

    [Fact]
    public async Task Import_ClaudeAndQwen_PrefersClaude()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "CLAUDE.md"), "# claude");
        await File.WriteAllTextAsync(Path.Combine(_root, "QWEN.md"), "# qwen");

        var import = await _importer.ImportAsync(_root, CancellationToken.None);

        import!.SourceFile.ShouldBe("CLAUDE.md");
    }

    [Fact]
    public async Task Import_QwenOnly_UsesQwen()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "QWEN.md"), "# qwen");

        var import = await _importer.ImportAsync(_root, CancellationToken.None);

        import!.SourceFile.ShouldBe("QWEN.md");
    }

    [Fact]
    public async Task Import_NoCandidates_ReturnsNull()
    {
        var import = await _importer.ImportAsync(_root, CancellationToken.None);

        import.ShouldBeNull();
    }

    [Fact]
    public async Task Import_EmptyAgentsFile_FallsThroughToClaude()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "AGENTS.md"), "   \n");
        await File.WriteAllTextAsync(Path.Combine(_root, "CLAUDE.md"), "# claude");

        var import = await _importer.ImportAsync(_root, CancellationToken.None);

        import!.SourceFile.ShouldBe("CLAUDE.md");
    }

    [Fact]
    public async Task Import_TrimsBodyAndPrependsHeaderLine()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "AGENTS.md"), "\n# Alpha\n\nBuild: dotnet build.\n\n");

        var import = await _importer.ImportAsync(_root, CancellationToken.None);

        import!.Body.ShouldBe("<!-- imported from AGENTS.md -->\n\n# Alpha\n\nBuild: dotnet build.");
    }

    [Fact]
    public async Task Import_RelativeRoot_ThrowsTraversal()
    {
        var import = async () => await _importer.ImportAsync("relative/path", CancellationToken.None);

        var exception = await import.ShouldThrowAsync<MaterializationException>();
        exception.Kind.ShouldBe(MaterializationFailureKind.PathTraversal);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
