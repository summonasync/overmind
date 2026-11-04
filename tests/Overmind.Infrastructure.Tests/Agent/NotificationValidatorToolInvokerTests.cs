using System.Text.Json;
using Microsoft.Extensions.AI;
using NSubstitute;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Tools;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class NotificationValidatorToolInvokerTests
{
    private readonly IAgentToolInvoker _full = Substitute.For<IAgentToolInvoker>();
    private readonly NotificationValidatorToolInvoker _invoker;

    public NotificationValidatorToolInvokerTests() => _invoker = new NotificationValidatorToolInvoker(_full);

    private static AgentToolFunction Tool(string name) => new(
        new AgentToolSpec(name, "Does test things", "{}", (_, _) => Task.FromResult(JsonSerializer.SerializeToElement(new { }))));

    [Fact]
    public void CreateTools_KeepsOnlyTheValidatorToolset()
    {
        _full.CreateTools(Arg.Any<bool>()).Returns(
        [
            Tool("get_board_context"),
            Tool("planning_validate"),
            Tool("planning_set_feature_status"),
            Tool("planning_task_queue"),
            Tool("wave_chat_post"),
        ]);

        var tools = _invoker.CreateTools(includeHumanGated: true);

        tools.Select(tool => tool.Name).ShouldBe(
            ["get_board_context", "planning_validate", "planning_set_feature_status"], ignoreOrder: true);
    }

    [Fact]
    public async Task InvokeAsync_OutsideToolset_IsRefusedWithoutReachingTheFullInvoker()
    {
        var result = await _invoker.InvokeAsync(
            "planning_task_queue", JsonSerializer.SerializeToElement(new { }), default, CancellationToken.None);

        result.GetProperty("error").GetString()!.ShouldContain("outside the notification validator's toolset");
        await _full.DidNotReceiveWithAnyArgs().InvokeAsync(
            Arg.Any<string>(), Arg.Any<JsonElement>(), Arg.Any<AgentToolInvocation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_AllowedTool_PassesThroughToTheFullInvoker()
    {
        _full
            .InvokeAsync(
                "planning_validate", Arg.Any<JsonElement>(), Arg.Any<AgentToolInvocation>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.SerializeToElement(new { ok = true }));

        var result = await _invoker.InvokeAsync(
            "planning_validate", JsonSerializer.SerializeToElement(new { }), default, CancellationToken.None);

        result.GetProperty("ok").GetBoolean().ShouldBeTrue();
    }
}
