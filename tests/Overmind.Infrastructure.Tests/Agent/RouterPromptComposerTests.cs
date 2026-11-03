using Microsoft.Extensions.AI;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class RouterPromptComposerTests
{
    private readonly Conversation _conversation = Conversation.Create(null, "router tests");

    [Fact]
    public async Task ComposeClassificationAsync_SystemPromptCarriesCharterTableAndContract()
    {
        var composer = new RouterPromptComposer();
        var hotHead = new[]
        {
            MakeRoute("show wave chat unread", "wave"),
            MakeRoute("board queue status", "planning"),
        };

        var messages = await composer.ComposeClassificationAsync(
            _conversation, [], hotHead, "hello", CancellationToken.None);
        var system = messages[0].Text;

        messages[0].Role.ShouldBe(ChatRole.System);
        system.ShouldContain("intent router");
        system.ShouldContain("\"show wave chat unread\" -> worker (wave)");
        system.ShouldContain("\"board queue status\" -> worker (planning)");
        system.ShouldContain(RouterPromptComposer.ClassificationContract);
    }

    [Fact]
    public async Task ComposeClassificationAsync_ToolRowsNeverEnterTheDialogue()
    {
        var composer = new RouterPromptComposer();
        var history = new[]
        {
            ConversationMessage.Create(_conversation.Id, ConversationMessage.UserRole, "ask one"),
            ConversationMessage.Create(_conversation.Id, ConversationMessage.ToolRole, "{\"tool\":\"planning_task_queue\"}"),
            ConversationMessage.Create(_conversation.Id, ConversationMessage.AssistantRole, "answer one"),
            ConversationMessage.Create(_conversation.Id, ConversationMessage.UserRole, "ask two"),
        };

        var messages = await composer.ComposeClassificationAsync(_conversation, history, [], "next ask", CancellationToken.None);

        messages.Count.ShouldBe(5);
        messages.ShouldNotContain(message => message.Text!.Contains("planning_task_queue"));
        messages[1].Text.ShouldBe("ask one");
        messages[2].Text.ShouldBe("answer one");
        messages[3].Text.ShouldBe("ask two");
        messages[4].Text.ShouldBe("next ask");
    }

    [Fact]
    public async Task ComposeClassificationAsync_DialogueWindowIsCapped()
    {
        var composer = new RouterPromptComposer();
        var history = Enumerable.Range(0, RouterPromptComposer.MaxHistoryMessages + 10)
            .Select(index => ConversationMessage.Create(
                _conversation.Id,
                index % 2 == 0 ? ConversationMessage.UserRole : ConversationMessage.AssistantRole,
                $"msg {index}"))
            .ToArray();

        var messages = await composer.ComposeClassificationAsync(_conversation, history, [], "latest", CancellationToken.None);

        messages.Count.ShouldBe(RouterPromptComposer.MaxHistoryMessages + 2);
        messages.Last().Text.ShouldBe("latest");
    }

    [Fact]
    public async Task ComposeChatAsync_SystemPromptOffersNoTools()
    {
        var composer = new RouterPromptComposer();

        var messages = await composer.ComposeChatAsync(_conversation, [], [], "hi", CancellationToken.None);

        messages[0].Text.ShouldContain("You have no tools");
        messages[0].Text.ShouldNotContain(RouterPromptComposer.ClassificationContract);
    }

    [Fact]
    public async Task ComposeClassificationAsync_StaysUnderTheTokenBudgetWithPopulatedTableAndDialogue()
    {
        var composer = new RouterPromptComposer();
        var hotHead = Enumerable.Range(0, IntentRouter.HotHeadSize)
            .Select(index => MakeRoute(
                $"route row {index} for the wave chat unread count and the planning queue", "wave"))
            .ToArray();
        var history = Enumerable.Range(0, RouterPromptComposer.MaxHistoryMessages)
            .Select(index => ConversationMessage.Create(
                _conversation.Id,
                index % 2 == 0 ? ConversationMessage.UserRole : ConversationMessage.AssistantRole,
                "Could you show the wave chat unread count and the planning queue for the router feature? " +
                "I also want the latest review audits and the estimate workbook before the sprint demo."))
            .ToArray();

        var messages = await composer.ComposeClassificationAsync(
            _conversation, history, hotHead, "show me the wave chat unread count and the planning queue", CancellationToken.None);
        var totalTokens = messages.Sum(message => RouterTokenBudget.EstimateTokens(message.Text));

        totalTokens.ShouldBeLessThanOrEqualTo(RouterTokenBudget.MaxPromptTokens);
    }

    [Fact]
    public async Task ComposeClassificationAsync_HotHeadBlockIsCappedWhenRowsAreHuge()
    {
        var composer = new RouterPromptComposer();
        var oversizedPattern = string.Join(' ', Enumerable.Repeat("pattern", 60));
        var hotHead = Enumerable.Range(0, 12).Select(_ => MakeRoute(oversizedPattern, "planning")).ToArray();

        var messages = await composer.ComposeClassificationAsync(_conversation, [], hotHead, "ask", CancellationToken.None);
        var system = messages[0].Text;

        RouterTokenBudget.EstimateTokens(system).ShouldBeLessThanOrEqualTo(700);
    }

    private static AgentRouteRecord MakeRoute(string pattern, string domain) =>
        new(
            Guid.NewGuid(),
            AgentRouteText.Normalize(pattern),
            domain,
            RoutingTargets.Worker,
            "test row",
            AgentRouteStatus.Provisional,
            1,
            AgentRouteSource.Learned,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
