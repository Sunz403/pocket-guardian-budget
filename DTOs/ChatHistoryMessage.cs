namespace AIShoppingAssistant.DTOs;

public sealed class ChatHistoryMessage
{
    // Expected values are "user", "assistant", or "system".
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}
