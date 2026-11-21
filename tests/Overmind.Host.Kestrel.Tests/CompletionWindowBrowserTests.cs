using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Host.AgentChat;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[Trait("Host", "Kestrel")]
[Trait("Surface", "Browser")]
[Collection("overmind kestrel browser")]
public sealed class CompletionWindowBrowserTests(BrowserHostFixture fixture)
{
    private const string SelectionStorageKey = "overmind.agent-chat.selected-conversation";
    private const string LoadedEmptyMarker = "No messages yet";
    private const string UserRowMarker = "data-agent-message-role=\"user\"";
    private const string AssistantRowMarker = "data-agent-message-role=\"assistant\"";

    private const int BootAttachHoldMs = 1000;
    private const int HistoryDelayMs = 800;
    private const int AttachDelayMs = 2000;

    private static readonly TimeSpan MountBudget = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan StreamBudget = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HoldBudget = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CompletionBudget = TimeSpan.FromSeconds(30);

    [BrowserFact]
    public async Task Reload_IntoCompletionWindow_RendersPersistedReplyAfterIdleAttachWithoutSecondLoad()
    {
        fixture.EnsureUsable();
        var page = fixture.Page!;
        var harness = fixture.Harness!;
        var factory = fixture.Factory!;
        var streamGate = factory.Services.GetRequiredService<KestrelTurnStreamGate>();
        var delayGate = factory.Services.GetRequiredService<KestrelResponseDelayGate>();
        var (projectId, conversationId) = await SeedFreshConversationAsync(factory);
        var chatUrl = $"{fixture.BaseUrl}/agent-chat?project={projectId}";
        var historyPath = $"/api/agent/conversations/{conversationId}/messages";
        var attachPath = $"/api/agent/conversations/{conversationId}/turn/stream";

        await harness.SeedStateAsync(SelectionStorageKey, conversationId.ToString(), CancellationToken.None);
        delayGate.Arm(attachPath, BootAttachHoldMs);
        try
        {
            await page.NavigateAsync(chatUrl, CancellationToken.None);
            await WaitUntilSnapshotAsync(harness, dom => dom.Contains(LoadedEmptyMarker), MountBudget, "the loaded empty conversation");
            await WaitUntilStateAsync(
                async () => await CountResourceRequestsAsync(page, "/messages", CancellationToken.None) == 2
                    && await CountResourceRequestsAsync(page, "/turn/stream", CancellationToken.None) >= 1,
                HoldBudget,
                "the boot settle (attach answered and its history refetch landed)");
            delayGate.DisarmAll();
            var consumedBaseline = delayGate.ConsumedCount;

            streamGate.Arm();
            await harness.TypeIntoComposerAsync("completion window probe", CancellationToken.None);
            await harness.ClickSendAsync(CancellationToken.None);
            await WaitUntilSnapshotAsync(
                harness, dom => dom.Contains("planning_task_queue"), StreamBudget, "the live tool row before the reload");

            delayGate.Arm(historyPath, HistoryDelayMs, delayAfterHandler: true);
            delayGate.Arm(attachPath, AttachDelayMs);
            await page.NavigateAsync(chatUrl, CancellationToken.None);
            await WaitUntilStateAsync(
                async () => delayGate.ConsumedCount == consumedBaseline + 2,
                HoldBudget,
                "both reload requests held inside the armed window");

            streamGate.Release();
            await WaitUntilStateAsync(
                async () => await TurnIsSettledAsync(factory, conversationId, CancellationToken.None),
                CompletionBudget,
                "the persisted assistant reply and the deregistered turn");

            var stale = await WaitUntilSnapshotAsync(
                harness,
                dom => dom.Contains(UserRowMarker) && !dom.Contains(AssistantRowMarker),
                CompletionBudget,
                "the stale pre-completion history rendered without the assistant row");
            stale.ShouldContain("completion window probe");
            stale.ShouldNotContain("stub delta one");

            var completed = await WaitUntilSnapshotAsync(
                harness,
                dom => dom.Contains(AssistantRowMarker) && dom.Contains("stub delta one"),
                CompletionBudget,
                "the assistant reply rendered after the idle attach");
            completed.ShouldContain(UserRowMarker);
            completed.ShouldContain("stub delta two");

            (await CountResourceRequestsAsync(page, "/messages", CancellationToken.None))
                .ShouldBe(2, customMessage: await ReadChatWireDumpAsync(page, CancellationToken.None)
                    + " | consumed: " + string.Join(" ;; ", delayGate.ConsumedPaths));
            (await ReadAttachResponseStatusAsync(page, CancellationToken.None)).ShouldBe(204);
        }
        finally
        {
            streamGate.Release();
            delayGate.DisarmAll();
        }
    }

    private static async Task<(Guid ProjectId, Guid ConversationId)> SeedFreshConversationAsync(KestrelApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var project = Project.Create(
            $"kestrel-completion-{Guid.NewGuid():N}",
            Path.Combine(Path.GetTempPath(), "overmind-kestrel-completion", Guid.NewGuid().ToString("N")));
        var conversation = Conversation.Create(project.Id, "completion window conversation");
        db.Projects.Add(project);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return (project.Id, conversation.Id);
    }

    private static async Task<bool> TurnIsSettledAsync(KestrelApiFactory factory, Guid conversationId, CancellationToken ct)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var turns = scope.ServiceProvider.GetRequiredService<ConversationTurnRegistry>();
        var replyPersisted = await db.ConversationMessages.AnyAsync(
            message => message.ConversationId == conversationId && message.Role == ConversationMessage.AssistantRole, ct);
        return replyPersisted && turns.Find(conversationId) is null;
    }

    private static async Task<int> CountResourceRequestsAsync(CdpPage page, string pathFragment, CancellationToken ct)
    {
        var script =
            "performance.getEntriesByType('resource')"
            + ".filter((entry) => entry.name.indexOf('" + pathFragment + "') !== -1).length";
        var count = await page.EvaluateAsync(script, ct);
        return count.GetInt32();
    }

    private static async Task<int> ReadAttachResponseStatusAsync(CdpPage page, CancellationToken ct)
    {
        var script =
            "(() => { const entries = performance.getEntriesByType('resource')"
            + ".filter((entry) => entry.name.indexOf('/turn/stream') !== -1);"
            + " return entries.length === 0 ? 0 : (entries[entries.length - 1].responseStatus ?? 0); })()";
        var status = await page.EvaluateAsync(script, ct);
        return status.GetInt32();
    }

    private static async Task<string> ReadChatWireDumpAsync(CdpPage page, CancellationToken ct)
    {
        var script =
            "JSON.stringify(performance.getEntriesByType('resource')"
            + ".filter((entry) => entry.name.indexOf('/api/') !== -1)"
            + ".map((entry) => ({ path: entry.name.split('/api/')[1], status: entry.responseStatus ?? null, t: entry.startTime })))";
        var dump = await page.EvaluateAsync(script, ct);
        return dump.GetString() ?? "(no wire entries)";
    }

    private static async Task<string> WaitUntilSnapshotAsync(
        CdpChatHarness harness,
        Func<string, bool> ready,
        TimeSpan budget,
        string description)
    {
        var deadline = DateTime.UtcNow + budget;
        while (true)
        {
            var dom = await harness.SnapshotDomAsync(CancellationToken.None);
            if (ready(dom))
            {
                return dom;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(
                    $"browser DOM never reached {description} within {budget.TotalSeconds}s");
            }

            await Task.Delay(200);
        }
    }

    private static async Task WaitUntilStateAsync(Func<Task<bool>> ready, TimeSpan budget, string description)
    {
        var deadline = DateTime.UtcNow + budget;
        while (true)
        {
            if (await ready())
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(
                    $"server state never reached {description} within {budget.TotalSeconds}s");
            }

            await Task.Delay(100);
        }
    }
}
