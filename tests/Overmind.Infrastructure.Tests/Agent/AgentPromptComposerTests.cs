using Microsoft.Extensions.AI;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentPromptComposerTests
{
    private readonly Conversation _conversation = Conversation.Create(null, "composer tests");

    [Fact]
    public async Task ComposeAsync_AllSourcesUp_IdentityRulesAndScopedNoteLandBeforeInstructions()
    {
        var composer = CreateComposer();

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var system = messages[0].Text;

        messages[0].Role.ShouldBe(ChatRole.System);
        system.ShouldContain("You are overmind-agent");
        system.ShouldContain("Rules:");
        system.ShouldNotContain(AgentPromptComposer.InstructionsHeader);
    }

    [Fact]
    public async Task ComposeAsync_ScopedConversation_IdentityCarriesTheProjectNote()
    {
        var composer = CreateComposer();
        var scoped = Conversation.Create(Guid.NewGuid(), "scoped chat");

        var messages = await composer.ComposeAsync(scoped, [], "status?", CancellationToken.None);

        messages[0].Text.ShouldContain("The conversation is scoped to project");
    }

    [Fact]
    public async Task ComposeAsync_UnscopedConversation_CarriesNoSnapshotJson()
    {
        var composer = CreateComposer();

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var system = messages[0].Text;

        system.ShouldNotContain("\"projects\"");
        system.ShouldNotContain("\"queue\"");
        system.ShouldNotContain("\"recentReplies\"");
        system.ShouldNotContain("\"memoryFacts\"");
        system.ShouldNotContain("Bootstrap snapshot");
    }

    [Fact]
    public async Task ComposeAsync_HistoryIsTrimmedToTheRollingWindowAndUserTextIsLast()
    {
        var composer = CreateComposer();
        var history = Enumerable.Range(0, AgentPromptComposer.MaxHistoryMessages + 6)
            .Select(index => ConversationMessage.Create(
                _conversation.Id,
                index % 2 == 0 ? ConversationMessage.UserRole : ConversationMessage.AssistantRole,
                $"msg {index}"))
            .ToArray();

        var messages = await composer.ComposeAsync(_conversation, history, "hello there", CancellationToken.None);

        messages.Count.ShouldBe(AgentPromptComposer.MaxHistoryMessages + 2);
        messages[1].Text.ShouldBe($"msg {history.Length - AgentPromptComposer.MaxHistoryMessages}");
        messages.Last().Role.ShouldBe(ChatRole.User);
        messages.Last().Text.ShouldBe("hello there");
    }

    [Fact]
    public async Task ComposeAsync_WithInstructions_AppendsDelimitedBlockAtTheEnd()
    {
        const string instructions = "Be terse. Skip pleasantries.";
        var composer = CreateComposer(instructions);

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var system = messages[0].Text;

        system.ShouldContain(AgentPromptComposer.InstructionsHeader);
        system.ShouldEndWith(instructions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ComposeAsync_WithoutInstructions_PromptEndsOnTheRulesBlock(string instructions)
    {
        var composer = CreateComposer(instructions);

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var system = messages[0].Text;

        system.ShouldNotContain(AgentPromptComposer.InstructionsHeader);
        system.TrimEnd().ShouldEndWith("instead of guessing.");
        system.ShouldEndWith(Environment.NewLine);
    }

    [Fact]
    public async Task ComposeAsync_OverCapInstructions_TruncatesAtTheCap()
    {
        var composer = CreateComposer(new string('i', 4500));

        var messages = await composer.ComposeAsync(_conversation, [], "status?", CancellationToken.None);
        var system = messages[0].Text;

        system.ShouldEndWith(new string('i', SettingDefaults.AgentInstructionsMaxLength));
        system.ShouldNotContain(new string('i', SettingDefaults.AgentInstructionsMaxLength + 1));
    }

    private static AgentPromptComposer CreateComposer(string instructions = "")
    {
        var settings = Substitute.For<IAgentRunSettingsProvider>();
        settings
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AgentRunSettings("overmind-agent", 8192, 0, instructions));
        return new AgentPromptComposer(settings);
    }
}
