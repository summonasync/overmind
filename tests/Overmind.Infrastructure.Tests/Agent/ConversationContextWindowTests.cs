using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class ConversationContextWindowTests
{
    [Fact]
    public void Apply_BalancedHistory_TakesLastTenOfEachRoleInOrder()
    {
        var history = new List<ConversationMessage>();
        for (var turn = 1; turn <= 30; turn++)
        {
            history.Add(Message(ConversationMessage.UserRole, $"user {turn}"));
            history.Add(Message(ConversationMessage.AssistantRole, $"assistant {turn}"));
        }

        var window = ConversationContextWindow.Apply(history);

        var expected = new List<string>();
        for (var turn = 21; turn <= 30; turn++)
        {
            expected.Add($"user {turn}");
            expected.Add($"assistant {turn}");
        }

        window.Count.ShouldBe(ConversationContextWindow.MaxMessages);
        window.Select(m => m.Content).ShouldBe(expected);
        window.Select(m => m.Id).ToHashSet().IsSubsetOf(history.Select(m => m.Id)).ShouldBeTrue();
    }

    [Fact]
    public void Apply_ShortHistory_ReturnsEverythingInOrder()
    {
        var history = new List<ConversationMessage>();
        for (var turn = 1; turn <= 6; turn++)
        {
            history.Add(Message(ConversationMessage.UserRole, $"user {turn}"));
            history.Add(Message(ConversationMessage.AssistantRole, $"assistant {turn}"));
        }

        var window = ConversationContextWindow.Apply(history);

        window.Select(m => m.Id).ShouldBe(history.Select(m => m.Id));
    }

    [Fact]
    public void Apply_UserHeavyHistory_KeepsTheAssistantFewAndTheUserTail()
    {
        var history = new List<ConversationMessage>();
        for (var turn = 1; turn <= 10; turn++)
        {
            history.Add(Message(ConversationMessage.UserRole, $"user {turn}"));
        }

        history.Add(Message(ConversationMessage.AssistantRole, "assistant early"));
        for (var turn = 11; turn <= 20; turn++)
        {
            history.Add(Message(ConversationMessage.UserRole, $"user {turn}"));
        }

        history.Add(Message(ConversationMessage.AssistantRole, "assistant late"));
        for (var turn = 21; turn <= 25; turn++)
        {
            history.Add(Message(ConversationMessage.UserRole, $"user {turn}"));
        }

        var window = ConversationContextWindow.Apply(history);

        window.Select(m => m.Content).ShouldBe(
        [
            "assistant early",
            .. Enumerable.Range(16, 5).Select(turn => $"user {turn}"),
            "assistant late",
            .. Enumerable.Range(21, 5).Select(turn => $"user {turn}"),
        ]);
    }

    [Fact]
    public void Apply_NonDialogueRolesNeverConsumeWindowBudget()
    {
        var history = new List<ConversationMessage>();
        for (var turn = 1; turn <= 30; turn++)
        {
            history.Add(Message(ConversationMessage.UserRole, $"user {turn}"));
            history.Add(Message(ConversationMessage.ToolRole, $"tool {turn}"));
            history.Add(Message(ConversationMessage.AssistantRole, $"assistant {turn}"));
            history.Add(Message(ConversationMessage.SystemRole, $"system {turn}"));
        }

        var window = ConversationContextWindow.Apply(history);

        var expected = new List<string>();
        for (var turn = 21; turn <= 30; turn++)
        {
            expected.Add($"user {turn}");
            expected.Add($"assistant {turn}");
        }

        window.Count.ShouldBe(ConversationContextWindow.MaxMessages);
        window.Select(m => m.Content).ShouldBe(expected);
    }

    [Fact]
    public void Apply_EmptyOrDialogueFreeHistory_ReturnsEmpty()
    {
        ConversationContextWindow.Apply([]).ShouldBeEmpty();
        ConversationContextWindow.Apply(
        [
            Message(ConversationMessage.ToolRole, "tool 1"),
            Message(ConversationMessage.SystemRole, "system 1"),
        ]).ShouldBeEmpty();
    }

    private static ConversationMessage Message(string role, string text) =>
        ConversationMessage.Create(Guid.NewGuid(), role, text);
}
