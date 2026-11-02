namespace Overmind.Core;

public sealed record ConversationStreamEvent(
    ConversationStreamEventKind Kind,
    string? Text = null,
    string? ToolName = null,
    long? TokensUsed = null,
    string? Code = null)
{
    public static ConversationStreamEvent Delta(string text) =>
        new(ConversationStreamEventKind.Delta, text);

    public static ConversationStreamEvent ToolCall(string toolName) =>
        new(ConversationStreamEventKind.ToolCall, ToolName: toolName);

    public static ConversationStreamEvent Completed(long? tokensUsed = null) =>
        new(ConversationStreamEventKind.Completed, TokensUsed: tokensUsed);

    public static ConversationStreamEvent Failed(string error, string? code = null) =>
        new(ConversationStreamEventKind.Failed, error, Code: code);
}
