using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.Models;

public class ChatSession
{
    [Key]
    [StringLength(64)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
