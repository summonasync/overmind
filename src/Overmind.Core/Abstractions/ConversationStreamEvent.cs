namespace Overmind.Core;

public sealed record ConversationStreamEvent(ConversationStreamEventKind Kind, string? Text = null)
{
    public static ConversationStreamEvent Delta(string text) =>
        new(ConversationStreamEventKind.Delta, text);

    public static ConversationStreamEvent Completed() =>
        new(ConversationStreamEventKind.Completed);

    public static ConversationStreamEvent Failed(string error) =>
        new(ConversationStreamEventKind.Failed, error);
}
