using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class ConversationTurnGateTests
{
    [Fact]
    public void TryEnter_FreeConversation_AdmitsAndBlocksTheNext()
    {
        var gate = new ConversationTurnGate();
        var conversationId = Guid.NewGuid();

        gate.TryEnter(conversationId).ShouldBeTrue();
        gate.IsBusy(conversationId).ShouldBeTrue();
        gate.TryEnter(conversationId).ShouldBeFalse();
    }

    [Fact]
    public void Exit_FreesTheConversation()
    {
        var gate = new ConversationTurnGate();
        var conversationId = Guid.NewGuid();
        gate.TryEnter(conversationId).ShouldBeTrue();

        gate.Exit(conversationId);

        gate.IsBusy(conversationId).ShouldBeFalse();
        gate.TryEnter(conversationId).ShouldBeTrue();
    }

    [Fact]
    public void Enter_IsUnconditional()
    {
        var gate = new ConversationTurnGate();
        var conversationId = Guid.NewGuid();

        gate.Enter(conversationId);
        gate.Enter(conversationId);

        gate.IsBusy(conversationId).ShouldBeTrue();
        gate.Exit(conversationId);
        gate.IsBusy(conversationId).ShouldBeTrue();
        gate.Exit(conversationId);
        gate.IsBusy(conversationId).ShouldBeFalse();
    }

    [Fact]
    public void TryEnter_Refusal_DoesNotLeakTheSlot()
    {
        var gate = new ConversationTurnGate();
        var conversationId = Guid.NewGuid();
        gate.Enter(conversationId);

        gate.TryEnter(conversationId).ShouldBeFalse();
        gate.Exit(conversationId);

        gate.TryEnter(conversationId).ShouldBeTrue();
    }
}
