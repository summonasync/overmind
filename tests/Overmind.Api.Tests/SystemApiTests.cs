using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class SystemApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SystemApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-system-api-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sandboxDir);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task TreeRoots_ReturnsAtLeastOneRootedRoot()
    {
        var response = await _client.GetAsync("/api/system/tree-roots");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roots = await response.Content.ReadFromJsonAsync<TreeRootsJson>(JsonOptions);

        roots.ShouldNotBeNull();
        roots!.Roots.Length.ShouldBeGreaterThan(0);
        roots.Roots.ShouldAllBe(r => Path.IsPathRooted(r.Path));
    }

    [Fact]
    public async Task ListDirectory_ReturnsSubdirectoriesOnly_SortedAlphabetically()
    {
        Directory.CreateDirectory(Path.Combine(_sandboxDir, "zebra"));
        Directory.CreateDirectory(Path.Combine(_sandboxDir, "Alpha"));
        Directory.CreateDirectory(Path.Combine(_sandboxDir, "mike"));
        await File.WriteAllTextAsync(Path.Combine(_sandboxDir, "notes.txt"), "not a directory");

        var listing = await GetListingAsync(_sandboxDir);

        listing.Path.ShouldBe(Normalize(_sandboxDir));
        listing.Truncated.ShouldBeFalse();
        listing.Entries.Select(e => e.Name).ShouldBe(["Alpha", "mike", "zebra"]);
        listing.Entries.ShouldAllBe(e => e.Accessible);
    }

    [Fact]
    public async Task ListDirectory_SkipsHiddenAndSystemDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_sandboxDir, "visible"));
        var hidden = Path.Combine(_sandboxDir, "hidden-dir");
        Directory.CreateDirectory(hidden);
        File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);

        var listing = await GetListingAsync(_sandboxDir);

        listing.Entries.Select(e => e.Name).ShouldBe(["visible"]);
    }

    [Fact]
    public async Task ListDirectory_MoreThan500Directories_ReturnsFirst500WithTruncated()
    {
        for (var i = 0; i < 505; i++)
        {
            Directory.CreateDirectory(Path.Combine(_sandboxDir, $"dir-{i:D4}"));
        }

        var listing = await GetListingAsync(_sandboxDir);

        listing.Entries.Length.ShouldBe(500);
        listing.Truncated.ShouldBeTrue();
        listing.Entries.ShouldAllBe(e => e.Accessible);
    }

    [Fact]
    public async Task ListDirectory_ProjectDiskPathMatchesEntryByNormalizedPath()
    {
        var projectDir = Path.Combine(_sandboxDir, "registered");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(_sandboxDir, "stranger"));
        var project = await CreateProjectAsync("tree-match", projectDir + Path.DirectorySeparatorChar);

        var listing = await GetListingAsync(_sandboxDir);

        var registered = listing.Entries.Single(e => e.Name == "registered");
        registered.ProjectId.ShouldBe(project.Id);
        registered.ProjectName.ShouldBe(project.Name);
        registered.Accessible.ShouldBeTrue();

        var stranger = listing.Entries.Single(e => e.Name == "stranger");
        stranger.ProjectId.ShouldBeNull();
        stranger.ProjectName.ShouldBeNull();
    }

    [Fact]
    public async Task ListDirectory_ProjectMatchIgnoresCaseOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var projectDir = Path.Combine(_sandboxDir, "casefold");
        Directory.CreateDirectory(projectDir);
        var project = await CreateProjectAsync("tree-casefold", projectDir.ToUpperInvariant());

        var listing = await GetListingAsync(_sandboxDir);

        var entry = listing.Entries.Single(e => e.Name == "casefold");
        entry.ProjectId.ShouldBe(project.Id);
    }

    [Fact]
    public async Task ListDirectory_NonexistentPath_Returns404()
    {
        var response = await _client.GetAsync(
            $"/api/system/list-directory?path={Uri.EscapeDataString(Path.Combine(_sandboxDir, "missing"))}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListDirectory_RelativePath_Returns400()
    {
        var response = await _client.GetAsync(
            $"/api/system/list-directory?path={Uri.EscapeDataString("some/relative/path")}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListDirectory_MissingPath_Returns400()
    {
        var response = await _client.GetAsync("/api/system/list-directory");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<ListingJson> GetListingAsync(string path)
    {
        var response = await _client.GetAsync(
            $"/api/system/list-directory?path={Uri.EscapeDataString(path)}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var listing = await response.Content.ReadFromJsonAsync<ListingJson>(JsonOptions);
        listing.ShouldNotBeNull();
        return listing!;
    }

    private async Task<ProjectJson> CreateProjectAsync(string name, string diskPath)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = diskPath,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length <= root.Length ? root : trimmed;
    }

    public sealed record TreeRootsJson(TreeRootJson[] Roots);

    public sealed record TreeRootJson(string Name, string Path);

    public sealed record ListingJson(string Path, bool Truncated, EntryJson[] Entries);

    public sealed record EntryJson(string Name, string Path, bool Accessible, Guid? ProjectId, string? ProjectName);

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);
}
