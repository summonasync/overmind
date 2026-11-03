using Microsoft.EntityFrameworkCore;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramChatStoreTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Fact]
    public async Task GetOrCreate_CreatesLazyConversation_TitledTelegramAndUnscoped()
    {
        using var context = _temp.CreateContext();
        var store = new TelegramChatStore(context);

        var conversation = await store.GetOrCreateConversationAsync(42, CancellationToken.None);

        conversation.Title.ShouldBe(TelegramChatStore.ConversationTitle);
        conversation.ProjectId.ShouldBeNull();
        context.Conversations.Count().ShouldBe(1);
    }

    [Fact]
    public async Task GetOrCreate_ReusesTheMappingOnTheNextCall()
    {
        using var context = _temp.CreateContext();
        var store = new TelegramChatStore(context);

        var first = await store.GetOrCreateConversationAsync(42, CancellationToken.None);
        var second = await store.GetOrCreateConversationAsync(42, CancellationToken.None);

        second.Id.ShouldBe(first.Id);
        context.Conversations.Count().ShouldBe(1);
    }

    [Fact]
    public async Task GetOrCreate_MapsEachChatToItsOwnConversation()
    {
        using var context = _temp.CreateContext();
        var store = new TelegramChatStore(context);

        var owner = await store.GetOrCreateConversationAsync(42, CancellationToken.None);
        var other = await store.GetOrCreateConversationAsync(43, CancellationToken.None);

        other.Id.ShouldNotBe(owner.Id);
        context.Conversations.Count().ShouldBe(2);
    }

    [Fact]
    public async Task GetOrCreate_RecreatesTheConversationAfterDeletion()
    {
        var store = new TelegramChatStore(_temp.CreateContext());
        var original = await store.GetOrCreateConversationAsync(42, CancellationToken.None);

        await using (var context = _temp.CreateContext())
        {
            await context.Conversations.Where(c => c.Id == original.Id).ExecuteDeleteAsync(CancellationToken.None);
        }

        using var fresh = _temp.CreateContext();
        var recreated = new TelegramChatStore(fresh).GetOrCreateConversationAsync(42, CancellationToken.None);

        (await recreated).Id.ShouldNotBe(original.Id);
        fresh.TelegramChatMappings.Count().ShouldBe(1);
    }

    public void Dispose() => _temp.Dispose();
}
