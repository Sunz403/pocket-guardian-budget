using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.DTOs;

public class FavoriteStoreDto
{
    [Required]
    [StringLength(100)]
    public string StoreName { get; set; } = string.Empty;
}
