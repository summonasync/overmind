using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Xunit;

namespace Overmind.Host.Kestrel.Tests;

public sealed class BrowserHostFixture : IAsyncLifetime
{
    private ChromeInstance? _chrome;
    private CdpConnection? _connection;

    public string? SkipReason { get; private set; }

    public string? FailureReason { get; private set; }

    public KestrelApiFactory? Factory { get; private set; }

    public CdpPage? Page { get; private set; }

    public CdpChatHarness? Harness { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid ConversationId { get; private set; }

    public string BaseUrl => Factory!.BaseUrl;

    public async Task InitializeAsync()
    {
        if (ChromeLocator.Find() is null)
        {
            SkipReason = "chrome binary not found";
            return;
        }

        if (NodeLocator.Find() is not NodeToolchain toolchain)
        {
            SkipReason = "node/npm toolchain not found";
            return;
        }

        var webDirectory = FindWebDirectory();
        if (webDirectory is null)
        {
            FailureReason = "no web/ directory with package.json found above the test assembly";
            return;
        }

        var build = await WebBuildRunner.RunAsync(toolchain, webDirectory, CancellationToken.None);
        if (!build.Succeeded)
        {
            FailureReason = build.Error;
            return;
        }

        try
        {
            await BootAsync(Path.Combine(webDirectory, "out"));
        }
        catch (Exception ex)
        {
            FailureReason = $"browser host bootstrap failed: {ex.Message}";
            await TearDownAsync();
        }
    }

    public async Task DisposeAsync() => await TearDownAsync();

    public void EnsureUsable()
    {
        if (SkipReason is not null)
        {
            throw new InvalidOperationException($"browser leg skipped: {SkipReason}");
        }

        if (FailureReason is not null)
        {
            throw new InvalidOperationException($"browser leg failed to boot: {FailureReason}");
        }

        if (Page is null || Harness is null)
        {
            throw new InvalidOperationException("browser fixture did not boot");
        }
    }

    private async Task BootAsync(string webRoot)
    {
        Factory = new KestrelApiFactory(webRoot);
        (ProjectId, ConversationId) = await SeedChatStateAsync();
        _chrome = await ChromeInstance.LaunchAsync(CancellationToken.None);
        _connection = await CdpConnection.ConnectAsync(new Uri(_chrome.PageWebSocketUrl), CancellationToken.None);
        var page = new CdpPage(_connection);
        await page.EnableAsync(CancellationToken.None);
        Page = page;
        Harness = new CdpChatHarness(page);
    }

    private async Task<(Guid ProjectId, Guid ConversationId)> SeedChatStateAsync()
    {
        using var scope = Factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var project = Project.Create(
            $"kestrel-browser-{Guid.NewGuid():N}",
            Path.Combine(Path.GetTempPath(), "overmind-kestrel-browser", Guid.NewGuid().ToString("N")));
        var conversation = Conversation.Create(project.Id, "harness smoke conversation");
        db.Projects.Add(project);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return (project.Id, conversation.Id);
    }

    private async Task TearDownAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        if (_chrome is not null)
        {
            await _chrome.DisposeAsync();
            _chrome = null;
        }

        if (Factory is not null)
        {
            await Task.Run(Factory.Dispose);
            Factory = null;
        }

        Page = null;
        Harness = null;
    }

    private static string? FindWebDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "web");
            if (File.Exists(Path.Combine(candidate, "package.json"))
                && File.Exists(Path.Combine(current.FullName, "Overmind.sln")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
