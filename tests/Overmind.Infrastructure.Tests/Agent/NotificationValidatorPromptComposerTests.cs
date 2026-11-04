using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class NotificationValidatorPromptComposerTests
{
    private readonly NotificationValidatorPromptComposer _composer = new();

    [Fact]
    public async Task ComposeAsync_SystemPromptStaysWithinTheThousandTokenBudget()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Telegram validator");

        var messages = await _composer.ComposeAsync(conversation, [], "instruction", CancellationToken.None);

        var system = messages[0].Text;
        system.ShouldContain("notification-validator");
        system.Length.ShouldBeLessThanOrEqualTo(4000);
    }

    [Fact]
    public async Task ComposeAsync_ScopesThePromptToTheConversationProject()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), "Telegram validator");

        var messages = await _composer.ComposeAsync(conversation, [], "instruction", CancellationToken.None);

        messages[0].Text.ShouldContain(conversation.ProjectId!.Value.ToString());
    }

    [Fact]
    public async Task ComposeAsync_AppendsTheInstructionAsTheUserTurn()
    {
        var conversation = Conversation.Create(null, "Telegram validator");
        var history = new List<ConversationMessage>
        {
            ConversationMessage.Create(conversation.Id, ConversationMessage.UserRole, "first instruction"),
            ConversationMessage.Create(conversation.Id, ConversationMessage.AssistantRole, "validated"),
            ConversationMessage.Create(conversation.Id, ConversationMessage.ToolRole, "{\"tool\":\"planning_validate\"}"),
        };

        var messages = await _composer.ComposeAsync(conversation, history, "second instruction", CancellationToken.None);

        messages[messages.Count - 1].Text.ShouldBe("second instruction");
        messages.ShouldNotContain(message => message.Text == "{\"tool\":\"planning_validate\"}");
    }

}
