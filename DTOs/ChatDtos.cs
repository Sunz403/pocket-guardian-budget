using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.DTOs;

public sealed class SendChatMessageDto
{
    [Required, StringLength(4000)]
    public string Message { get; init; } = string.Empty;
}

public sealed class ChatMessageDto
{
    public int Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Sender { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string ChatSessionId { get; init; } = string.Empty;
}

public sealed class ChatResponseDto
{
    public string SessionId { get; init; } = string.Empty;
    public ChatMessageDto UserMessage { get; init; } = new();
    public ChatMessageDto AiMessage { get; init; } = new();
    public List<ChatProductDto> Products { get; init; } = [];
}

public sealed class ChatProductDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string StoreName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string? Description { get; init; }
}
