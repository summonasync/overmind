using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWaveDraftStoreTests
{
    private const long ChatId = 42;
    private static readonly Guid Project = Guid.Parse("1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111");

    [Fact]
    public void Open_PublishesADraftWithAParseableToken()
    {
        var drafts = CreateStore();

        var draft = drafts.Open(ChatId);

        draft.ChatId.ShouldBe(ChatId);
        TelegramWaveCallbackData.TryParse(
            TelegramWaveCallbackData.RoomData(draft.Token, 0), out var data).ShouldBeTrue();
        data.RoomIndex.ShouldBe(0);
    }

    [Fact]
    public void Open_SecondTimeSupersedesTheFirstDraft()
    {
        var drafts = CreateStore();
        var first = drafts.Open(ChatId);
        first.MarkProject(Project, "Overmind", ["all"]);
        first.AwaitText("all", 21);

        var second = drafts.Open(ChatId);

        second.Token.ShouldNotBe(first.Token);
        drafts.Current(ChatId).ShouldBe(second);
        drafts.AwaitingText(ChatId, 21).ShouldBeNull();
    }

    [Fact]
    public void Current_DropsTheDraftOnceItsLifetimeElapsed()
    {
        var clock = new OffsetClock(TimeSpan.FromMinutes(1));
        var drafts = new TelegramWaveDraftStore(clock);
        var draft = drafts.Open(ChatId);
        draft.MarkProject(Project, "Overmind", ["all"]);
        draft.AwaitText("all", 21);

        clock.Advance(TimeSpan.FromMinutes(TelegramWaveDraft.LifetimeMinutes));

        drafts.Current(ChatId).ShouldBeNull();
        drafts.AwaitingText(ChatId, 21).ShouldBeNull();
    }

    [Fact]
    public void Current_DropsTheDraftOneMinuteBeforeTheLifetimeEnds()
    {
        var clock = new OffsetClock(TimeSpan.FromMinutes(1));
        var drafts = new TelegramWaveDraftStore(clock);
        var draft = drafts.Open(ChatId);
        clock.Advance(TimeSpan.FromMinutes(TelegramWaveDraft.LifetimeMinutes - 1));

        draft.IsLive(clock.GetUtcNow()).ShouldBeTrue();
        drafts.Current(ChatId).ShouldBe(draft);
    }

    [Fact]
    public void Open_PrunesForeignChatsThatExpired()
    {
        var clock = new OffsetClock(TimeSpan.FromMinutes(1));
        var drafts = new TelegramWaveDraftStore(clock);
        drafts.Open(77);
        clock.Advance(TimeSpan.FromMinutes(TelegramWaveDraft.LifetimeMinutes + 1));

        drafts.Open(ChatId);

        drafts.Current(77).ShouldBeNull();
    }

    [Fact]
    public void Current_TokenOverload_OnlyAnswersTheCurrentDraft()
    {
        var drafts = CreateStore();
        var draft = drafts.Open(ChatId);

        drafts.Current(ChatId, draft.Token).ShouldBe(draft);
        drafts.Current(ChatId, "aaaaaaaa").ShouldBeNull();
        drafts.Current(ChatId + 1, draft.Token).ShouldBeNull();
    }

    [Fact]
    public void AwaitingText_RequiresBothTheRoomAndThePrompt()
    {
        var drafts = CreateStore();
        var draft = drafts.Open(ChatId);
        draft.MarkProject(Project, "Overmind", ["all"]);

        drafts.AwaitingText(ChatId, 21).ShouldBeNull();

        draft.AwaitText("all", 21);
        drafts.AwaitingText(ChatId, 21).ShouldBe(draft);
        drafts.AwaitingText(ChatId, 22).ShouldBeNull();
    }

    [Fact]
    public void MarkProject_ReplacesTheRoomSnapshotAndClearsThePendingPrompt()
    {
        var draft = CreateStore().Open(ChatId);
        draft.MarkProject(Project, "Overmind", ["all", "old-lane"]);
        draft.AwaitText("old-lane", 21);

        draft.MarkProject(Project, "Overmind", ["all", "new-lane"]);

        draft.Rooms.ShouldBe(["all", "new-lane"]);
        draft.Room.ShouldBeNull();
        draft.PromptMessageId.ShouldBeNull();
    }

    [Fact]
    public void TryResolveRoom_ReadsOnlyTheSnapshotThePickerWasBuiltFrom()
    {
        var draft = CreateStore().Open(ChatId);
        draft.MarkProject(Project, "Overmind", ["all", "telegram-bridge"]);

        draft.TryResolveRoom(1, out var room).ShouldBeTrue();
        room.ShouldBe("telegram-bridge");
        draft.TryResolveRoom(2, out _).ShouldBeFalse();
        draft.TryResolveRoom(-1, out _).ShouldBeFalse();
        draft.Room.ShouldBeNull();
    }

    [Fact]
    public void Cancel_RemovesTheDraftOnce()
    {
        var drafts = CreateStore();
        drafts.Open(ChatId);

        drafts.Cancel(ChatId).ShouldBeTrue();
        drafts.Cancel(ChatId).ShouldBeFalse();
        drafts.Current(ChatId).ShouldBeNull();
    }

    private static TelegramWaveDraftStore CreateStore() => new(new OffsetClock(TimeSpan.Zero));

    private sealed class OffsetClock(TimeSpan offset) : TimeProvider
    {
        private TimeSpan _offset = offset;

        public void Advance(TimeSpan delta) => _offset += delta;

        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero).Add(_offset);
    }
}
