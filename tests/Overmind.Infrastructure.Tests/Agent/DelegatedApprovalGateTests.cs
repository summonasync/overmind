using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class DelegatedApprovalGateTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly Guid _projectId;
    private readonly Guid _conversationId;
    private readonly Guid _embeddedSessionId;
    private readonly ConversationMessage _instruction;

    public DelegatedApprovalGateTests()
    {
        using var context = _temp.CreateContext();
        var project = Project.Create("gate-tests", "/tmp/gate-tests");
        project.SetDelegateApprovals(true);
        var conversation = Conversation.Create(project.Id, "delegation chat");
        var session = AgentSession.Create("overmind-agent", "overmind-host", AgentSession.EmbeddedRole);
        context.Projects.Add(project);
        context.Conversations.Add(conversation);
        context.AgentSessions.Add(session);
        context.SaveChanges();
        _projectId = project.Id;
        _conversationId = conversation.Id;
        _embeddedSessionId = session.Id;

        _instruction = Store().AppendAsync(
            _conversationId, ConversationMessage.UserRole, "approve spec of 007", CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    [Fact]
    public async Task IsEnabledAsync_FreshProject_IsOff()
    {
        using var context = _temp.CreateContext();
        var fresh = Project.Create("gate-fresh", "/tmp/gate-fresh");
        context.Projects.Add(fresh);
        await context.SaveChangesAsync();
        var gate = new DelegatedApprovalGate(_temp.CreateContext());

        (await gate.IsEnabledAsync(fresh.Id)).ShouldBeFalse();
        (await gate.IsEnabledAsync(_projectId)).ShouldBeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_NullProject_IsOff()
    {
        var gate = new DelegatedApprovalGate(_temp.CreateContext());

        (await gate.IsEnabledAsync(null)).ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_FlagOnWithEmbeddedSessionAndInstruction_Grants()
    {
        var gate = new DelegatedApprovalGate(_temp.CreateContext());

        var grant = await gate.AuthorizeAsync(_projectId, _embeddedSessionId, _embeddedSessionId, _instruction.Id);

        grant.ShouldNotBeNull();
        grant.InstructionMessageId.ShouldBe(_instruction.Id);
        grant.InstructionExcerpt.ShouldBe("approve spec of 007");
        DelegatedApprovalGate.ProvenanceLine(grant)
            .ShouldBe($"{DelegatedApprovalGate.ProvenanceMarker} owner message {_instruction.Id} \"approve spec of 007\"");
    }

    [Fact]
    public async Task AuthorizeAsync_FlagOff_IsRefused()
    {
        using var context = _temp.CreateContext();
        var project = await context.Projects.SingleAsync(p => p.Id == _projectId);
        project.SetDelegateApprovals(false);
        await context.SaveChangesAsync();
        var gate = new DelegatedApprovalGate(_temp.CreateContext());

        var grant = await gate.AuthorizeAsync(_projectId, _embeddedSessionId, _embeddedSessionId, _instruction.Id);

        grant.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizeAsync_ForeignCallerSession_IsRefused()
    {
        var gate = new DelegatedApprovalGate(_temp.CreateContext());
        var externalSession = Guid.NewGuid();

        var grant = await gate.AuthorizeAsync(_projectId, externalSession, _embeddedSessionId, _instruction.Id);

        grant.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizeAsync_RegisteredButNotEmbeddedSession_IsRefused()
    {
        using var context = _temp.CreateContext();
        var worker = AgentSession.Create("outside-agent", "claude");
        context.AgentSessions.Add(worker);
        await context.SaveChangesAsync();
        var gate = new DelegatedApprovalGate(_temp.CreateContext());

        var grant = await gate.AuthorizeAsync(_projectId, worker.Id, worker.Id, _instruction.Id);

        grant.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizeAsync_AssistantMessage_IsRefused()
    {
        var gate = new DelegatedApprovalGate(_temp.CreateContext());
        var reply = await Store().AppendAsync(
            _conversationId, ConversationMessage.AssistantRole, "on it", CancellationToken.None);

        var grant = await gate.AuthorizeAsync(_projectId, _embeddedSessionId, _embeddedSessionId, reply.Id);

        grant.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizeAsync_MessageFromAnotherProject_IsRefused()
    {
        using var context = _temp.CreateContext();
        var otherProject = Project.Create("gate-other", "/tmp/gate-other");
        otherProject.SetDelegateApprovals(true);
        var otherConversation = Conversation.Create(otherProject.Id, "other chat");
        context.Projects.Add(otherProject);
        context.Conversations.Add(otherConversation);
        await context.SaveChangesAsync();
        var foreignMessage = await Store().AppendAsync(
            otherConversation.Id, ConversationMessage.UserRole, "approve spec of 007 here", CancellationToken.None);
        var gate = new DelegatedApprovalGate(_temp.CreateContext());

        var grant = await gate.AuthorizeAsync(_projectId, _embeddedSessionId, _embeddedSessionId, foreignMessage.Id);

        grant.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizeAsync_LongInstruction_IsExcerptedToOneLine()
    {
        var gate = new DelegatedApprovalGate(_temp.CreateContext());
        var longText = new string('x', 120) + "\nsecond line";
        var message = await Store().AppendAsync(
            _conversationId, ConversationMessage.UserRole, longText, CancellationToken.None);

        var grant = await gate.AuthorizeAsync(_projectId, _embeddedSessionId, _embeddedSessionId, message.Id);

        grant!.InstructionExcerpt.Length.ShouldBe(80);
        grant.InstructionExcerpt.ShouldNotContain("\n");
    }

    private ConversationMessageStore Store() => new(_temp.CreateContext());

    public void Dispose() => _temp.Dispose();
}
