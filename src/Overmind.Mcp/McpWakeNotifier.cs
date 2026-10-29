using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

#pragma warning disable MCP9005

public sealed class McpWakeNotifier(McpServer server) : IWakeNotifier
{
    public const string LoggerName = "overmind-wake";

    private static readonly JsonSerializerOptions DataOptions = new(JsonSerializerDefaults.Web);

    public async Task NotifyAsync(int pending, CancellationToken cancellationToken)
    {
        if (server.ClientInfo is null || server.LoggingLevel is { } floor && LoggingLevel.Notice < floor)
        {
            return;
        }

        try
        {
            await server.SendNotificationAsync(
                NotificationMethods.LoggingMessageNotification,
                new LoggingMessageNotificationParams
                {
                    Level = LoggingLevel.Notice,
                    Logger = LoggerName,
                    Data = JsonSerializer.SerializeToElement(new { pending }, DataOptions)
                },
                McpJsonUtilities.DefaultOptions,
                cancellationToken);
        }
        catch (Exception)
        {
        }
    }
}

#pragma warning restore MCP9005
