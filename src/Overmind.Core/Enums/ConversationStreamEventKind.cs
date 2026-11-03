namespace Overmind.Core;

public enum ConversationStreamEventKind
{
    Delta = 0,
    Completed = 1,
    Failed = 2,
    ToolCall = 3,
    Aborted = 4,
    Routed = 5,
    Bounced = 6
}
