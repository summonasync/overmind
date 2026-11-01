using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wake;
using Overmind.Infrastructure.Wave;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class WakeDropFanoutTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"wake-{Guid.NewGuid():N}");
    private readonly WakeDropFanout _fanout;
    private readonly ServiceProvider _services;
    private readonly WaveChatService _chat;
    private readonly Guid _projectId;

    public WakeDropFanoutTests()
    {
        _context = _db.CreateContext();
        var project = Project.Create($"wakefanout-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;

        var (fanout, services) = TestWakeFanout.Create(_db, _root);
        _fanout = fanout;
        _services = services;
        _chat = new WaveChatService(_context, _fanout);
    }

    public void Dispose()
    {
        _services.Dispose();
        _context.Dispose();
        _db.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public async Task Post_MentionKnownAlias_RewritesTheAliasDropFile()
    {
        var alias = "billy";
        _context.AgentSessions.Add(AgentSession.Create(alias, "claude"));
        await _context.SaveChangesAsync();

        await _chat.PostAsync(_projectId, "all", "dan", "worker", $"ping @{alias}", CancellationToken.None);

        var file = Path.Combine(_root, $"{alias}.json");
        await WaitUntilAsync(() => File.Exists(file));
        var drop = JsonSerializer.Deserialize<WakeDrop>(await File.ReadAllTextAsync(file), JsonOptions);
        drop.ShouldNotBeNull();
        drop.Alias.ShouldBe(alias);
        var signal = drop.Signals.Single();
        signal.Kind.ShouldBe(AgentWakeService.MentionKind);
        signal.From.ShouldBe("dan");
        signal.RoomId.ShouldBe("all");
        drop.Cursor.ShouldBe($"{signal.CreatedAt:O}|{signal.RefId}");
    }

    [Fact]
    public async Task Post_MentionUnknownAlias_WritesNothing()
    {
        await _chat.PostAsync(_projectId, "all", "dan", "worker", "ping @ghost", CancellationToken.None);

        await _fanout.WakeAsync("ghost");

        Directory.Exists(_root).ShouldBeFalse();
    }

    [Fact]
    public async Task Reply_RewritesTheNotificationAgentDropFile()
    {
        var alias = "billy";
        _context.AgentSessions.Add(AgentSession.Create(alias, "claude"));
        var notification = AgentNotification.Restore(
            Guid.NewGuid(), _projectId, NotificationKind.Question, null, null,
            "gate needs you", alias, DateTime.UtcNow.AddMinutes(-1));
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();
        var notifications = new NotificationsService(
            _context,
            new ReplyAutoActFanout(
                _context, new ReplyAutoActQueue(), NullLogger<ReplyAutoActFanout>.Instance),
            _fanout);

        await notifications.ReplyAsync(notification.Id, "approved", CancellationToken.None);

        var file = Path.Combine(_root, $"{alias}.json");
        await WaitUntilAsync(() => File.Exists(file));
        var drop = JsonSerializer.Deserialize<WakeDrop>(await File.ReadAllTextAsync(file), JsonOptions);
        drop.ShouldNotBeNull();
        drop.Signals.Single().Kind.ShouldBe(AgentWakeService.ReplyKind);
        drop.Signals.Single().From.ShouldBe("human");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }
}
