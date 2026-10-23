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

    [Fact]
    public async Task List_AwaitingTrue_ReturnsOnlyUnansweredApprovalAndQuestion()
    {
        var answered = await NotifyAsync("approval", "Approve the deploy?");
        await ReplyAsync(answered.Id, "go");

        var hidden = await NotifyAsync("question", "Hidden conversation");
        await ReplyAsync(hidden.Id, "later");
        (await ClearConversationsAsync()).Hidden.ShouldBe(2);

        await NotifyAsync("info", "just a note");

        var readUnanswered = await NotifyAsync("question", "Read but undecided");
        await MarkReadAsync(readUnanswered.Id);

        var approvalOpen = await NotifyAsync("approval", "Approve the spec");
        var questionOpen = await NotifyAsync("question", "Which database?");

        Guid[] expected = [questionOpen.Id, approvalOpen.Id, readUnanswered.Id];

        var awaiting = await ListAsync(awaiting: true);
        awaiting.Items.Select(i => i.Id).ToArray().ShouldBe(expected);
        awaiting.Items.ShouldNotContain(i => i.ReplyText != null);
        awaiting.Items.ShouldNotContain(i => i.HiddenAt != null);
        awaiting.Items.ShouldNotContain(i => i.Kind == "info");
        awaiting.UnreadCount.ShouldBe(3);
    }

    [Fact]
    public async Task List_Take_DefaultStaysAtFiftyAndExplicitTakeWidensTheWindow()
    {
        for (var i = 0; i < 60; i++)
        {
            await NotifyAsync("info", $"note {i}");
        }

        var defaultList = await ListAsync();
        defaultList.Items.Length.ShouldBe(50);
        defaultList.Items[0].Text.ShouldBe("note 59");
        defaultList.UnreadCount.ShouldBe(60);

        var widened = await ListAsync(take: 200);
        widened.Items.Length.ShouldBe(60);
        widened.Items[0].Text.ShouldBe("note 59");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public async Task List_TakeOutOfRange_Returns400(int take)
    {
        var response = await _client.GetAsync($"/api/notifications?take={take}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
    public async Task Create_TextOverLimit_Returns400NamingFieldLimitAndActualSize()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/notifications", new { Kind = "info", Text = new string('x', 2143) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.Keys.ShouldContain("Text");
        problem.Errors["Text"].ShouldBe(["Text exceeds the 2000 character limit (got 2143)."]);
    }

    [Fact]
    public async Task Create_TextExactlyAtLimit_Succeeds()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/notifications", new { Kind = "info", Text = new string('x', 2000) });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Reply_TextOverLimit_Returns400NamingFieldLimitAndActualSize()
    {
        var notification = await NotifyAsync("question", "Anyone there?");

        var response = await _client.PostAsJsonAsync(
            $"/api/notifications/{notification.Id}/reply", new { Text = new string('y', 2143) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.Keys.ShouldContain("Text");
        problem.Errors["Text"].ShouldBe(["Text exceeds the 2000 character limit (got 2143)."]);
    }

    [Fact]
    public async Task Reply_TextExactlyAtLimit_Succeeds()
    {
        var notification = await NotifyAsync("question", "Anyone there?");

        var response = await _client.PostAsJsonAsync(
            $"/api/notifications/{notification.Id}/reply", new { Text = new string('y', 2000) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_RemovesRow_AndSecondCallReturns404()
    {
        var removed = await NotifyAsync("info", "delete me");
        var survivor = await NotifyAsync("info", "keep me");

        (await DeleteNotificationAsync(removed.Id)).ShouldBe(HttpStatusCode.NoContent);
        (await DeleteNotificationAsync(removed.Id)).ShouldBe(HttpStatusCode.NotFound);

        var list = await ListAsync();
        list.Items.Single().Id.ShouldBe(survivor.Id);
        list.UnreadCount.ShouldBe(1);
    }

    [Fact]
    public async Task ReadAll_MarksOnlyUnread_AndReturnsExactCount()
    {
        var read = await NotifyAsync("info", "already read");
        var readAt = (await MarkReadAsync(read.Id)).ReadAt;
        var first = await NotifyAsync("question", "one");
        var second = await NotifyAsync("info", "two");

        (await ReadAllAsync()).Marked.ShouldBe(2);
        (await ReadAllAsync()).Marked.ShouldBe(0);

        var list = await ListAsync();
        list.UnreadCount.ShouldBe(0);
        list.Items.Length.ShouldBe(3);
        list.Items.Single(i => i.Id == read.Id).ReadAt.ShouldBe(readAt);
        list.Items.Single(i => i.Id == first.Id).ReadAt.ShouldNotBeNull();
        list.Items.Single(i => i.Id == second.Id).ReadAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ClearRead_DeletesOnlyReadNotifications()
    {
        var read = await NotifyAsync("info", "read");
        await MarkReadAsync(read.Id);
        var unread = await NotifyAsync("approval", "still unread");

        (await ClearReadAsync()).Deleted.ShouldBe(1);
        (await ClearReadAsync()).Deleted.ShouldBe(0);

        var list = await ListAsync();
        list.Items.Single().Id.ShouldBe(unread.Id);
        list.UnreadCount.ShouldBe(1);
    }

    [Fact]
    public async Task ClearRead_KeepsRepliedProofRows_UnreadApprovalSurvivesReadAll()
    {
        var approval = await NotifyAsync("approval", "Approve the deploy?");
        var replied = await NotifyAsync("question", "Ship or hold?");
        await ReplyAsync(replied.Id, "ship it");

        (await ClearReadAsync()).Deleted.ShouldBe(0);

        var afterClear = await ListAsync();
        afterClear.Items.Count().ShouldBe(2);
        afterClear.UnreadCount.ShouldBe(1);

        (await ReadAllAsync()).Marked.ShouldBe(1);

        var afterReadAll = await ListAsync();
        afterReadAll.UnreadCount.ShouldBe(0);

        var deleteReplied = await _client.DeleteAsync($"/api/notifications/{replied.Id}");
        deleteReplied.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterExplicitDelete = await ListAsync();
        afterExplicitDelete.Items.Single().Id.ShouldBe(approval.Id);
    }

    [Fact]
    public async Task ClearConversations_HidesOnlyRepliedRows_AndKeepsTheRestVisible()
    {
        var asked = await NotifyAsync("question", "Ship or hold?");
        await ReplyAsync(asked.Id, "ship");
        var read = await NotifyAsync("info", "read but not a conversation");
        await MarkReadAsync(read.Id);
        var unread = await NotifyAsync("approval", "still open");

        (await ClearConversationsAsync()).Hidden.ShouldBe(1);

        var list = await ListAsync();
        list.Items.Length.ShouldBe(2);
        list.Items.ShouldNotContain(i => i.Id == asked.Id);
        list.Items.ShouldContain(i => i.Id == read.Id);
        list.Items.ShouldContain(i => i.Id == unread.Id);
        list.UnreadCount.ShouldBe(1);
    }

    [Fact]
    public async Task ClearConversations_SecondCall_HidesNothingMore()
    {
        var asked = await NotifyAsync("question", "Which database?");
        await ReplyAsync(asked.Id, "SQLite");

        (await ClearConversationsAsync()).Hidden.ShouldBe(1);
        (await ClearConversationsAsync()).Hidden.ShouldBe(0);

        var archive = await ListAsync(includeHidden: true);
        var hidden = archive.Items.Single(i => i.Id == asked.Id);
        hidden.HiddenAt.ShouldNotBeNull();
        archive.UnreadCount.ShouldBe(0);
    }

    [Fact]
    public async Task Replies_ReturnHiddenConversations_Unfiltered()
    {
        var asked = await NotifyAsync("question", "Anyone there?");
        await ReplyAsync(asked.Id, "yes");
        (await ClearConversationsAsync()).Hidden.ShouldBe(1);

        var replies = await RepliesAsync();
        replies.Items.Single().Id.ShouldBe(asked.Id);
        replies.Items.Single().ReplyText.ShouldBe("yes");
    }

    [Fact]
    public async Task ClearRead_DoesNotDeleteHiddenConversations()
    {
        var asked = await NotifyAsync("question", "Ship or hold?");
        await ReplyAsync(asked.Id, "ship");
        (await ClearConversationsAsync()).Hidden.ShouldBe(1);

        (await ClearReadAsync()).Deleted.ShouldBe(0);

        var archive = await ListAsync(includeHidden: true);
        archive.Items.ShouldContain(i => i.Id == asked.Id);
    }

    [Fact]
    public async Task List_DropsHiddenRows_BeforeTheCapacityWindow()
    {
        for (var i = 0; i <= 51; i++)
        {
            await NotifyAsync("info", $"note {i}");
        }

        var beforeHide = await ListAsync();
        await ReplyAsync(beforeHide.Items[0].Id, "answered");
        await ReplyAsync(beforeHide.Items[1].Id, "also answered");
        (await ClearConversationsAsync()).Hidden.ShouldBe(2);

        var list = await ListAsync();
        list.Items.Length.ShouldBe(50);
        list.Items[0].Text.ShouldBe("note 49");
        list.Items.Last().Text.ShouldBe("note 0");
        list.Items.ShouldNotContain(i => i.Text == "note 51");
        list.Items.ShouldNotContain(i => i.Text == "note 50");
        list.UnreadCount.ShouldBe(50);

        var archive = await ListAsync(includeHidden: true);
        archive.Items.Length.ShouldBe(50);
        archive.Items[0].Text.ShouldBe("note 51");
        archive.Items.ShouldContain(i => i.Id == beforeHide.Items[0].Id);
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

    [Fact]
    public async Task Create_WithFeatureSlugTabAndProject_ReturnsAbsoluteDashboardUrl()
    {
        var project = await CreateProjectAsync();

        var created = await NotifyAsync("approval", "Approve the spec", project.Id, "agent-notifications", "spec");

        created.Url.ShouldBe($"http://overmind.localhost/dashboard?project={project.Id}&feature=agent-notifications&tab=spec");
        Uri.TryCreate(created.Url, UriKind.Absolute, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithFeatureSlugWithoutProject_OmitsProjectParam()
    {
        var created = await NotifyAsync("info", "no project attached", featureSlug: "materialize-status");

        created.Url.ShouldBe("http://overmind.localhost/dashboard?feature=materialize-status");
    }

    [Fact]
    public async Task Create_WithoutFeatureSlug_OmitsUrlField()
    {
        var response = await _client.PostAsJsonAsync("/api/agent/notifications", new { Kind = "info", Text = "bare" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.TryGetProperty("url", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task HumanDoor_ListReadReply_DoNotCarryUrl()
    {
        var project = await CreateProjectAsync();
        var created = await NotifyAsync("question", "pick one", project.Id, "agent-notifications", "spec");

        using var listDoc = JsonDocument.Parse(await _client.GetStringAsync("/api/notifications"));
        foreach (var item in listDoc.RootElement.GetProperty("items").EnumerateArray())
        {
            item.TryGetProperty("url", out _).ShouldBeFalse();
        }

        var read = await _client.PostAsync($"/api/notifications/{created.Id}/read", null);
        using var readDoc = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        readDoc.RootElement.TryGetProperty("url", out _).ShouldBeFalse();

        var reply = await _client.PostAsJsonAsync($"/api/notifications/{created.Id}/reply", new { Text = "pick two" });
        using var replyDoc = JsonDocument.Parse(await reply.Content.ReadAsStringAsync());
        replyDoc.RootElement.TryGetProperty("url", out _).ShouldBeFalse();
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

    private async Task<NotificationsJson> ListAsync(bool includeHidden = false, bool awaiting = false, int? take = null)
    {
        var query = new List<string>();
        if (includeHidden)
        {
            query.Add("includeHidden=true");
        }

        if (awaiting)
        {
            query.Add("awaiting=true");
        }

        if (take is { } size)
        {
            query.Add($"take={size}");
        }

        var suffix = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;
        return await _client.GetFromJsonAsync<NotificationsJson>($"/api/notifications{suffix}", JsonOptions)
            ?? throw new InvalidOperationException("list body was null");
    }

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

    private async Task<HttpStatusCode> DeleteNotificationAsync(Guid id)
    {
        var response = await _client.DeleteAsync($"/api/notifications/{id}");
        return response.StatusCode;
    }

    private async Task<ReadAllJson> ReadAllAsync()
    {
        var response = await _client.PostAsync("/api/notifications/read-all", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<ReadAllJson>(JsonOptions)
            ?? throw new InvalidOperationException("read-all body was null");
    }

    private async Task<ClearReadJson> ClearReadAsync()
    {
        var response = await _client.PostAsync("/api/notifications/clear-read", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<ClearReadJson>(JsonOptions)
            ?? throw new InvalidOperationException("clear-read body was null");
    }

    private async Task<HiddenJson> ClearConversationsAsync()
    {
        var response = await _client.PostAsync("/api/notifications/clear-conversations", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<HiddenJson>(JsonOptions)
            ?? throw new InvalidOperationException("clear-conversations body was null");
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
        DateTime? RepliedAt,
        DateTime? HiddenAt,
        string? Url);

    public sealed record NotificationsJson(NotificationJson[] Items, int UnreadCount);

    public sealed record RepliesJson(NotificationJson[] Items);

    public sealed record ReadAllJson(int Marked);

    public sealed record ClearReadJson(int Deleted);

    public sealed record HiddenJson(int Hidden);

    private sealed record ValidationProblemJson(Dictionary<string, string[]> Errors);
}
