using System.ComponentModel.DataAnnotations;

namespace AIShoppingAssistant.Models;

public class Store
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Address { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    [Required, StringLength(100)]
    public string Category { get; set; } = string.Empty;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
