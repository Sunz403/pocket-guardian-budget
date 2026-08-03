using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    [Required, StringLength(4000)]
    public string Message { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string Sender { get; set; } = string.Empty; // User or AI

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required, StringLength(64)]
    public string ChatSessionId { get; set; } = string.Empty;
    public ChatSession? ChatSession { get; set; }
}
