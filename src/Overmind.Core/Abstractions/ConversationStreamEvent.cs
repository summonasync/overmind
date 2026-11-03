namespace Overmind.Core;

public sealed record ConversationStreamEvent(
    ConversationStreamEventKind Kind,
    string? Text = null,
    string? ToolName = null,
    long? TokensUsed = null,
    string? Code = null,
    Guid? MessageId = null,
    string? RouteTarget = null,
    RoutingSource? RouteSource = null)
{
    public static ConversationStreamEvent Delta(string text) =>
        new(ConversationStreamEventKind.Delta, text);

    public static ConversationStreamEvent ToolCall(string toolName) =>
        new(ConversationStreamEventKind.ToolCall, ToolName: toolName);

    public static ConversationStreamEvent Completed(long? tokensUsed = null, Guid? messageId = null) =>
        new(ConversationStreamEventKind.Completed, TokensUsed: tokensUsed, MessageId: messageId);

    public static ConversationStreamEvent Failed(string error, string? code = null) =>
        new(ConversationStreamEventKind.Failed, error, Code: code);

    public static ConversationStreamEvent Aborted() =>
        new(ConversationStreamEventKind.Aborted);

    public static ConversationStreamEvent Routed(string target, RoutingSource source) =>
        new(ConversationStreamEventKind.Routed, RouteTarget: target, RouteSource: source);

    public static ConversationStreamEvent Bounced(string reason) =>
        new(ConversationStreamEventKind.Bounced, reason);
}
