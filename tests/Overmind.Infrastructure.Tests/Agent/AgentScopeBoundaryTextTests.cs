using Microsoft.Extensions.AI;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Agent.Tools;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentScopeBoundaryTextTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    private const int ModelVisibleTextFloor = 81;
    private const int ModelVisibleDescriptionsFloor = 72;

    private static readonly string[] ScopeTemplates =
    [
        AgentToolRegistry.ProjectScopeMismatchMessage,
        AgentToolRegistry.ProjectScopeUnverifiableMessage,
        AgentToolRegistry.ProjectProvenanceMismatchMessage,
        AgentToolRegistry.ProjectProvenanceMissingMessage,
        AgentToolRegistry.HumanGatedToolMessage,
        AgentToolRegistry.DelegationInstructionMissingMessage,
        AgentToolRegistry.DelegatedDispatchMissingMessage,
        AgentToolRegistry.ConversationScopeMissingMessage,
        NotificationValidatorToolInvoker.RefusalTemplate,
    ];

    private static readonly string[] GeneralEnforcementClaims =
    [
        "every call", "all calls", "any call", "on any", "no bypass", "cannot reach",
        "impossible", "guarantee", "without exception", "always enforced", "never reach another project",
        "each call is validated", "every request is checked", "every request is validated", "each call is checked",
        "always checks", "is always checked", "is always verified", "no way to get around it",
        "you can never touch another project", "fully guarded", "foolproof", "airtight", "cannot be circumvented",
    ];

    private static readonly string[] IdShoppingAdvice =
    [
        "Always pass projectId explicitly",
        "resolve it from get_board_context",
        "never guess project ids",
        "resolve project ids from its result",
        "resolve them from the tool result",
        "never guess ids",
        "never guess project ids",
    ];

    private static readonly string[] ClaimsNeedingAComparison =
    [
        "comes back as a refusal", "comes back refused", "is compared with", "refuses a foreign one",
        "refused instead of", "scope is checked", "and refuses a foreign",
    ];

    // The sweep above only forbids over-claiming. These assert the new truth is actually present, because a
    // prompt that quietly drops back to "object-addressed calls are a gap" would pass every ShouldNotContain
    // in this file while lying about what T1-T3 shipped.
    [Fact]
    public async Task ScopedPrompt_NamesTheClosedDoorsAndTheOneHoleLeftOpen()
    {
        var system = await ComposeAsync(Conversation.Create(Guid.NewGuid(), "scoped chat"));

        foreach (var handle in new[] { "artifactId", "issueId", "entryId" })
        {
            system.ShouldContain(handle, Case.Sensitive,
                $"the prompt must still name {handle} - and say the host checks it against the owner");
        }

        system.ShouldContain("kit_reimport", Case.Sensitive,
            "the remaining gap has to stay named by the doors that constitute it, not described in the abstract");
        system.ShouldContain("human UI", Case.Sensitive,
            "spec R8's sentence names two things that stay unscoped - dropping the second half would be an " +
            "underclaim in the other direction, and the model reads this text as the contract");
        system.ShouldContain("declared gap", Case.Insensitive);
        system.ShouldNotContain("sees arguments only", Case.Insensitive,
            "object-addressed calls are no longer outside the check - this was the pre-246 sentence");
        system.ShouldNotContain("sit outside the scope check in every shape", Case.Insensitive,
            customMessage: "that claim is only true for a conversation without a project, and it lives in that branch now");
    }

    [Fact]
    public async Task GlobalPrompt_KeepsThePassThroughHonestWithoutPromisingAComparison()
    {
        var system = await ComposeAsync(Conversation.Create(null, "global chat"));

        system.ShouldContain("246", Case.Sensitive);
        system.ShouldContain("pass-through", Case.Insensitive,
            "a project-less turn must be told its calls are unchecked because there is no owner, not because they are safe");
        system.ShouldContain("absence of a comparison", Case.Insensitive,
            customMessage: "the sentence must name the mechanism honestly instead of letting the model read a licence");
    }

    [Fact]
    public void BoardContextDescription_NoLongerFilesObjectDoorsAsOutsideTheCheck()
    {
        var description = CreateRegistry().Specs.Single(spec => spec.Name == "get_board_context").Description;

        description.ShouldContain("246", Case.Sensitive);
        description.ShouldContain("kit", Case.Insensitive,
            "the tool description must point at the path-addressed kit operations as what remains open");
        description.ShouldNotContain("sits outside that comparison", Case.Insensitive);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("validator")]
    public async Task ComposedPrompt_PromisesARefusalOnlyWhereAComparisonCanHappen(string composer)
    {
        var scoped = await ComposeToolCallingPrompt(composer, Conversation.Create(Guid.NewGuid(), "scoped"));
        var global = await ComposeToolCallingPrompt(composer, Conversation.Create(null, "global"));

        scoped.ShouldContain("comes back", Case.Insensitive, $"{composer} scoped prompt lost its refusal sentence");

        foreach (var promise in ClaimsNeedingAComparison)
        {
            global.ShouldNotContain(promise, Case.Insensitive,
                $"{composer} promises a scope refusal on a conversation with no project - on that turn nothing is " +
                "compared and nothing is refused on scope grounds (spec R3 pass-through)");
        }

        global.ShouldContain("no project", Case.Insensitive, composer);
        global.ShouldContain("global", Case.Insensitive, composer);
        global.ShouldContain("246", Case.Sensitive, composer);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("validator")]
    [InlineData("router")]
    public async Task EveryComposedPrompt_TellsTheTruthAboutWhatTheServerChecks(string composer)
    {
        var scoped = composer == "router"
            ? await ComposeRouterAsync(Conversation.Create(Guid.NewGuid(), "scoped"))
            : await ComposeAsync(composer, Conversation.Create(Guid.NewGuid(), "scoped"));
        var global = composer == "router"
            ? await ComposeRouterAsync(Conversation.Create(null, "global"))
            : await ComposeAsync(composer, Conversation.Create(null, "global"));

        foreach (var text in new[] { scoped, global })
        {
            text.ShouldNotBeNullOrWhiteSpace();
            foreach (var claim in GeneralEnforcementClaims)
            {
                text.ShouldNotContain(claim, Case.Insensitive, $"{composer} claims enforcement the server does not provide");
            }

            foreach (var advice in IdShoppingAdvice)
            {
                text.ShouldNotContain(advice, Case.Insensitive, $"{composer} still sends the model shopping for project ids");
            }
        }

        if (composer != "router")
        {
            scoped.ShouldContain("246", Case.Sensitive,
                customMessage: $"{composer} calls object-addressed tools, so its prompt must name the owner of that gap");
        }
    }

    [Theory]
    [InlineData("scoped")]
    [InlineData("global")]
    public async Task ComposedPrompt_DropsIdShoppingForScope(string shape)
    {
        var conversation = shape == "scoped"
            ? Conversation.Create(Guid.NewGuid(), "scoped chat")
            : Conversation.Create(null, "global chat");

        var system = await ComposeAsync(conversation);

        foreach (var advice in IdShoppingAdvice)
        {
            system.ShouldNotContain(advice, Case.Insensitive, $"the {shape} prompt still tells the model to shop for project ids");
        }
    }

    [Theory]
    [InlineData("scoped")]
    [InlineData("global")]
    public async Task ComposedPrompt_ClaimsOnlyWhatTheServerEnforces_AndNamesTheGapOwner(string shape)
    {
        var conversation = shape == "scoped"
            ? Conversation.Create(Guid.NewGuid(), "scoped chat")
            : Conversation.Create(null, "global chat");

        var system = await ComposeAsync(conversation);

        foreach (var claim in GeneralEnforcementClaims)
        {
            system.ShouldNotContain(claim, Case.Insensitive, $"the {shape} prompt claims enforcement the server does not provide");
        }

        system.ShouldContain("argument", customMessage: "the prompt must say scope is enforced for argument-addressed calls");
        system.ShouldContain("246", customMessage: "the prompt must name feature 246 as the owner of the object-addressed doors");
    }

    [Fact]
    public async Task ComposedPrompt_KeepsTheNoBypassClaimScopedToTheMechanismsThatHaveIt()
    {
        var system = await ComposeAsync(Conversation.Create(Guid.NewGuid(), "scoped chat"));

        system.ShouldContain("server-side", Case.Insensitive,
            customMessage: "the block that describes the enforced mechanisms has to stay in the prompt, only its claim has to shrink");
        var bypassSentence = system.Split('\n')
            .First(line => line.Contains("server-side", StringComparison.OrdinalIgnoreCase));

        bypassSentence.ShouldContain("Claim locks", Case.Sensitive);
        bypassSentence.ShouldNotContain("no bypass", Case.Insensitive);
        bypassSentence.ShouldNotContain("every call", Case.Insensitive);
        bypassSentence.ShouldNotContain("scope", Case.Insensitive,
            "the locks/gates sentence must not be the place where scope is described");
    }

    [Fact]
    public void BoardContextToolDescription_StatesBothProjectShapes()
    {
        var description = CreateRegistry().Specs.Single(spec => spec.Name == "get_board_context").Description;

        description.ShouldContain("belongs to a project", Case.Sensitive);
        description.ShouldContain("no project", Case.Sensitive);
        description.ShouldContain("global", Case.Sensitive);
        description.ShouldContain("246", Case.Sensitive);
    }

    [Fact]
    public void BoardContextToolDescription_DropsIdShoppingAndGeneralClaims()
    {
        var registry = CreateRegistry();
        var description = registry.Specs.Single(spec => spec.Name == "get_board_context").Description;

        foreach (var advice in IdShoppingAdvice)
        {
            description.ShouldNotContain(advice, Case.Insensitive);
        }

        foreach (var claim in GeneralEnforcementClaims)
        {
            description.ShouldNotContain(claim, Case.Insensitive);
        }
    }

    [Theory]
    [InlineData(nameof(AgentToolRegistry.ProjectScopeMismatchMessage), AgentToolRegistry.ProjectScopeMismatchMessage)]
    [InlineData(nameof(AgentToolRegistry.ProjectScopeUnverifiableMessage), AgentToolRegistry.ProjectScopeUnverifiableMessage)]
    [InlineData(nameof(AgentToolRegistry.ProjectProvenanceMismatchMessage), AgentToolRegistry.ProjectProvenanceMismatchMessage)]
    [InlineData(nameof(AgentToolRegistry.ProjectProvenanceMissingMessage), AgentToolRegistry.ProjectProvenanceMissingMessage)]
    public void ScopeRefusalTemplate_MakesNoGeneralEnforcementClaim(string name, string template)
    {
        var phrased = string.Format(template, "some_tool", Guid.NewGuid(), Guid.NewGuid());

        foreach (var claim in GeneralEnforcementClaims)
        {
            phrased.ShouldNotContain(claim, Case.Insensitive, name);
        }
    }

    [Fact]
    public void RegistryRefusalTemplates_AllCarryNoEnforcementClaim()
    {
        ScopeTemplates.Length.ShouldBe(9,
            customMessage: "five registry refusals, four scope/provenance ones - the sweep must stay self-sufficient");
        foreach (var template in ScopeTemplates)
        {
            foreach (var claim in GeneralEnforcementClaims)
            {
                template.ShouldNotContain(claim, Case.Insensitive, template);
            }
        }
    }

    [Fact]
    public void ModelVisibleText_NeverClaimsGeneralEnforcementAnywhere()
    {
        var registry = CreateRegistry();
        var visible = registry.Specs.Select(spec => spec.Description).Concat(ScopeTemplates).ToList();

        visible.Count.ShouldBeGreaterThanOrEqualTo(ModelVisibleTextFloor);
        visible
            .SelectMany(text => GeneralEnforcementClaims
                .Where(claim => text.Contains(claim, StringComparison.OrdinalIgnoreCase))
                .Select(claim => $"{claim} in: {text[..Math.Min(text.Length, 120)]}"))
            .ShouldBeEmpty("a model-visible string claims enforcement the server does not provide (spec R7)");
    }

    [Fact]
    public void ModelVisibleText_NeverSendsTheModelToCollectProjectIds()
    {
        var registry = CreateRegistry();
        var visible = registry.Specs.Select(spec => spec.Description).ToList();

        visible.Count.ShouldBeGreaterThanOrEqualTo(ModelVisibleDescriptionsFloor);
        visible
            .SelectMany(text => IdShoppingAdvice
                .Where(advice => text.Contains(advice, StringComparison.OrdinalIgnoreCase))
                .Select(advice => $"{advice} in: {text[..Math.Min(text.Length, 120)]}"))
            .ShouldBeEmpty("a tool description still tells the model to shop for project ids (spec R4)");
    }

    private static async Task<string> ComposeToolCallingPrompt(string composer, Conversation conversation) =>
        await ComposeAsync(composer, conversation);

    private static async Task<string> ComposeAsync(Conversation conversation) =>
        await ComposeAsync("main", conversation);

    private static async Task<string> ComposeAsync(string composer, Conversation conversation)
    {
        IAgentPromptComposer instance = composer switch
        {
            "validator" => new NotificationValidatorPromptComposer(),
            "main" => CreateComposer(),
            _ => throw new ArgumentOutOfRangeException(nameof(composer), composer, "unknown composer under test"),
        };

        var messages = await instance.ComposeAsync(conversation, [], "status?", CancellationToken.None);
        return messages[0].Text;
    }

    private static async Task<string> ComposeRouterAsync(Conversation conversation)
    {
        var messages = await new RouterPromptComposer().ComposeClassificationAsync(
            conversation, [], [], "hello", CancellationToken.None);
        return messages[0].Text;
    }

    private static AgentPromptComposer CreateComposer()
    {
        var settingsProvider = Substitute.For<IAgentRunSettingsProvider>();
        settingsProvider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AgentRunSettings("overmind-agent", 8_192, 0));
        return new AgentPromptComposer(settingsProvider);
    }

    private AgentToolRegistry CreateRegistry()
    {
        var http = new HttpClient(new NoOpHandler()) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var state = new EmbeddedAgentSessionState();
        state.Set(Guid.NewGuid());
        return new AgentToolRegistry(
            new AgentLoopbackClient(http), state, Substitute.For<IGrimoireMemoryClient>(),
            new ConversationMessageStore(_temp.CreateContext()),
            new AgentRouteRecorder(new AgentRouteMemoryStore()));
    }

    public void Dispose() => _temp.Dispose();

    private sealed class NoOpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });
    }
}
