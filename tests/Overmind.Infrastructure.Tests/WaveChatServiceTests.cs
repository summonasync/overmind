using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wave;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class WaveChatServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly Guid _projectId;

    public WaveChatServiceTests()
    {
        _context = _db.CreateContext();
        var project = Project.Create($"wave-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.PlanningLanes.Add(PlanningLane.Create(project.Id, "Backend", "dotnet", 1));
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Post_ToLaneRoom_KeepsRosterSpelling()
    {
        var message = await Service().PostAsync(_projectId, "backend", "Dan", AgentSession.DefaultRole, "hello lane");

        message.Room.ShouldBe("Backend");
        message.AuthorAlias.ShouldBe("Dan");
        (await CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Post_ToAll_AnyCasing_StoresCanonicalAll()
    {
        var message = await Service().PostAsync(_projectId, "ALL", "Dan", AgentSession.DefaultRole, "hello everyone");

        message.Room.ShouldBe("all");
    }

    [Fact]
    public async Task Post_UnknownLane_ThrowsMissingWaveRoom_WithRosterInDetail()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            Service().PostAsync(_projectId, "frontend", "Dan", AgentSession.DefaultRole, "hello"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingWaveRoom);
        ex.Message.ShouldContain("Backend");
    }

    [Fact]
    public async Task Post_TextOverCap_ThrowsNamedWaveTextTooLong()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            Service().PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, new string('x', WaveMessage.MaxTextLength + 1)));

        ex.Code.ShouldBe(PlanningFailureCode.WaveTextTooLong);
        (await CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Post_AtTheCap_IsAccepted()
    {
        var message = await Service().PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, new string('x', WaveMessage.MaxTextLength));

        message.Text.Length.ShouldBe(WaveMessage.MaxTextLength);
    }

    [Fact]
    public async Task Post_UnknownProject_ThrowsMissingProject()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            Service().PostAsync(Guid.NewGuid(), "all", "Dan", AgentSession.DefaultRole, "hello"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }

    [Fact]
    public async Task Post_StoresTheRoleTheCallerStamped()
    {
        var marshal = await Service().PostAsync(_projectId, "all", "Marshal", AgentSession.MarshalRole, "marshal talk");
        var worker = await Service().PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "worker talk");

        marshal.AuthorRole.ShouldBe(AgentSession.MarshalRole);
        worker.AuthorRole.ShouldBe(AgentSession.DefaultRole);
    }

    [Fact]
    public async Task Unread_AndHistory_SurfaceTheStampedRole()
    {
        var service = Service();
        await service.PostAsync(_projectId, "all", "Marshal", AgentSession.MarshalRole, "marshal talk");
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "worker talk");

        var unread = await service.ListUnreadAsync(_projectId, "guldan", "all");
        unread.Messages.Select(m => (m.AuthorAlias, m.AuthorRole))
            .ShouldBe([("Marshal", AgentSession.MarshalRole), ("Dan", AgentSession.DefaultRole)]);

        var history = await service.ListHistoryAsync(_projectId, "all");
        history.Select(m => (m.AuthorAlias, m.AuthorRole))
            .ShouldBe([("Marshal", AgentSession.MarshalRole), ("Dan", AgentSession.DefaultRole)]);
    }

    [Fact]
    public async Task Unread_LaneFeed_IsIsolatedFromOtherLanes()
    {
        var service = Service();
        await service.PostAsync(_projectId, "backend", "Dan", AgentSession.DefaultRole, "lane talk");
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "hall talk");

        var backend = await service.ListUnreadAsync(_projectId, "marshal", "backend");
        var hall = await service.ListUnreadAsync(_projectId, "marshal", "all");

        backend.Messages.Select(m => m.Text).ShouldBe(["lane talk"]);
        hall.Messages.Select(m => m.Text).ShouldBe(["hall talk"]);
    }

    [Fact]
    public async Task Unread_MentionCrossesRooms_ThenIsAckedForTheLaneRead()
    {
        var service = Service();
        await service.PostAsync(_projectId, "backend", "Dan", AgentSession.DefaultRole, "need @marshal here");

        var hall = await service.ListUnreadAsync(_projectId, "marshal", "all");
        var backend = await service.ListUnreadAsync(_projectId, "marshal", "backend");

        hall.Messages.Select(m => m.Text).ShouldBe(["need @marshal here"]);
        hall.Messages.Single().Room.ShouldBe("Backend");
        backend.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unread_MentionMatching_IsCaseInsensitive()
    {
        var service = Service();
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "ping @MARSHAL");

        var feed = await service.ListUnreadAsync(_projectId, "Marshal", "backend");

        feed.Messages.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Unread_WatermarkAdvances_OnRead()
    {
        var service = Service();
        var posted = await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "first");

        var feed = await service.ListUnreadAsync(_projectId, "marshal", "all");
        feed.Messages.Count.ShouldBe(1);
        feed.WatermarkAt.ShouldBe(posted.CreatedAt);

        _context.ChangeTracker.Clear();
        var stored = await _context.WaveWatermarks.SingleAsync(w => w.Alias == "marshal");
        stored.LastReadAt.ShouldBe(posted.CreatedAt);
    }

    [Fact]
    public async Task Unread_MentionIsDeliveredOnce_SecondReadIsEmpty()
    {
        var service = Service();
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "plain note");
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "ping @marshal");

        var first = await service.ListUnreadAsync(_projectId, "marshal", "all");
        first.Messages.Select(m => m.Text).ShouldBe(["plain note", "ping @marshal"]);

        var second = await service.ListUnreadAsync(_projectId, "marshal", "all");

        second.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unread_ReplayMentions_RestoresTheUnscopedDelivery()
    {
        var service = Service();
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "ping @marshal");
        var watermarkAt = (await service.ListUnreadAsync(_projectId, "marshal", "all")).WatermarkAt;

        var stale = await service.PostAsync(_projectId, "backend", "Dan", AgentSession.DefaultRole, "stale ping @marshal");
        await BackdateAsync(stale.Id, watermarkAt.AddMinutes(-30));

        var scoped = await service.ListUnreadAsync(_projectId, "marshal", "backend");
        var replayed = await service.ListUnreadAsync(_projectId, "marshal", "backend", replayMentions: true);

        scoped.Messages.ShouldBeEmpty();
        replayed.Messages.Select(m => m.Text).ShouldBe(["stale ping @marshal", "ping @marshal"]);
    }

    [Fact]
    public async Task Unread_MentionOlderThanWatermark_IsSuppressed()
    {
        var service = Service();
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "read me");
        var watermarkAt = (await service.ListUnreadAsync(_projectId, "marshal", "all")).WatermarkAt;

        var mentioned = await service.PostAsync(_projectId, "backend", "Dan", AgentSession.DefaultRole, "late ping @marshal");
        await BackdateAsync(mentioned.Id, watermarkAt.AddMinutes(-10));

        var feed = await service.ListUnreadAsync(_projectId, "marshal", "backend");

        feed.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unread_Since_NarrowsTheWindowAndNeverWidens()
    {
        var service = Service();
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "read me");
        var watermarkAt = (await service.ListUnreadAsync(_projectId, "marshal", "all")).WatermarkAt;

        var hidden = await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "hidden");
        await BackdateAsync(hidden.Id, watermarkAt.AddMinutes(5));
        var shown = await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "shown");
        await BackdateAsync(shown.Id, watermarkAt.AddMinutes(10));

        var narrowed = await service.ListUnreadAsync(_projectId, "marshal", "all", since: watermarkAt.AddMinutes(7));
        narrowed.Messages.Select(m => m.Text).ShouldBe(["shown"]);

        var widened = await service.ListUnreadAsync(_projectId, "marshal", "all", since: watermarkAt.AddMinutes(-100));
        widened.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unread_Limit_KeepsTheNewestWindowInChronologicalOrder()
    {
        var service = Service();
        var seeded = await SeedHistoryAsync(service, 5);

        var page = await service.ListUnreadAsync(_projectId, "marshal", "all", limit: 2);

        page.Messages.Select(m => m.Id).ShouldBe(seeded.Skip(3).Select(m => m.Id));

        _context.ChangeTracker.Clear();
        var stored = await _context.WaveWatermarks.SingleAsync(w => w.Alias == "marshal");
        stored.LastReadAt.ShouldBe(page.Messages[^1].CreatedAt);
    }

    [Fact]
    public async Task Unread_LimitBelowOne_ClampsToOne()
    {
        await SeedHistoryAsync(Service(), 3);

        var page = await Service().ListUnreadAsync(_projectId, "marshal", "all", limit: 0);

        page.Messages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Unread_LimitAboveCap_ClampsToMax()
    {
        _context.WaveMessages.AddRange(Enumerable.Range(0, WaveChatService.MaxHistoryLimit + 5)
            .Select(_ => WaveMessage.Create(_projectId, "all", "Dan", AgentSession.DefaultRole, "bulk")));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var page = await Service().ListUnreadAsync(
            _projectId, "marshal", "all", limit: WaveChatService.MaxHistoryLimit + 100);

        page.Messages.Count.ShouldBe(WaveChatService.MaxHistoryLimit);
    }

    [Fact]
    public async Task Unread_WithoutLimit_ThePageStaysUnbounded()
    {
        var seeded = await SeedHistoryAsync(Service(), WaveChatService.MaxHistoryLimit + 5);

        var page = await Service().ListUnreadAsync(_projectId, "marshal", "all");

        page.Messages.Count.ShouldBe(seeded.Count);
    }

    [Fact]
    public async Task Unread_WatermarkIsPerAlias()
    {
        var service = Service();
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "for everyone");

        await service.ListUnreadAsync(_projectId, "marshal", "all");

        var fresh = await service.ListUnreadAsync(_projectId, "guldan", "all");
        fresh.Messages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Unread_UnknownLane_ThrowsMissingWaveRoom()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            Service().ListUnreadAsync(_projectId, "marshal", "frontend"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingWaveRoom);
    }

    [Fact]
    public async Task Unread_UnknownProject_ThrowsMissingProject()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            Service().ListUnreadAsync(Guid.NewGuid(), "marshal", "all"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }

    [Fact]
    public async Task Peek_MirrorsTheUnreadSelection_WithoutAdvancingTheWatermark()
    {
        var service = Service();
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "read me");
        var watermarkAt = (await service.ListUnreadAsync(_projectId, "marshal", "all")).WatermarkAt;

        var mentioned = await service.PostAsync(_projectId, "backend", "Dan", AgentSession.DefaultRole, "need @marshal here");

        var first = await service.PeekAsync(_projectId, "marshal", "all");
        var second = await service.PeekAsync(_projectId, "marshal", "all");

        first.Room.ShouldBe("all");
        first.Count.ShouldBe(1);
        first.Ids.ShouldBe([mentioned.Id]);
        second.Ids.ShouldBe(first.Ids);

        _context.ChangeTracker.Clear();
        var stored = await _context.WaveWatermarks.SingleAsync(w => w.Alias == "marshal");
        stored.LastReadAt.ShouldBe(watermarkAt);

        var unread = await service.ListUnreadAsync(_projectId, "marshal", "all");
        unread.Messages.Select(m => m.Id).ShouldBe(first.Ids);
    }

    [Fact]
    public async Task Peek_AfterRead_ReportsOnlyStillUnreadTraffic()
    {
        var service = Service();
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "read me");
        await service.ListUnreadAsync(_projectId, "marshal", "all");

        var peek = await service.PeekAsync(_projectId, "marshal", "all");

        peek.Count.ShouldBe(0);
        peek.Ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task Peek_UnknownLane_ThrowsMissingWaveRoom()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            Service().PeekAsync(_projectId, "marshal", "frontend"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingWaveRoom);
    }

    [Fact]
    public async Task Peek_UnknownProject_ThrowsMissingProject()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            Service().PeekAsync(Guid.NewGuid(), "marshal", "all"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }

    [Fact]
    public async Task Rooms_ListAllPlusRoster_WithLastActivity()
    {
        var service = Service();
        var first = await service.PostAsync(_projectId, "backend", "Dan", AgentSession.DefaultRole, "lane talk");
        var last = await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "hall talk");

        var rooms = await service.ListRoomsAsync(_projectId);

        rooms.Select(r => r.Room).ShouldBe(["all", "Backend"]);
        rooms.Single(r => r.Room == "all").LastActivityAt.ShouldBe(last.CreatedAt);
        rooms.Single(r => r.Room == "Backend").LastActivityAt.ShouldBe(first.CreatedAt);
    }

    [Fact]
    public async Task Rooms_DoesNotListDeletedLanes()
    {
        var service = Service();
        await service.PostAsync(_projectId, "backend", "Dan", AgentSession.DefaultRole, "lane talk");
        var lane = await _context.PlanningLanes.SingleAsync(l => l.Name == "Backend");
        _context.PlanningLanes.Remove(lane);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var rooms = await service.ListRoomsAsync(_projectId);

        rooms.Select(r => r.Room).ShouldBe(["all"]);
    }

    [Fact]
    public async Task Rooms_WithLaneWithoutTraffic_ReportsNullActivity()
    {
        var rooms = await Service().ListRoomsAsync(_projectId);

        rooms.Select(r => r.Room).ShouldBe(["all", "Backend"]);
        rooms.Count(r => r.LastActivityAt == null).ShouldBe(2);
    }

    [Fact]
    public async Task Rooms_UnknownProject_ThrowsMissingProject()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            Service().ListRoomsAsync(Guid.NewGuid()));

        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }

    [Fact]
    public async Task History_ReturnsChronologicalOldestFirst()
    {
        var posted = await SeedHistoryAsync(Service(), 4);

        var page = await Service().ListHistoryAsync(_projectId, "all");

        page.Select(m => m.Id).ShouldBe(posted.Select(m => m.Id));
    }

    [Fact]
    public async Task History_DefaultLimit_ReturnsTheLatestWindow()
    {
        var posted = await SeedHistoryAsync(Service(), WaveChatService.DefaultHistoryLimit + 5);

        var page = await Service().ListHistoryAsync(_projectId, "all");

        page.Count.ShouldBe(WaveChatService.DefaultHistoryLimit);
        page.Select(m => m.Id).ShouldBe(posted.Skip(5).Select(m => m.Id));
    }

    [Fact]
    public async Task History_LimitAboveCap_ClampsToMax()
    {
        _context.WaveMessages.AddRange(Enumerable.Range(0, WaveChatService.MaxHistoryLimit + 5)
            .Select(_ => WaveMessage.Create(_projectId, "all", "Dan", AgentSession.DefaultRole, "bulk")));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var page = await Service().ListHistoryAsync(
            _projectId, "all", WaveChatService.MaxHistoryLimit + 100);

        page.Count.ShouldBe(WaveChatService.MaxHistoryLimit);
    }

    [Fact]
    public async Task History_LimitBelowOne_ClampsToOne()
    {
        await SeedHistoryAsync(Service(), 3);

        var page = await Service().ListHistoryAsync(_projectId, "all", 0);

        page.Count.ShouldBe(1);
    }

    [Fact]
    public async Task History_BeforeCursor_ReturnsStrictlyOlderMessages()
    {
        var posted = await SeedHistoryAsync(Service(), 5);

        var page = await Service().ListHistoryAsync(_projectId, "all", 3);
        page.Select(m => m.Id).ShouldBe(posted.Skip(2).Select(m => m.Id));

        var older = await Service().ListHistoryAsync(_projectId, "all", before: page[0].CreatedAt);
        older.Select(m => m.Id).ShouldBe(posted.Take(2).Select(m => m.Id));
    }

    [Fact]
    public async Task History_IsolatedFromOtherRooms()
    {
        var service = Service();
        await service.PostAsync(_projectId, "backend", "Dan", AgentSession.DefaultRole, "lane talk");
        var hall = await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "hall talk");

        var page = await service.ListHistoryAsync(_projectId, "all");

        page.Select(m => m.Id).ShouldBe([hall.Id]);
    }

    [Fact]
    public async Task History_LeavesWatermarksUntouched()
    {
        var service = Service();
        await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, "history one");
        var unread = await service.ListUnreadAsync(_projectId, "marshal", "all");

        await service.ListHistoryAsync(_projectId, "all");
        await service.ListHistoryAsync(_projectId, "all");

        _context.ChangeTracker.Clear();
        _context.WaveWatermarks.Count().ShouldBe(1);
        var watermark = await _context.WaveWatermarks.SingleAsync(w => w.Alias == "marshal");
        watermark.LastReadAt.ShouldBe(unread.WatermarkAt);
    }

    [Fact]
    public async Task History_UnknownLane_ThrowsMissingWaveRoom()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            Service().ListHistoryAsync(_projectId, "frontend"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingWaveRoom);
    }

    [Fact]
    public async Task History_UnknownProject_ThrowsMissingProject()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() =>
            Service().ListHistoryAsync(Guid.NewGuid(), "all"));

        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }

    private async Task<List<WaveMessage>> SeedHistoryAsync(WaveChatService service, int count)
    {
        var posted = new List<WaveMessage>();
        for (var i = 0; i < count; i++)
        {
            posted.Add(await service.PostAsync(_projectId, "all", "Dan", AgentSession.DefaultRole, $"msg-{i:000}"));
        }

        var baseTime = DateTime.UtcNow.AddMinutes(-count - 10);
        for (var i = 0; i < posted.Count; i++)
        {
            await BackdateAsync(posted[i].Id, baseTime.AddMinutes(i));
        }

        return posted;
    }

    private WaveChatService Service() => new(_context);

    private async Task<int> CountAsync() =>
        await _context.WaveMessages.CountAsync();

    private async Task BackdateAsync(Guid messageId, DateTime createdAt)
    {
        await _context.Database.ExecuteSqlAsync(
            $"UPDATE WaveMessages SET CreatedAt = {createdAt} WHERE Id = {messageId}");
        _context.ChangeTracker.Clear();
    }
}
