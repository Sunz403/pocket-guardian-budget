using AIShoppingAssistant.Data;
using AIShoppingAssistant.Models;
using AIShoppingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[Authorize]
public class AdminProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminProductsController> _logger;
    private readonly FileUploadService _fileUploadService;

    public AdminProductsController(ApplicationDbContext context, ILogger<AdminProductsController> logger, FileUploadService fileUploadService)
    {
        _context = context;
        _logger = logger;
        _fileUploadService = fileUploadService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await _context.Products.AsNoTracking().OrderByDescending(product => product.CreatedAt).ThenBy(product => product.Name).ToListAsync(cancellationToken));

    public async Task<IActionResult> Details(int? id, CancellationToken cancellationToken)
    {
        if (id is null) return NotFound();
        var product = await _context.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    public IActionResult Create() => View(new Product());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description,Price,Color,Size,ShippingCost,StoreName,Category")] Product product, IFormFile? imageFile, CancellationToken cancellationToken)
    {
        if (imageFile is not null && !_fileUploadService.ValidateImage(imageFile))
            ModelState.AddModelError("ImageFile", "Upload a JPG, JPEG, PNG, GIF, or WEBP image no larger than 5 MB.");
        if (!ModelState.IsValid) return View(product);

        if (imageFile is not null) product.ImageFileName = await _fileUploadService.UploadImageAsync(imageFile, cancellationToken);
        product.CreatedAt = DateTime.UtcNow;
        try
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _fileUploadService.DeleteImageAsync(product.ImageFileName);
            throw;
        }
        TempData["SuccessMessage"] = $"{product.Name} was added to the catalogue.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null) return NotFound();
        var product = await _context.Products.FindAsync([id.Value], cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Price,Color,Size,ShippingCost,StoreName,Category")] Product submittedProduct, IFormFile? imageFile, CancellationToken cancellationToken)
    {
        if (id != submittedProduct.Id) return NotFound();
        var product = await _context.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null) return NotFound();
        if (imageFile is not null && !_fileUploadService.ValidateImage(imageFile))
            ModelState.AddModelError("ImageFile", "Upload a JPG, JPEG, PNG, GIF, or WEBP image no larger than 5 MB.");
        if (!ModelState.IsValid)
        {
            submittedProduct.ImageFileName = product.ImageFileName;
            return View(submittedProduct);
        }

        product.Name = submittedProduct.Name;
        product.Description = submittedProduct.Description;
        product.Price = submittedProduct.Price;
        product.Color = submittedProduct.Color;
        product.Size = submittedProduct.Size;
        product.ShippingCost = submittedProduct.ShippingCost;
        product.StoreName = submittedProduct.StoreName;
        product.Category = submittedProduct.Category;
        var previousImageFileName = product.ImageFileName;
        if (imageFile is not null) product.ImageFileName = await _fileUploadService.UploadImageAsync(imageFile, cancellationToken);

        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException ex)
        {
            if (imageFile is not null) await _fileUploadService.DeleteImageAsync(product.ImageFileName);
            _logger.LogWarning(ex, "Product {ProductId} was changed or removed while being edited.", id);
            if (!await _context.Products.AnyAsync(item => item.Id == id, cancellationToken)) return NotFound();
            throw;
        }
        catch
        {
            if (imageFile is not null) await _fileUploadService.DeleteImageAsync(product.ImageFileName);
            throw;
        }

        if (imageFile is not null) await _fileUploadService.DeleteImageAsync(previousImageFileName);

        TempData["SuccessMessage"] = $"{product.Name} was updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id, CancellationToken cancellationToken)
    {
        if (id is null) return NotFound();
        var product = await _context.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var product = await _context.Products.FindAsync([id], cancellationToken);
        if (product is null) return NotFound();
        _context.Products.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);
        await _fileUploadService.DeleteImageAsync(product.ImageFileName);
        TempData["SuccessMessage"] = $"{product.Name} was deleted.";
        return RedirectToAction(nameof(Index));
    }
}
