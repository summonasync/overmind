using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[Trait("Host", "Kestrel")]
[Trait("Surface", "Browser")]
[Collection("overmind kestrel browser")]
public sealed class SendPathOrderingBrowserTests(BrowserHostFixture fixture)
{
    private const string SelectionStorageKey = "overmind.agent-chat.selected-conversation";
    private const string LoadedEmptyMarker = "No messages yet";
    private const string UserRowMarker = "data-agent-message-role=\"user\"";
    private const string ToolRowMarker = "data-agent-message-role=\"tool\"";
    private const string AssistantRowMarker = "data-agent-message-role=\"assistant\"";

    private static readonly TimeSpan MountBudget = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan StreamBudget = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CompletionBudget = TimeSpan.FromSeconds(30);

    [BrowserFact]
    public async Task SentTurn_ToolFrameHeldMidStream_RendersToolRowBeforeAssistantRow()
    {
        fixture.EnsureUsable();
        var page = fixture.Page!;
        var harness = fixture.Harness!;
        var gate = fixture.Factory!.Services.GetRequiredService<KestrelTurnStreamGate>();

        await harness.SeedStateAsync(SelectionStorageKey, fixture.ConversationId.ToString(), CancellationToken.None);
        await page.NavigateAsync($"{fixture.BaseUrl}/agent-chat?project={fixture.ProjectId}", CancellationToken.None);
        await WaitUntilSnapshotAsync(harness, dom => dom.Contains(LoadedEmptyMarker), MountBudget, "the loaded empty conversation");

        gate.Arm();
        try
        {
            await harness.TypeIntoComposerAsync("stub browser send-path probe", CancellationToken.None);
            await harness.ClickSendAsync(CancellationToken.None);

            var midStream = await WaitUntilSnapshotAsync(
                harness, dom => dom.Contains(ToolRowMarker), StreamBudget, "the live tool row");
            midStream.ShouldContain(UserRowMarker);
            midStream.ShouldContain("planning_task_queue");
            midStream.ShouldNotContain(AssistantRowMarker);

            gate.Release();
            var completed = await WaitUntilSnapshotAsync(
                harness, dom => dom.Contains(AssistantRowMarker), CompletionBudget, "the assistant row after completion");
            completed.ShouldContain(UserRowMarker);
            completed.ShouldContain("stub delta one");
            completed.ShouldContain("stub delta two");
        }
        finally
        {
            gate.Release();
        }
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
}
