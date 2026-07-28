using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLogin { get; set; }

    public UserPreference? UserPreference { get; set; }

    public ICollection<SearchHistory> SearchHistories { get; set; } = new List<SearchHistory>();

    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}
