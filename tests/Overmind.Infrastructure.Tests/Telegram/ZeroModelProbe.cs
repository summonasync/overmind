using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Telegram;
using Shouldly;

namespace Overmind.Infrastructure.Tests.Telegram;

internal sealed class ZeroModelProbe(IReadOnlyList<ConversationStreamEvent>? turn = null)
    : IConversationStreamSource, INotificationValidatorRunner
{
    public IReadOnlyList<ConversationStreamEvent>? Turn { get; set; } = turn;

    public int StreamInvocations { get; private set; }

    public int ValidatorInvocations { get; private set; }

    public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        [EnumeratorCancellation] CancellationToken ct = default,
        RoutingMissContext? missContext = null)
    {
        StreamInvocations++;
        if (Turn is null)
        {
            throw new InvalidOperationException("the wave command path must never start an agent turn");
        }

        foreach (var streamEvent in Turn)
        {
            await Task.Yield();
            yield return streamEvent;
        }
    }

    public Task RunAsync(
        long chatId, AgentNotification notification, string ownerReply, CancellationToken ct)
    {
        ValidatorInvocations++;
        return Task.CompletedTask;
    }

    public void AssertUntouched(string because)
    {
        StreamInvocations.ShouldBe(0, $"{because}: the conversation source was invoked");
        ValidatorInvocations.ShouldBe(0, $"{because}: the notification validator was invoked");
    }

    public static async Task AssertNoConversationFootprintAsync(TempDatabase temp)
    {
        await using var context = temp.CreateContext();
        (await context.Conversations.CountAsync()).ShouldBe(0);
        (await context.ConversationMessages.CountAsync()).ShouldBe(0);
        (await context.TelegramChatMappings.CountAsync()).ShouldBe(0);
    }

    public static async Task AssertFootprintGrowsWhenTheModelRunsAsync(TempDatabase temp)
    {
        await using var context = temp.CreateContext();
        (await context.Conversations.CountAsync()).ShouldBeGreaterThan(0);
        (await context.ConversationMessages.CountAsync()).ShouldBeGreaterThan(0);
        (await context.TelegramChatMappings.CountAsync()).ShouldBeGreaterThan(0);
    }
}
