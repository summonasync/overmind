using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Overmind.Infrastructure.Agent.Tools;

public sealed class AgentToolFunction : AIFunction
{
    private readonly AgentToolSpec _spec;
    private readonly JsonElement _schema;

    public AgentToolFunction(AgentToolSpec spec)
    {
        _spec = spec;
        _schema = JsonDocument.Parse(spec.ParametersJson).RootElement.Clone();
    }

    public override string Name => _spec.Name;

    public override string Description => _spec.Description;

    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            $"Agent tool '{_spec.Name}' is dispatched by the loop, never invoked through the provider connector.");
}
