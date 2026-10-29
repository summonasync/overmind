using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

public sealed class WakeSlotFilter(WakeSignalSlot slot)
{
    public McpRequestFilter<CallToolRequestParams, CallToolResult> Create() =>
        next => async (request, cancellationToken) =>
        {
            var result = await next(request, cancellationToken);
            var pending = slot.Consume();
            return pending > 0 ? WithWakeNote(result, pending) : result;
        };

    private static CallToolResult WithWakeNote(CallToolResult result, int pending)
    {
        var content = result.Content;
        if (content is null)
        {
            content = [];
            result.Content = content;
        }

        content.Add(new TextContentBlock
        {
            Text = pending == 1
                ? "overmind wake: 1 pending signal — poll overmind_wake_peek for details"
                : $"overmind wake: {pending} pending signals — poll overmind_wake_peek for details"
        });

        return result;
    }
}
