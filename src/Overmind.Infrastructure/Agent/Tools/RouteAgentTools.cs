using System.Text.Json;
using Overmind.Core;
using Overmind.Infrastructure.Agent.Routes;

namespace Overmind.Infrastructure.Agent.Tools;

public static class RouteAgentTools
{
    public const string RecordRouteToolName = "record_route";
    public const string BounceToolName = "bounce_turn";

    public static IReadOnlyList<AgentToolSpec> Build(
        AgentRouteRecorder routes, ConversationMessageStore messages)
    {
        return [RecordRouteSpec(routes, messages), BounceSpec()];
    }

    private static AgentToolSpec RecordRouteSpec(AgentRouteRecorder routes, ConversationMessageStore messages) => new(
        RecordRouteToolName,
        "Records why the current request reached this fallback worker so the routing table learns. Call it once " +
        "per fallback turn when the intent is clear: domain is the actual intent area, missReason is the short " +
        "reason routing missed it, suggestedPattern is the optional keyword list (comma separated) or phrase that " +
        "should catch similar requests next time. Omit suggestedPattern only when the raw request text itself is " +
        "the best pattern.",
        """
        {"type":"object","properties":{"domain":{"type":"string"},"missReason":{"type":"string"},"suggestedPattern":{"type":"string"}},"required":["domain","missReason"]}
        """,
        (_, _) => throw new NotSupportedException(
            $"{RecordRouteToolName} is dispatched with the current conversation scope, never the plain handler."),
        AgentToolCategory.Standard,
        null,
        async (args, conversationId, ct) =>
        {
            var domain = AgentToolArgs.RequiredString(args, "domain");
            var missReason = AgentToolArgs.RequiredString(args, "missReason");
            var suggested = AgentRouteText.Normalize(AgentToolArgs.OptionalString(args, "suggestedPattern"));
            var pattern = suggested;
            if (pattern.Length == 0)
            {
                var request = await messages.LatestAsync(conversationId, ConversationMessage.UserRole, ct);
                pattern = AgentRouteText.Normalize(request?.Content);
            }

            if (pattern.Length == 0)
            {
                throw new ArgumentException(
                    "Neither a suggested pattern nor a request text was available to record a route row.");
            }

            var row = await routes.RecordAnalysisAsync(conversationId, pattern, domain, missReason, ct);
            return JsonSerializer.SerializeToElement(new
            {
                recorded = true,
                row.Pattern,
                row.Domain,
                row.MissReason,
                row.HitCount,
                status = row.Status.ToString().ToLowerInvariant()
            }, AgentLoopbackClient.JsonOptions);
        });

    private static AgentToolSpec BounceSpec() => new(
        BounceToolName,
        "Ends the turn with a structured not-my-domain verdict when the request belongs to another specialist " +
        "and answering it here would be wrong. Give the short reason so the router can re-route the request.",
        """
        {"type":"object","properties":{"reason":{"type":"string"}},"required":[]}
        """,
        async (args, ct) => JsonSerializer.SerializeToElement(new
        {
            bounced = true,
            reason = AgentToolArgs.OptionalString(args, "reason") ?? string.Empty
        }, AgentLoopbackClient.JsonOptions));
}
