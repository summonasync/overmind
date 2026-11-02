using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.AI;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentPromptComposerTests
{
    private static readonly GrimoireMemoryEndpoint Endpoint = new("http://127.0.0.1:19996", "crew", "overmind-agent");

    private readonly Conversation _conversation = Conversation.Create(null, "composer tests");

    [Fact]
    public async Task ComposeAsync_GrimoireUp_IncludesTopFactsIntoTheSnapshot()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory.ResolveEndpointAsync(Arg.Any<CancellationToken>()).Returns(Endpoint);
        memory
            .ListTopFactsAsync(Endpoint, AgentPromptComposer.MaxMemoryFacts, Arg.Any<CancellationToken>())
            .Returns([
                new GrimoireMemoryRecord(
                    Guid.NewGuid(),
                    "crew ships features through PRs opened from feat/NNN-slug branches",
                    "fact", [],
                    DateTimeOffset.Parse("2026-11-02T09:00:00Z"), DateTimeOffset.Parse("2026-11-02T09:00:00Z"),
                    "billy-herrington"),
            ]);
        var composer = CreateComposer(memory);

        var messages = await composer.ComposeAsync(_conversation, [], "hello there", CancellationToken.None);

        var system = messages[0].Text;
        messages[0].Role.ShouldBe(ChatRole.System);
        system.ShouldContain("memoryFacts");
        system.ShouldContain("crew ships features through PRs");
        system.ShouldContain("2026-11-02");
    }

    [Fact]
    public async Task ComposeAsync_GrimoireDown_DegradesSilentlyAndStillComposes()
    {
        var memory = Substitute.For<IGrimoireMemoryClient>();
        memory
            .ResolveEndpointAsync(Arg.Any<CancellationToken>())
            .Returns<GrimoireMemoryEndpoint>(_ => throw new GrimoireMemoryException(
                "grimoire memory is unavailable: no running grimoire instance was detected"));
        var composer = CreateComposer(memory);

        var messages = await composer.ComposeAsync(_conversation, [], "hello there", CancellationToken.None);

        messages.Count.ShouldBe(2);
        messages[0].Text.ShouldNotContain("memoryFacts");
        messages[0].Text.ShouldContain("You are overmind-agent");
        messages.Last().Role.ShouldBe(ChatRole.User);
        messages.Last().Text.ShouldBe("hello there");
    }

    private static AgentPromptComposer CreateComposer(IGrimoireMemoryClient memory)
    {
        var http = new HttpClient(new EmptyHandler()) { BaseAddress = new Uri("http://127.0.0.1:1") };
        var settings = Substitute.For<IAgentRunSettingsProvider>();
        settings
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AgentRunSettings("overmind-agent", 8192, 0));
        return new AgentPromptComposer(
            new AgentLoopbackClient(http),
            settings,
            memory);
    }

    private sealed class EmptyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
