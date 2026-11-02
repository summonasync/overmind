namespace Overmind.Core;

public enum ConversationStreamEventKind
{
    Delta = 0,
    Completed = 1,
    Failed = 2,
    ToolCall = 3
}
