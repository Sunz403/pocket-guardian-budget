using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.DTOs;

public sealed class ParsedQuery
{
    [StringLength(150)]
    public string? Keyword { get; set; }

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal? MaxPrice { get; set; }

    [StringLength(50)]
    public string? Color { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(50)]
    public string? Size { get; set; }
}
