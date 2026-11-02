using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentInstructionsLiveApplyApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public AgentInstructionsLiveApplyApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Put_Instructions_NextConversationCompositionCarriesThem()
    {
        using (var put = await _client.PutAsJsonAsync(
                   "/api/settings", new { AgentInstructions = "Be terse. Skip pleasantries." }))
        {
            put.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var composer = scope.ServiceProvider.GetRequiredService<IAgentPromptComposer>();
        var conversation = Conversation.Create(null, "instructions live apply");

        var first = await composer.ComposeAsync(conversation, [], "status?", CancellationToken.None);
        first[0].Role.ShouldBe(ChatRole.System);
        first[0].Text.ShouldContain(AgentPromptComposer.InstructionsHeader);
        first[0].Text.ShouldEndWith("Be terse. Skip pleasantries.");

        using (var update = await _client.PutAsJsonAsync(
                   "/api/settings", new { AgentInstructions = "Answer in Russian." }))
        {
            update.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var second = await composer.ComposeAsync(conversation, [], "status?", CancellationToken.None);
        second[0].Text.ShouldNotContain("Be terse");
        second[0].Text.ShouldEndWith("Answer in Russian.");
    }
}
