using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ConversationStoreTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly Guid _projectId;
    private readonly Guid _otherProjectId;

    public ConversationStoreTests()
    {
        _context = _db.CreateContext();
        var project = Project.Create($"chat-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        var other = Project.Create($"chat-other-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.AddRange(project, other);
        _context.SaveChanges();
        _projectId = project.Id;
        _otherProjectId = other.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task CreateAndAppend_ReadHistory_IsProjectScoped()
    {
        var store = new ConversationStore(_context);
        var messages = new ConversationMessageStore(_context);

        var scoped = await store.CreateAsync(_projectId, "spec chat", CancellationToken.None);
        var loose = await store.CreateAsync(null, null, CancellationToken.None);
        await messages.AppendAsync(scoped.Id, ConversationMessage.UserRole, "first question", CancellationToken.None);
        await messages.AppendAsync(scoped.Id, ConversationMessage.AssistantRole, "first answer", CancellationToken.None);
        await messages.AppendAsync(loose.Id, ConversationMessage.UserRole, "loose question", CancellationToken.None);

        var history = await messages.ListPageAsync(scoped.Id, ct: CancellationToken.None);
        history.Count.ShouldBe(2);
        history[0].Role.ShouldBe(ConversationMessage.UserRole);
        history[0].Content.ShouldBe("first question");
        history[1].Role.ShouldBe(ConversationMessage.AssistantRole);

        var scopedList = await store.ListAsync(_projectId, CancellationToken.None);
        scopedList.Single().Id.ShouldBe(scoped.Id);
        scopedList.Single().Title.ShouldBe("spec chat");

        var fullList = await store.ListAsync(null, CancellationToken.None);
        fullList.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Persistence_SurvivesContextReopen()
    {
        Guid conversationId;
        using (var first = _db.CreateContext())
        {
            var store = new ConversationStore(first);
            var messages = new ConversationMessageStore(first);
            var created = await store.CreateAsync(_projectId, "restart chat", CancellationToken.None);
            await messages.AppendAsync(created.Id, ConversationMessage.UserRole, "hello", CancellationToken.None);
            conversationId = created.Id;
        }

        using (var second = _db.CreateContext())
        {
            var store = new ConversationStore(second);
            var messages = new ConversationMessageStore(second);

            var reopened = await store.GetAsync(conversationId, CancellationToken.None);
            reopened.ShouldNotBeNull();
            reopened.Title.ShouldBe("restart chat");
            reopened.ProjectId.ShouldBe(_projectId);

            var history = await messages.ListPageAsync(conversationId, ct: CancellationToken.None);
            history.Single().Content.ShouldBe("hello");
        }
    }

    [Fact]
    public async Task Append_UnknownConversation_Throws()
    {
        var messages = new ConversationMessageStore(_context);

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            messages.AppendAsync(Guid.NewGuid(), ConversationMessage.UserRole, "hi", CancellationToken.None));

        ex.Code.ShouldBe(PlanningFailureCode.MissingConversation);
    }

    [Fact]
    public async Task Create_UnknownProject_Throws()
    {
        var store = new ConversationStore(_context);

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            store.CreateAsync(Guid.NewGuid(), null, CancellationToken.None));

        ex.Code.ShouldBe(PlanningFailureCode.MissingProject);
    }

    [Fact]
    public async Task Delete_RemovesConversationAndMessages()
    {
        var store = new ConversationStore(_context);
        var messages = new ConversationMessageStore(_context);
        var conversation = await store.CreateAsync(_projectId, "doomed", CancellationToken.None);
        await messages.AppendAsync(conversation.Id, ConversationMessage.UserRole, "hi", CancellationToken.None);

        var deleted = await store.DeleteAsync(conversation.Id, CancellationToken.None);

        deleted.ShouldBeTrue();
        _context.ChangeTracker.Clear();
        (await store.GetAsync(conversation.Id, CancellationToken.None)).ShouldBeNull();
        (await messages.ListPageAsync(conversation.Id, ct: CancellationToken.None)).ShouldBeEmpty();
        (await store.DeleteAsync(conversation.Id, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task ListPage_BeforeCursor_WalksBackward()
    {
        var store = new ConversationStore(_context);
        var messages = new ConversationMessageStore(_context);
        var conversation = await store.CreateAsync(null, "paged", CancellationToken.None);
        var first = await messages.AppendAsync(conversation.Id, ConversationMessage.UserRole, "one", CancellationToken.None);
        var second = await messages.AppendAsync(conversation.Id, ConversationMessage.AssistantRole, "two", CancellationToken.None);
        var third = await messages.AppendAsync(conversation.Id, ConversationMessage.UserRole, "three", CancellationToken.None);

        var page = await messages.ListPageAsync(conversation.Id, limit: 2, before: third.CreatedAt, ct: CancellationToken.None);
        page.Select(m => m.Id).ShouldBe([first.Id, second.Id]);

        var newest = await messages.ListPageAsync(conversation.Id, limit: 1, ct: CancellationToken.None);
        newest.Single().Id.ShouldBe(third.Id);
    }

    [Fact]
    public async Task Append_TooLong_Throws()
    {
        var store = new ConversationStore(_context);
        var messages = new ConversationMessageStore(_context);
        var conversation = await store.CreateAsync(null, null, CancellationToken.None);
        var overflow = new string('x', ConversationMessage.MaxTextLength + 1);

        var ex = await Should.ThrowAsync<PlanningException>(() =>
            messages.AppendAsync(conversation.Id, ConversationMessage.UserRole, overflow, CancellationToken.None));

        ex.Code.ShouldBe(PlanningFailureCode.ConversationTextTooLong);
    }
}
