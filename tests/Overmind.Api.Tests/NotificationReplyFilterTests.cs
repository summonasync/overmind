using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class NotificationReplyFilterTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotificationReplyFilterTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-reply-filters-{Guid.NewGuid():N}");
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
    public async Task Replies_Since_ReturnsOnlyRepliesAfterTheTimestamp()
    {
        var early = await SeedRepliedAsync("question", "early", "151-alpha");
        var mid = await SeedRepliedAsync("info", "mid", "151-alpha");
        var late = await SeedRepliedAsync("approval", "late");

        var sinceEarly = await RepliesAsync(since: early.RepliedAt!.Value);
        sinceEarly.Items.Select(i => i.Id).ToArray().ShouldBe([late.Id, mid.Id]);

        var sinceTheDawn = await RepliesAsync(since: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        sinceTheDawn.Items.Select(i => i.Id).ToArray().ShouldBe([late.Id, mid.Id, early.Id]);

        var sinceFuture = await RepliesAsync(since: new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        sinceFuture.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Replies_InvalidSince_Returns400()
    {
        var response = await _client.GetAsync("/api/agent/notifications/replies?since=not-a-timestamp");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Replies_Kind_NarrowsToThatKindCaseInsensitively()
    {
        var question = await SeedRepliedAsync("question", "which db?");
        await SeedRepliedAsync("info", "deployed");
        var approval = await SeedRepliedAsync("approval", "activate the kit?");

        var approvals = await RepliesAsync(kind: "APPROVAL");
        approvals.Items.Select(i => i.Id).ToArray().ShouldBe([approval.Id]);

        var questions = await RepliesAsync(kind: "question");
        questions.Items.Single().Id.ShouldBe(question.Id);
    }

    [Fact]
    public async Task Replies_UnknownKind_Returns400()
    {
        var response = await _client.GetAsync("/api/agent/notifications/replies?kind=urgent");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Replies_FeatureSlug_MatchesTheExactSlugOnly()
    {
        var first = await SeedRepliedAsync("info", "one", "151-alpha");
        var second = await SeedRepliedAsync("question", "two", "151-alpha");
        await SeedRepliedAsync("approval", "three");

        var alpha = await RepliesAsync(featureSlug: "151-alpha");
        alpha.Items.Select(i => i.Id).ToArray().ShouldBe([second.Id, first.Id]);

        var none = await RepliesAsync(featureSlug: "nope-not-a-slug");
        none.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Replies_CombinedFilters_Intersect()
    {
        var early = await SeedRepliedAsync("question", "early", "151-alpha");
        var mid = await SeedRepliedAsync("info", "mid", "151-alpha");
        var loose = await SeedRepliedAsync("info", "late");

        var combined = await RepliesAsync(since: early.RepliedAt!.Value, kind: "info", featureSlug: "151-alpha");
        combined.Items.Select(i => i.Id).ToArray().ShouldBe([mid.Id]);
        combined.Items.ShouldNotContain(i => i.Id == loose.Id);
    }

    [Fact]
    public async Task Replies_OverNarrowCombination_ReturnsEmptyArrayNotError()
    {
        await SeedRepliedAsync("question", "only question", "151-alpha");
        var late = await SeedRepliedAsync("info", "late");

        var narrowed = await RepliesAsync(
            since: late.RepliedAt!.Value, kind: "question", featureSlug: "151-alpha");

        narrowed.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Replies_IncludeReadFalse_ComposesWithFilters()
    {
        var first = await SeedRepliedAsync("info", "one", "151-alpha");
        var second = await SeedRepliedAsync("info", "two", "151-alpha");

        var all = await RepliesAsync(featureSlug: "151-alpha");
        all.Items.Select(i => i.Id).ToArray().ShouldBe([second.Id, first.Id]);

        var unread = await RepliesAsync(featureSlug: "151-alpha", includeRead: false);
        unread.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Replies_FilteredBody_KeepsTheUnchangedShape()
    {
        var replied = await SeedRepliedAsync("question", "shape check", "151-alpha");

        using var doc = JsonDocument.Parse(
            await _client.GetStringAsync("/api/agent/notifications/replies?kind=QUESTION&featureSlug=151-alpha"));

        doc.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(["items"]);
        var item = doc.RootElement.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(replied.Id);
        item.GetProperty("replyText").GetString().ShouldBe("answer to shape check");
        item.EnumerateObject().Select(p => p.Name).ShouldBe(
        [
            "id", "projectId", "kind", "featureSlug", "tab", "text", "agent",
            "createdAt", "readAt", "replyText", "repliedAt", "hiddenAt"
        ]);
    }

    private async Task<NotificationJson> SeedRepliedAsync(string kind, string text, string? featureSlug = null)
    {
        var notification = await NotifyAsync(kind, text, featureSlug);
        return await ReplyAsync(notification.Id, $"answer to {text}");
    }

    private async Task<NotificationJson> NotifyAsync(string kind, string text, string? featureSlug = null)
    {
        var response = await _client.PostAsJsonAsync("/api/agent/notifications", new
        {
            Kind = kind,
            Text = text,
            FeatureSlug = featureSlug,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<NotificationJson>(JsonOptions)
            ?? throw new InvalidOperationException("create body was null");
    }

    private async Task<NotificationJson> ReplyAsync(Guid id, string text)
    {
        var response = await _client.PostAsJsonAsync($"/api/notifications/{id}/reply", new { Text = text });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<NotificationJson>(JsonOptions)
            ?? throw new InvalidOperationException("reply body was null");
    }

    private async Task<RepliesJson> RepliesAsync(
        DateTime? since = null, string? kind = null, string? featureSlug = null, bool includeRead = true)
    {
        var query = new List<string> { $"includeRead={(includeRead ? "true" : "false")}" };
        if (since is { } stamp)
        {
            query.Add($"since={Uri.EscapeDataString(stamp.ToString("O"))}");
        }

        if (kind is not null)
        {
            query.Add($"kind={kind}");
        }

        if (featureSlug is not null)
        {
            query.Add($"featureSlug={featureSlug}");
        }

        return await _client.GetFromJsonAsync<RepliesJson>(
                $"/api/agent/notifications/replies?{string.Join("&", query)}", JsonOptions)
            ?? throw new InvalidOperationException("replies body was null");
    }

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
        DateTime? HiddenAt);

    public sealed record RepliesJson(NotificationJson[] Items);
}
