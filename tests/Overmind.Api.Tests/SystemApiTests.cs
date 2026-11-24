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
    public async Task TreeRoots_EmptyByDefault_ReturnsNoRoots()
    {
        var roots = await GetRootsAsync(_client);

        roots.Roots.ShouldBeEmpty();
    }

    [Fact]
    public async Task TreeRoots_Add_ReturnsRootWithDirectoryName()
    {
        var response = await _client.PostAsJsonAsync("/api/system/tree-roots", new { Path = _sandboxDir });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roots = await response.Content.ReadFromJsonAsync<TreeRootsJson>(JsonOptions);
        roots.ShouldNotBeNull();
        roots!.Roots.Length.ShouldBe(1);
        roots.Roots[0].Path.ShouldBe(Normalize(_sandboxDir));
        roots.Roots[0].Name.ShouldBe(new DirectoryInfo(_sandboxDir).Name);
        roots.Roots[0].ContainsProjects.ShouldBeFalse();
    }

    [Fact]
    public async Task TreeRoots_AddDuplicate_Returns409()
    {
        await AddRootAsync(_sandboxDir);

        var response = await _client.PostAsJsonAsync("/api/system/tree-roots", new { Path = _sandboxDir });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TreeRoots_AddNestedInsideExistingRoot_Returns409()
    {
        var nested = Path.Combine(_sandboxDir, "nested");
        Directory.CreateDirectory(nested);
        await AddRootAsync(_sandboxDir);

        var response = await _client.PostAsJsonAsync("/api/system/tree-roots", new { Path = nested });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TreeRoots_AddParentOfExistingRoot_AbsorbsNestedRoots()
    {
        var nested = Path.Combine(_sandboxDir, "nested");
        Directory.CreateDirectory(nested);
        await AddRootAsync(nested);

        var response = await _client.PostAsJsonAsync("/api/system/tree-roots", new { Path = _sandboxDir });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roots = await response.Content.ReadFromJsonAsync<TreeRootsJson>(JsonOptions);
        roots!.Roots.Select(r => r.Path).ShouldBe([Normalize(_sandboxDir)]);
    }

    [Fact]
    public async Task TreeRoots_AddRelativePath_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/system/tree-roots", new { Path = "some/relative/path" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TreeRoots_AddMissingPath_Returns404()
    {
        var missing = Path.Combine(_sandboxDir, "missing");

        var response = await _client.PostAsJsonAsync("/api/system/tree-roots", new { Path = missing });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TreeRoots_AddFilePath_Returns400()
    {
        var file = Path.Combine(_sandboxDir, "notes.txt");
        await File.WriteAllTextAsync(file, "not a directory");

        var response = await _client.PostAsJsonAsync("/api/system/tree-roots", new { Path = file });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TreeRoots_Delete_RemovesRootAndReturnsRemainingRoots()
    {
        var otherDir = Path.Combine(
            Path.GetTempPath(), $"overmind-system-api-other-{Guid.NewGuid():N}");
        Directory.CreateDirectory(otherDir);
        try
        {
            await AddRootAsync(_sandboxDir);
            await AddRootAsync(otherDir);

            var response = await _client.DeleteAsync(
                $"/api/system/tree-roots?path={Uri.EscapeDataString(_sandboxDir)}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var roots = await response.Content.ReadFromJsonAsync<TreeRootsJson>(JsonOptions);
            roots.ShouldNotBeNull();
            roots!.Roots.Select(r => r.Path).ShouldBe([Normalize(otherDir)]);
        }
        finally
        {
            Directory.Delete(otherDir, recursive: true);
        }
    }

    [Fact]
    public async Task TreeRoots_DeleteUnknownPath_Returns404()
    {
        var response = await _client.DeleteAsync(
            $"/api/system/tree-roots?path={Uri.EscapeDataString(_sandboxDir)}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TreeRoots_DeleteBlockedByNestedProject_Returns409()
    {
        var projectDir = Path.Combine(_sandboxDir, "registered");
        Directory.CreateDirectory(projectDir);
        await CreateProjectAsync("root-guard", projectDir);
        await AddRootAsync(_sandboxDir);

        var response = await _client.DeleteAsync(
            $"/api/system/tree-roots?path={Uri.EscapeDataString(_sandboxDir)}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("RootContainsProjects");

        var roots = await GetRootsAsync(_client);
        roots.Roots.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task TreeRoots_PersistAcrossHostRestarts()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-api-restart-{Guid.NewGuid():N}.db");
        var rootDir = Path.Combine(_sandboxDir, "persisted");
        Directory.CreateDirectory(rootDir);

        try
        {
            using (var factory = new ApiFactory(databasePath, preserveDatabase: true))
            using (var client = factory.CreateClient())
            {
                var response = await client.PostAsJsonAsync("/api/system/tree-roots", new { Path = rootDir });
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            using (var factory = new ApiFactory(databasePath, preserveDatabase: true))
            using (var client = factory.CreateClient())
            {
                var roots = await GetRootsAsync(client);
                roots.Roots.Select(r => r.Path).ShouldBe([Normalize(rootDir)]);
            }
        }
        finally
        {
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var file = databasePath + suffix;
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }

    [Fact]
    public async Task BrowseRoots_IncludesHomeDirectory()
    {
        var roots = await GetBrowseRootsAsync(_client);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        roots.Roots.ShouldContain(r => r.Kind == "home" && r.Path == Normalize(home));
    }

    [Fact]
    public async Task BrowseRoots_OnLinux_IncludesMountedRealFilesystems()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var roots = await GetBrowseRootsAsync(_client);

        roots.Roots.ShouldContain(r => r.Kind == "volume" && r.Path.StartsWith('/'));
    }

    [Fact]
    public async Task BrowseRoots_ReflectsConfiguredTreeRoots()
    {
        await AddRootAsync(_sandboxDir);

        var roots = await GetBrowseRootsAsync(_client);

        var root = roots.Roots.Single(r => r.Path == Normalize(_sandboxDir));
        root.Name.ShouldBe(new DirectoryInfo(_sandboxDir).Name);
        root.Kind.ShouldBe("treeRoot");
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
        // FileAttributes.Hidden is an NTFS concept; setting it is a no-op on Linux.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(_sandboxDir, "visible"));
        var hidden = Path.Combine(_sandboxDir, "hidden-dir");
        Directory.CreateDirectory(hidden);
        File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);

        var listing = await GetListingAsync(_sandboxDir);

        listing.Entries.Select(e => e.Name).ShouldBe(["visible"]);
    }

    [Fact]
    public async Task ListDirectory_HidesDotDirectoriesOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(_sandboxDir, ".stash"));
        Directory.CreateDirectory(Path.Combine(_sandboxDir, "visible"));

        var listing = await GetListingAsync(_sandboxDir);

        listing.Entries.Select(e => e.Name).ShouldBe(["visible"]);
    }

    [Fact]
    public async Task ListDirectory_UnreadableRoot_ReturnsEmptyInaccessibleListing()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var locked = Path.Combine(_sandboxDir, "locked");
        Directory.CreateDirectory(locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            if (CanEnumerate(locked))
            {
                return;
            }

            var response = await _client.GetAsync(
                $"/api/system/list-directory?path={Uri.EscapeDataString(locked)}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var listing = (await response.Content.ReadFromJsonAsync<ListingJson>(JsonOptions))!;
            listing.Accessible.ShouldBeFalse();
            listing.Entries.ShouldBeEmpty();
        }
        finally
        {
            File.SetUnixFileMode(
                locked,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
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

    private async Task<TreeRootsJson> GetRootsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/system/tree-roots");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roots = await response.Content.ReadFromJsonAsync<TreeRootsJson>(JsonOptions);
        roots.ShouldNotBeNull();
        return roots!;
    }

    private async Task<TreeRootsJson> AddRootAsync(string path)
    {
        var response = await _client.PostAsJsonAsync("/api/system/tree-roots", new { Path = path });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roots = await response.Content.ReadFromJsonAsync<TreeRootsJson>(JsonOptions);
        roots.ShouldNotBeNull();
        return roots!;
    }

    private async Task<BrowseRootsJson> GetBrowseRootsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/system/browse-roots");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roots = await response.Content.ReadFromJsonAsync<BrowseRootsJson>(JsonOptions);
        roots.ShouldNotBeNull();
        return roots!;
    }

    private static bool CanEnumerate(string path)
    {
        try
        {
            foreach (var _ in Directory.EnumerateDirectories(path))
            {
                break;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
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

    public sealed record TreeRootJson(string Name, string Path, bool ContainsProjects);

    public sealed record ProblemJson(string? Title, string? Detail);

    public sealed record ListingJson(string Path, bool Truncated, EntryJson[] Entries, bool Accessible);

    public sealed record BrowseRootsJson(BrowseRootJson[] Roots);

    public sealed record BrowseRootJson(string Name, string Path, string Kind);

    public sealed record EntryJson(string Name, string Path, bool Accessible, Guid? ProjectId, string? ProjectName);

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);
}
