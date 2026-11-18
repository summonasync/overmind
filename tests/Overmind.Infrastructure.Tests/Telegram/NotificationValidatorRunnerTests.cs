using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Agent.Tools;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Grimoire;
using Overmind.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Telegram;
using Overmind.Infrastructure.Tests.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class NotificationValidatorRunnerTests : IDisposable
{
    private const long OwnerChatId = 42;
    private const string TestToken = "123456:TESTSECRETTOKEN";

    private readonly TempDatabase _temp = new();
    private readonly FakeTelegramApi _api = new();
    private readonly TelegramBotClientCache _cache;
    private readonly ProjectRecordingHandler _loopback = new();
    private readonly EmbeddedAgentSessionState _sessionState = new();
    private readonly IAgentRouteStore _routeStore = new AgentRouteMemoryStore();
    private readonly CountingBudgetStore _budget = new();

    public NotificationValidatorRunnerTests() =>
        _cache = new TelegramBotClientCache(new StubTelegramBotClientFactory(_api));

    [Fact]
    public async Task ApprovalReply_WithDelegationOn_ValidatesWithOwnerMessageProvenance()
    {
        var project = await SeedProjectAsync(delegationOn: true);
        var notification = await SeedNotificationAsync(project.Id);
        ConfigureTransport();
        var messages = new ConversationMessageStore(_temp.CreateContext());
        var conversations = new TelegramValidatorConversationStore(_temp.CreateContext());
        var runner = CreateRunner(
            messages, conversations, delegationOn: true, ScriptedValidationClient("Spec gate validated per your reply."));
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());

        await runner.RunAsync(OwnerChatId, notification, "lgtm, approve the spec", CancellationToken.None);

        var conversation = await conversations.GetOrCreateConversationAsync(OwnerChatId, project.Id, CancellationToken.None);
        var history = await messages.ListPageAsync(conversation.Id, ct: CancellationToken.None);
        var instruction = history.Single(row => row.Role == ConversationMessage.UserRole);
        instruction.Content.ShouldContain(notification.Id.ToString());
        instruction.Content.ShouldContain("lgtm, approve the spec");
        instruction.Content.ShouldContain(project.Id.ToString());
        history.ShouldContain(row => row.Role == ConversationMessage.AssistantRole &&
            row.Content.Contains("validated", StringComparison.OrdinalIgnoreCase));

        var validate = _loopback.Requests.Single(request =>
            request.Path.Contains("validate", StringComparison.OrdinalIgnoreCase));
        Guid.Parse(validate.DelegatedBy!).ShouldBe(instruction.Id);
        _budget.Used.ShouldBeGreaterThan(0);

        SentTexts().ShouldContain(text => text.Contains("validated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReplyWithDelegationOff_ExplainsWithoutAnyValidateDispatch()
    {
        var notification = await SeedNotificationAsync(null);
        ConfigureTransport();
        var messages = new ConversationMessageStore(_temp.CreateContext());
        var conversations = new TelegramValidatorConversationStore(_temp.CreateContext());
        var runner = CreateRunner(
            messages, conversations, delegationOn: false,
            ScriptedValidationClient("Delegated approvals are off for this project; use the Overmind UI door."));
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());

        await runner.RunAsync(OwnerChatId, notification, "approve this", CancellationToken.None);

        _loopback.Requests.ShouldNotContain(request =>
            request.Path.Contains("validate", StringComparison.OrdinalIgnoreCase));
        var conversation = await conversations.GetOrCreateConversationAsync(OwnerChatId, null, CancellationToken.None);
        var history = await messages.ListPageAsync(conversation.Id, ct: CancellationToken.None);
        history.ShouldContain(row => row.Role == ConversationMessage.AssistantRole);
        SentTexts().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ValidatorTurn_KeepsTheMainAgentConversationUntouched()
    {
        var project = await SeedProjectAsync(delegationOn: false);
        var notification = await SeedNotificationAsync(project.Id);
        ConfigureTransport();
        var chats = new TelegramChatStore(_temp.CreateContext());
        var mainConversation = await chats.GetOrCreateConversationAsync(OwnerChatId, CancellationToken.None);
        var messages = new ConversationMessageStore(_temp.CreateContext());
        var conversations = new TelegramValidatorConversationStore(_temp.CreateContext());
        var runner = CreateRunner(
            messages, conversations, delegationOn: false,
            ScriptedValidationClient("That gate covers feature 214."));
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());

        await runner.RunAsync(OwnerChatId, notification, "what is this about?", CancellationToken.None);

        var validatorConversation = await conversations.GetOrCreateConversationAsync(
            OwnerChatId, project.Id, CancellationToken.None);
        validatorConversation.Id.ShouldNotBe(mainConversation.Id);
        (await messages.ListPageAsync(mainConversation.Id, ct: CancellationToken.None)).ShouldBeEmpty();
        (await messages.ListPageAsync(validatorConversation.Id, ct: CancellationToken.None))
            .ShouldNotBeEmpty();
    }

    private static FakeAgentChatClient ScriptedValidationClient(string outcome) => new(
    [
        FakeAgentChatClient.ToolCallUpdate(
            "c1", "planning_validate", $$"""{"artifactId":"{{Guid.NewGuid()}}"}"""),
        FakeAgentChatClient.UsageUpdate(40)
    ],
    [
        FakeAgentChatClient.TextUpdate(outcome),
        FakeAgentChatClient.UsageUpdate(20)
    ]);

    [Fact]
    public async Task ValidatorTurnOnProjectlessNotification_MarksItsToolTrailGlobalScope()
    {
        var notification = await SeedNotificationAsync(null);
        ConfigureTransport();
        var messages = new ConversationMessageStore(_temp.CreateContext());
        var conversations = new TelegramValidatorConversationStore(_temp.CreateContext());
        var runner = CreateRunner(
            messages, conversations, delegationOn: false,
            ScriptedValidationClient("Delegated approvals are off for this project; use the Overmind UI door."));
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());

        await runner.RunAsync(OwnerChatId, notification, "approve this", CancellationToken.None);

        notification.ProjectId.ShouldBeNull(
            "the validator conversation is created from notification.ProjectId, which is Guid?");
        var conversation = await conversations.GetOrCreateConversationAsync(OwnerChatId, null, CancellationToken.None);
        var rows = await messages.ListPageAsync(conversation.Id, ct: CancellationToken.None);
        var toolRow = rows.Single(row => row.Role == ConversationMessage.ToolRole);

        using var document = JsonDocument.Parse(toolRow.Content);
        document.RootElement.GetProperty("tool").GetString().ShouldBe("planning_validate");
        document.RootElement.GetProperty("globalScope").GetBoolean().ShouldBeTrue(
            customMessage: $"the most privileged embedded turn carries no scope and the trail must say so: {toolRow.Content}");
    }

    [Fact]
    public async Task ValidatorTurnOnProjectNotification_LeavesItsToolTrailUnmarked()
    {
        var project = await SeedProjectAsync(delegationOn: false);
        var notification = await SeedNotificationAsync(project.Id);
        ConfigureTransport();
        var messages = new ConversationMessageStore(_temp.CreateContext());
        var conversations = new TelegramValidatorConversationStore(_temp.CreateContext());
        var runner = CreateRunner(
            messages, conversations, delegationOn: false,
            ScriptedValidationClient("Delegated approvals are off for this project; use the Overmind UI door."));
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());

        await runner.RunAsync(OwnerChatId, notification, "approve this", CancellationToken.None);

        var conversation = await conversations.GetOrCreateConversationAsync(
            OwnerChatId, project.Id, CancellationToken.None);
        var rows = await messages.ListPageAsync(conversation.Id, ct: CancellationToken.None);
        var toolRow = rows.Single(row => row.Role == ConversationMessage.ToolRole);

        toolRow.Content.ShouldNotContain("globalScope");
    }

    private NotificationValidatorRunner CreateRunner(
        ConversationMessageStore messages,
        TelegramValidatorConversationStore conversations,
        bool delegationOn,
        FakeAgentChatClient client)
    {
        var settingsProvider = Substitute.For<IAgentRunSettingsProvider>();
        settingsProvider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AgentRunSettings("overmind-agent", 8_192, 0));

        var context = _temp.CreateContext();
        var policy = new ApprovalPolicy(context, new AppSettingsService(_temp.CreateContext()));
        IAgentDelegationGate delegation = new DelegatedApprovalGate(_temp.CreateContext(), policy);
        if (!delegationOn)
        {
            var off = Substitute.For<IAgentDelegationGate>();
            off.IsEnabledAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);
            delegation = off;
        }

        var loopback = new AgentLoopbackClient(
            new HttpClient(_loopback) { BaseAddress = new Uri("http://127.0.0.1:1") });
        var registry = new AgentToolRegistry(
            loopback,
            _sessionState,
            Substitute.For<IGrimoireMemoryClient>(),
            messages,
            new AgentRouteRecorder(_routeStore));
        var sessionState = new EmbeddedAgentSessionState();
        var registrar = new NotificationValidatorSessionRegistrar(loopback, sessionState, settingsProvider);

        var loop = new AgentLoop(
            StubFactory(client),
            new NotificationValidatorPromptComposer(),
            new NotificationValidatorToolInvoker(registry),
            settingsProvider,
            _budget,
            Substitute.For<IAgentGuardNotifier>(),
            messages,
            registrar,
            delegation,
            new AgentRouteRecorder(_routeStore),
            NullLogger<AgentLoop>.Instance);

        var transportSettings = new AppSettingsService(_temp.CreateContext());
        return new NotificationValidatorRunner(
            conversations,
            messages,
            loop,
            new ConversationTurnGate(),
            new TelegramReplySender(_cache, new TelegramTransportOptionsProvider(transportSettings)),
            NullLogger<NotificationValidatorRunner>.Instance);
    }

    private ILlmChatClientFactory StubFactory(FakeAgentChatClient client)
    {
        var factory = Substitute.For<ILlmChatClientFactory>();
        factory.CreateAsync(Arg.Any<CancellationToken>()).Returns(client);
        return factory;
    }

    private async Task<Project> SeedProjectAsync(bool delegationOn)
    {
        var project = Project.Create("validator project", "/tmp/validator-project", "runner tests");
        if (delegationOn)
        {
            project.SetDelegateApprovals(DelegateApprovalsOverride.On);
        }

        await using var context = _temp.CreateContext();
        context.Projects.Add(project);
        await context.SaveChangesAsync(CancellationToken.None);
        return project;
    }

    private async Task<AgentNotification> SeedNotificationAsync(Guid? projectId)
    {
        var notification = AgentNotification.Create(
            NotificationKind.Approval, "Spec of 214 awaits your validation", projectId, "214-intent-router", "spec");
        await using var context = _temp.CreateContext();
        context.AgentNotifications.Add(notification);
        await context.SaveChangesAsync(CancellationToken.None);
        return notification;
    }

    private void ConfigureTransport()
    {
        _api.QueueResponse(FakeTelegramApi.SentMessageResponse());
        var settings = new AppSettingsService(_temp.CreateContext());
        settings.SetAsync(SettingKeys.TelegramEnabled, "true", CancellationToken.None).Wait();
        settings.SetAsync(SettingKeys.TelegramAllowedChatIds, "42", CancellationToken.None).Wait();
        settings.SetAsync(SettingKeys.TelegramBotToken, TestToken, CancellationToken.None).Wait();
    }

    private string[] SentTexts()
    {
        lock (_api)
        {
            return [.. _api.Requests.Select(SentText)];
        }
    }

    private static string SentText(FakeTelegramApi.Captured captured)
    {
        using var document = JsonDocument.Parse(captured.Body);
        return document.RootElement.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }

    public void Dispose() => _temp.Dispose();

    private sealed class CountingBudgetStore : IAgentBudgetStore
    {
        public long Used { get; private set; }

        public Task<long> GetUsedAsync(DateOnly day, CancellationToken ct) => Task.FromResult(Used);

        public Task<long> AddUsageAsync(DateOnly day, long tokens, CancellationToken ct)
        {
            Used += tokens;
            return Task.FromResult(Used);
        }
    }

    private sealed class ProjectRecordingHandler : HttpMessageHandler
    {
        public List<(string Method, string Path, string? DelegatedBy)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var delegated = request.Headers.TryGetValues(AgentLoopbackClient.DelegatedByHeader, out var values)
                ? values.FirstOrDefault()
                : null;
            lock (Requests)
            {
                Requests.Add((request.Method.ToString(), request.RequestUri?.PathAndQuery ?? string.Empty, delegated));
            }

            var body = (request.RequestUri?.PathAndQuery ?? string.Empty).Contains("sessions")
                ? $"{{\"id\":\"{Guid.NewGuid()}\"}}"
                : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
