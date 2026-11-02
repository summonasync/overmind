using System.Text.Json;

namespace Overmind.Infrastructure.Agent.Tools;

public sealed class AgentToolPayload
{
    private readonly Dictionary<string, object?> _fields = [];

    public AgentToolPayload Set(string name, object? value)
    {
        if (value is not null)
        {
            _fields[name] = value;
        }

        return this;
    }

    public Dictionary<string, object?> Build() => _fields;
}
