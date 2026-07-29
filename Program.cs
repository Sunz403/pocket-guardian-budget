using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using AIShoppingAssistant.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json is loaded by default, but this makes it explicit.
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// MVC/API services
builder.Services.AddControllersWithViews(options =>
{
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cache
builder.Services.AddMemoryCache();

// LocationService configuration and HTTP client
builder.Services.Configure<OpenCageOptions>(
    builder.Configuration.GetSection(OpenCageOptions.SectionName));

builder.Services.Configure<StoreLocationsOptions>(
    builder.Configuration.GetSection(StoreLocationsOptions.SectionName));

builder.Services.AddHttpClient("OpenCage", client =>
{
    client.BaseAddress = new Uri("https://api.opencagedata.com/");
    client.Timeout = TimeSpan.FromSeconds(45);
});

// LocalAI HTTP client configuration
builder.Services.AddHttpClient("LocalAI", client =>
{
    var localAiSection = builder.Configuration.GetSection("LocalAI");

    client.BaseAddress = new Uri(
        localAiSection["BaseUrl"] ?? "http://localhost:11434");

    client.Timeout = TimeSpan.FromSeconds(
        localAiSection.GetValue<int?>("RequestTimeoutSeconds") ?? 45);
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddHealthChecks()
    .AddCheck<ModelHealthCheck>("local_ai_model");

// Application services
builder.Services.AddScoped<LocalAIService>();
builder.Services.AddScoped<MockAIService>();
builder.Services.AddScoped<AIServiceFactory>();
builder.Services.AddScoped<IAIService>(serviceProvider =>
    serviceProvider.GetRequiredService<AIServiceFactory>().Create());
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<LocationService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "AIShoppingAssistant.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
    SeedDatabase(context);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                exception = entry.Value.Exception?.Message
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.MapGet("/testai", context =>
{
    context.Response.Redirect("/testai.html");
    return Task.CompletedTask;
});

app.Run();

static void SeedDatabase(ApplicationDbContext context)
{
    if (context.Users.Any(user =>
        user.Email == "thabo.mokoena@example.co.za" ||
        user.Email == "lerato.dlamini@example.co.za" ||
        user.Email == "sipho.nkosi@example.co.za" ||
        user.Email == "nomsa.maseko@example.co.za" ||
        user.Email == "andile.khumalo@example.co.za"))
    {
        EnsureCurrentMonthBudgets(context);
        EnsureShoppingAssistantTestProducts(context);
        return;
    }

    var now = DateTime.UtcNow;
    var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    var users = new List<User>
    {
        new() { FullName = "Thabo Mokoena", Email = "thabo.mokoena@example.co.za", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), CreatedAt = now.AddDays(-18) },
        new() { FullName = "Lerato Dlamini", Email = "lerato.dlamini@example.co.za", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), CreatedAt = now.AddDays(-16) },
        new() { FullName = "Sipho Nkosi", Email = "sipho.nkosi@example.co.za", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), CreatedAt = now.AddDays(-12) },
        new() { FullName = "Nomsa Maseko", Email = "nomsa.maseko@example.co.za", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), CreatedAt = now.AddDays(-9) },
        new() { FullName = "Andile Khumalo", Email = "andile.khumalo@example.co.za", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), CreatedAt = now.AddDays(-6) }
    };

    context.Users.AddRange(users);
    context.SaveChanges();

    var products = new List<Product>
    {
        new() { Name = "Checkers Family Grocery Hamper", Description = "Pantry staples with maize meal, rice, pasta, tinned food, and tea.", Price = 549.99m, ShippingCost = 35.00m, StoreName = "Checkers", Category = "Groceries", Size = "Family Pack", CreatedAt = now },
        new() { Name = "Samsung 32-inch Smart TV", Description = "HD smart television for streaming and everyday viewing.", Price = 3999.00m, ShippingCost = 149.00m, StoreName = "Game", Category = "Electronics", Color = "Black", Size = "32 inch", CreatedAt = now },
        new() { Name = "Woolworths Cotton Chino Shirt", Description = "Classic cotton shirt for work or weekends.", Price = 499.00m, ShippingCost = 60.00m, StoreName = "Woolworths", Category = "Clothing", Color = "Navy", Size = "M", CreatedAt = now },
        new() { Name = "Pick n Pay School Shoes", Description = "Durable black lace-up school shoes.", Price = 349.99m, ShippingCost = 45.00m, StoreName = "Pick n Pay", Category = "Shoes", Color = "Black", Size = "7", CreatedAt = now },
        new() { Name = "Makro Best of South African Cooking", Description = "Recipe book featuring local family favourites.", Price = 229.00m, ShippingCost = 55.00m, StoreName = "Makro", Category = "Books", CreatedAt = now }
    };

    context.Products.AddRange(products);
    context.SaveChanges();

    context.UserPreferences.AddRange(
        new UserPreference { UserId = users[0].Id, FavoriteStyles = ["Casual", "Practical"], FavoriteColors = ["Blue", "White"], FavoriteStores = ["Checkers", "Game"], PreferredPriceRangeMin = 100m, PreferredPriceRangeMax = 2500m },
        new UserPreference { UserId = users[1].Id, FavoriteStyles = ["Smart casual", "Classic"], FavoriteColors = ["Navy", "Cream"], FavoriteStores = ["Woolworths", "Pick n Pay"], PreferredPriceRangeMin = 150m, PreferredPriceRangeMax = 1800m },
        new UserPreference { UserId = users[2].Id, FavoriteStyles = ["Tech", "Minimal"], FavoriteColors = ["Black", "Grey"], FavoriteStores = ["Game", "Makro"], PreferredPriceRangeMin = 250m, PreferredPriceRangeMax = 5000m },
        new UserPreference { UserId = users[3].Id, FavoriteStyles = ["Family", "Comfort"], FavoriteColors = ["Green", "Brown"], FavoriteStores = ["Checkers", "Pick n Pay"], PreferredPriceRangeMin = 80m, PreferredPriceRangeMax = 1200m },
        new UserPreference { UserId = users[4].Id, FavoriteStyles = ["Sporty", "Everyday"], FavoriteColors = ["Black", "Red"], FavoriteStores = ["Makro", "Woolworths"], PreferredPriceRangeMin = 120m, PreferredPriceRangeMax = 2200m });

    context.Budgets.AddRange(
        new Budget { UserId = users[0].Id, Month = now.Month, Year = now.Year, MonthlyAmount = 4500m },
        new Budget { UserId = users[1].Id, Month = now.Month, Year = now.Year, MonthlyAmount = 3800m },
        new Budget { UserId = users[2].Id, Month = now.Month, Year = now.Year, MonthlyAmount = 6500m },
        new Budget { UserId = users[3].Id, Month = now.Month, Year = now.Year, MonthlyAmount = 3200m },
        new Budget { UserId = users[4].Id, Month = now.Month, Year = now.Year, MonthlyAmount = 5000m });

    context.SearchHistories.AddRange(
        new SearchHistory { UserId = users[0].Id, SearchTerm = "grocery specials near me", Budget = 700m, Location = "Soweto", SearchDate = monthStart.AddDays(2), ResultsCount = 8 },
        new SearchHistory { UserId = users[1].Id, SearchTerm = "cotton work shirts", Budget = 600m, Location = "Cape Town", SearchDate = monthStart.AddDays(4), ResultsCount = 5 },
        new SearchHistory { UserId = users[2].Id, SearchTerm = "smart TV deals", Budget = 4500m, Location = "Durban", SearchDate = monthStart.AddDays(6), ResultsCount = 7 },
        new SearchHistory { UserId = users[3].Id, SearchTerm = "black school shoes", Budget = 400m, Location = "Pretoria", SearchDate = monthStart.AddDays(8), ResultsCount = 6 },
        new SearchHistory { UserId = users[4].Id, SearchTerm = "South African cookbooks", Budget = 300m, Location = "Johannesburg", SearchDate = monthStart.AddDays(10), ResultsCount = 4 });

    context.CartItems.AddRange(
        new CartItem { UserId = users[0].Id, ProductId = products[0].Id, ProductName = products[0].Name, Price = products[0].Price, Quantity = 1, AddedDate = monthStart.AddDays(3) },
        new CartItem { UserId = users[1].Id, ProductId = products[2].Id, ProductName = products[2].Name, Price = products[2].Price, Quantity = 2, AddedDate = monthStart.AddDays(5) },
        new CartItem { UserId = users[2].Id, ProductId = products[1].Id, ProductName = products[1].Name, Price = products[1].Price, Quantity = 1, AddedDate = monthStart.AddDays(7) },
        new CartItem { UserId = users[3].Id, ProductId = products[3].Id, ProductName = products[3].Name, Price = products[3].Price, Quantity = 1, AddedDate = monthStart.AddDays(9) },
        new CartItem { UserId = users[4].Id, ProductId = products[4].Id, ProductName = products[4].Name, Price = products[4].Price, Quantity = 1, AddedDate = monthStart.AddDays(11) });

    context.PurchaseHistories.AddRange(
        BuildPurchase(users[0].Id, products[0], 1, monthStart.AddDays(4)),
        BuildPurchase(users[1].Id, products[2], 1, monthStart.AddDays(6)),
        BuildPurchase(users[2].Id, products[1], 1, monthStart.AddDays(8)),
        BuildPurchase(users[3].Id, products[3], 1, monthStart.AddDays(10)),
        BuildPurchase(users[4].Id, products[4], 2, monthStart.AddDays(12)));

    context.SaveChanges();
    EnsureShoppingAssistantTestProducts(context);
}

static void EnsureShoppingAssistantTestProducts(ApplicationDbContext context)
{
    var now = DateTime.UtcNow;
    var products = new[]
    {
        new Product { Name = "Velocity Red Running Shoe", Description = "Lightweight red running shoe for daily training.", Price = 449.99m, ShippingCost = 0m, StoreName = "SportScene", Category = "Shoes", Color = "Red", Size = "8", CreatedAt = now },
        new Product { Name = "Budget Android Smartphone", Description = "Affordable smartphone with mobile apps, camera, and long battery life.", Price = 1899.00m, ShippingCost = 59.00m, StoreName = "Takealot", Category = "Electronics", Color = "Black", Size = "One Size", CreatedAt = now },
        new Product { Name = "Essentials Grocery Basket", Description = "Groceries with rice, pasta, tinned food, tea, and pantry basics.", Price = 289.99m, ShippingCost = 35.00m, StoreName = "Checkers", Category = "Groceries", Size = "Basket", CreatedAt = now },
        new Product { Name = "Formal Navy Jacket", Description = "Smart formal jacket suitable for work, events, and interviews.", Price = 749.00m, ShippingCost = 60.00m, StoreName = "Woolworths", Category = "Outerwear", Color = "Navy", Size = "M", CreatedAt = now }
    };

    foreach (var product in products)
    {
        if (!context.Products.Any(existing => existing.Name == product.Name))
        {
            context.Products.Add(product);
        }
    }

    context.SaveChanges();
}

static void EnsureCurrentMonthBudgets(ApplicationDbContext context)
{
    var now = DateTime.UtcNow;
    var monthlyBudgetsByEmail = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["thabo.mokoena@example.co.za"] = 4500m,
        ["lerato.dlamini@example.co.za"] = 3800m,
        ["sipho.nkosi@example.co.za"] = 6500m,
        ["nomsa.maseko@example.co.za"] = 3200m,
        ["andile.khumalo@example.co.za"] = 5000m
    };

    var users = context.Users
        .Where(user => monthlyBudgetsByEmail.Keys.Contains(user.Email))
        .ToList();

    foreach (var user in users)
    {
        var hasBudget = context.Budgets.Any(budget =>
            budget.UserId == user.Id &&
            budget.Month == now.Month &&
            budget.Year == now.Year);

        if (!hasBudget)
        {
            context.Budgets.Add(new Budget
            {
                UserId = user.Id,
                Month = now.Month,
                Year = now.Year,
                MonthlyAmount = monthlyBudgetsByEmail[user.Email],
                CurrentSpending = 0m
            });
        }
    }

    context.SaveChanges();
}

static PurchaseHistory BuildPurchase(int userId, Product product, int quantity, DateTime purchaseDate)
{
    var lineTotal = product.Price * quantity;
    var items = new List<CartItemDto>
    {
        new()
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Price = product.Price,
            Quantity = quantity,
            LineTotal = lineTotal,
            AddedDate = purchaseDate
        }
    };

    return new PurchaseHistory
    {
        UserId = userId,
        PurchaseDate = purchaseDate,
        TotalAmount = lineTotal,
        Items = JsonSerializer.Serialize(items)
    };
}
