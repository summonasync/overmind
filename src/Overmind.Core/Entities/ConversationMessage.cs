namespace Overmind.Core;

public sealed class ConversationMessage
{
    public const int MaxRoleLength = 32;
    public const int MaxTextLength = 32_000;
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";
    public const string SystemRole = "system";

    private ConversationMessage()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public static ConversationMessage Create(Guid conversationId, string role, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var trimmedRole = role.Trim();
        if (trimmedRole.Length > MaxRoleLength)
        {
            throw new ArgumentException(
                $"Conversation message role '{trimmedRole}' exceeds the {MaxRoleLength} character cap.", nameof(role));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var trimmedContent = content.Trim();
        if (trimmedContent.Length > MaxTextLength)
        {
            throw new ArgumentException(
                $"Conversation message text is {trimmedContent.Length} characters; the cap is {MaxTextLength}.", nameof(content));
        }

        return new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = trimmedRole,
            Content = trimmedContent,
            CreatedAt = DateTime.UtcNow
        };
    }
}
