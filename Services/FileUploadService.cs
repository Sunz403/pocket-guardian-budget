using Microsoft.AspNetCore.Http;

namespace AIShoppingAssistant.Services;

public sealed class FileUploadService
{
    public const long MaxImageSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };
    private readonly IWebHostEnvironment _environment;

    public FileUploadService(IWebHostEnvironment environment) => _environment = environment;

    public bool ValidateImage(IFormFile? file) => file is not null
        && file.Length > 0
        && file.Length <= MaxImageSizeBytes
        && AllowedExtensions.Contains(Path.GetExtension(file.FileName));

    public async Task<string> UploadImageAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (!ValidateImage(file))
            throw new InvalidOperationException("Upload a JPG, JPEG, PNG, GIF, or WEBP image no larger than 5 MB.");

        var directory = GetProductsDirectory();
        Directory.CreateDirectory(directory);
        var fileName = GenerateUniqueFileName(file.FileName);
        await using var stream = new FileStream(Path.Combine(directory, fileName), FileMode.CreateNew, FileAccess.Write);
        await file.CopyToAsync(stream, cancellationToken);
        return fileName;
    }

    public Task DeleteImageAsync(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return Task.CompletedTask;
        var safeFileName = Path.GetFileName(fileName);
        var path = Path.Combine(GetProductsDirectory(), safeFileName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public string GetImageUrl(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "/images/products/product-placeholder.svg";
        var safeFileName = Path.GetFileName(fileName);
        return File.Exists(Path.Combine(GetProductsDirectory(), safeFileName))
            ? $"/images/products/{Uri.EscapeDataString(safeFileName)}"
            : "/images/products/product-placeholder.svg";
    }

    public string GenerateUniqueFileName(string originalName) =>
        $"{Guid.NewGuid():N}{Path.GetExtension(originalName).ToLowerInvariant()}";

    private string GetProductsDirectory() => Path.Combine(_environment.WebRootPath, "images", "products");
}
