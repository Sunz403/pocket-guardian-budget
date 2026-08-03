using AIShoppingAssistant.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIShoppingAssistant.Controllers;

[Authorize]
public class CatalogController : Controller
{
    private readonly ApplicationDbContext _context;
    public CatalogController(ApplicationDbContext context) => _context = context;

    [HttpGet("/Catalog")]
    public IActionResult Index()
    {
        ViewBag.Categories = _context.Products.Select(p => p.Category).Distinct().OrderBy(x => x).ToList();
        ViewBag.Stores = _context.Products.Select(p => p.StoreName).Distinct().OrderBy(x => x).ToList();
        return View();
    }
}
