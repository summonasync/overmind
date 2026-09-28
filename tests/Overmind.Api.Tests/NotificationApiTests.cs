using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class NotificationApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotificationApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-notifications-{Guid.NewGuid():N}");
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
    public async Task Lifecycle_BothDoors_CreateListReadReplyAndPollReplies()
    {
        var project = await CreateProjectAsync();

        var created = await NotifyAsync("Question", "Spec gate wants your call", project.Id, "agent-notifications", "spec", "dotnet-coder");
        created.Kind.ShouldBe("question");
        created.ProjectId.ShouldBe(project.Id);
        created.FeatureSlug.ShouldBe("agent-notifications");
        created.Tab.ShouldBe("spec");
        created.Agent.ShouldBe("dotnet-coder");
        created.Text.ShouldBe("Spec gate wants your call");
        created.ReadAt.ShouldBeNull();
        created.ReplyText.ShouldBeNull();

        var firstList = await ListAsync();
        firstList.UnreadCount.ShouldBe(1);
        firstList.Items.Single().Id.ShouldBe(created.Id);

        var read = await MarkReadAsync(created.Id);
        read.ReadAt.ShouldNotBeNull();

        var reread = await MarkReadAsync(created.Id);
        reread.ReadAt.ShouldBe(read.ReadAt);

        var second = await NotifyAsync("info", "Import finished");
        second.Agent.ShouldBe("agent");
        second.ProjectId.ShouldBeNull();

        var secondList = await ListAsync();
        secondList.UnreadCount.ShouldBe(1);
        secondList.Items.Length.ShouldBe(2);
        secondList.Items[0].Id.ShouldBe(second.Id);

        var replied = await ReplyAsync(created.Id, "Ship it");
        replied.ReplyText.ShouldBe("Ship it");
        replied.RepliedAt.ShouldNotBeNull();

        var replies = await RepliesAsync();
        var answer = replies.Items.Single();
        answer.Id.ShouldBe(created.Id);
        answer.ReplyText.ShouldBe("Ship it");
        answer.RepliedAt.ShouldNotBeNull();
        answer.Text.ShouldBe("Spec gate wants your call");
    }

    [Fact]
    public async Task Reply_OnUnread_MarksItReadInTheSameOperation()
    {
        var notification = await NotifyAsync("approval", "Approve the plan?");

        var replied = await ReplyAsync(notification.Id, "approved");

        replied.ReplyText.ShouldBe("approved");
        replied.ReadAt.ShouldNotBeNull();
        replied.RepliedAt.ShouldNotBeNull();

        var list = await ListAsync();
        list.UnreadCount.ShouldBe(0);
        list.Items.Single(i => i.Id == notification.Id).ReadAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Reply_OnAlreadyRead_KeepsTheOriginalReadAt()
    {
        var notification = await NotifyAsync("question", "Which database?");
        var read = await MarkReadAsync(notification.Id);

        var replied = await ReplyAsync(notification.Id, "SQLite");

        replied.ReadAt.ShouldBe(read.ReadAt);
    }

    [Fact]
    public async Task Reply_SecondReply_OverwritesTheFirst()
    {
        var notification = await NotifyAsync("approval", "Activate the kit?");

        await ReplyAsync(notification.Id, "not yet");
        var second = await ReplyAsync(notification.Id, "go ahead");

        second.ReplyText.ShouldBe("go ahead");
        second.ReadAt.ShouldNotBeNull();

        var replies = await RepliesAsync();
        replies.Items.Single().ReplyText.ShouldBe("go ahead");
    }

    [Fact]
    public async Task Replies_IncludeReadFalse_DropsConversationsTheHumanClosed()
    {
        var notification = await NotifyAsync("question", "Which database?");
        await MarkReadAsync(notification.Id);
        await ReplyAsync(notification.Id, "SQLite");

        var all = await RepliesAsync();
        all.Items.Length.ShouldBe(1);

        var unreadOnly = await RepliesAsync(includeRead: false);
        unreadOnly.Items.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Replies_WithProjectId_ReturnsOnlyThatProject()
    {
        var project = await CreateProjectAsync();
        var scoped = await NotifyAsync("info", "scoped", project.Id);
        var loose = await NotifyAsync("info", "loose");
        await ReplyAsync(scoped.Id, "ok");
        await ReplyAsync(loose.Id, "fine");

        var filtered = await RepliesAsync(projectId: project.Id);
        filtered.Items.Single().Id.ShouldBe(scoped.Id);

        var everything = await RepliesAsync();
        everything.Items.Length.ShouldBe(2);
        everything.Items[0].Id.ShouldBe(loose.Id);
    }

    [Fact]
    public async Task List_MoreThanFifty_CapsAtNewestFifty()
    {
        for (var i = 0; i <= 51; i++)
        {
            await NotifyAsync("info", $"note {i}");
        }

        var list = await ListAsync();
        list.Items.Length.ShouldBe(50);
        list.Items[0].Text.ShouldBe("note 51");
        list.UnreadCount.ShouldBe(52);
    }

    [Theory]
    [MemberData(nameof(InvalidCreates))]
    public async Task Create_InvalidRequest_Returns400(string kind, string text, string? tab)
    {
        var response = await _client.PostAsJsonAsync("/api/agent/notifications", new { Kind = kind, Text = text, Tab = tab });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reply_EmptyText_Returns400()
    {
        var notification = await NotifyAsync("question", "Anyone there?");

        var response = await _client.PostAsJsonAsync(
            $"/api/notifications/{notification.Id}/reply", new { Text = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkRead_UnknownId_Returns404()
    {
        var response = await _client.PostAsync($"/api/notifications/{Guid.NewGuid()}/read", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reply_UnknownId_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/notifications/{Guid.NewGuid()}/reply", new { Text = "answer" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_UnknownProject_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/agent/notifications", new
        {
            Kind = "info",
            Text = "orphan",
            ProjectId = Guid.NewGuid(),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public static TheoryData<string, string, string?> InvalidCreates => new()
    {
        { "urgent", "valid text", null },
        { "", "valid text", null },
        { "info", "", null },
        { "info", "   ", null },
        { "info", new string('x', 2001), null },
        { "info", "valid text", "board" },
    };

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"notifications-proj-{Guid.NewGuid():N}",
            DiskPath = _sandboxDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task<NotificationJson> NotifyAsync(
        string kind, string text, Guid? projectId = null, string? featureSlug = null, string? tab = null, string? from = null)
    {
        var response = await _client.PostAsJsonAsync("/api/agent/notifications", new
        {
            Kind = kind,
            Text = text,
            ProjectId = projectId,
            FeatureSlug = featureSlug,
            Tab = tab,
            From = from,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var notification = await response.Content.ReadFromJsonAsync<NotificationJson>(JsonOptions);
        notification.ShouldNotBeNull();
        return notification!;
    }

    private async Task<NotificationsJson> ListAsync() =>
        await _client.GetFromJsonAsync<NotificationsJson>("/api/notifications", JsonOptions)
        ?? throw new InvalidOperationException("list body was null");

    private async Task<NotificationJson> MarkReadAsync(Guid id)
    {
        var response = await _client.PostAsync($"/api/notifications/{id}/read", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<NotificationJson>(JsonOptions)
            ?? throw new InvalidOperationException("mark-read body was null");
    }

    private async Task<NotificationJson> ReplyAsync(Guid id, string text)
    {
        var response = await _client.PostAsJsonAsync($"/api/notifications/{id}/reply", new { Text = text });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<NotificationJson>(JsonOptions)
            ?? throw new InvalidOperationException("reply body was null");
    }

    private async Task<RepliesJson> RepliesAsync(Guid? projectId = null, bool includeRead = true)
    {
        var readFlag = includeRead ? "true" : "false";
        var query = projectId is { } id ? $"?projectId={id}&includeRead={readFlag}" : $"?includeRead={readFlag}";
        return await _client.GetFromJsonAsync<RepliesJson>($"/api/agent/notifications/replies{query}", JsonOptions)
            ?? throw new InvalidOperationException("replies body was null");
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record NotificationJson(
        Guid Id,
        Guid? ProjectId,
        string Kind,
        string? FeatureSlug,
        string? Tab,
        string Text,
        string Agent,
        DateTime CreatedAt,
        DateTime? ReadAt,
        string? ReplyText,
        DateTime? RepliedAt);

    public sealed record NotificationsJson(NotificationJson[] Items, int UnreadCount);

    public sealed record RepliesJson(NotificationJson[] Items);
}
