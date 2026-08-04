using AIShoppingAssistant.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    [HttpGet("/Catalog/Details/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var product = await _context.Products.AsNoTracking().SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }
}
