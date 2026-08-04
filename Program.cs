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
builder.Services.AddScoped<StoreService>();
builder.Services.AddScoped<FileUploadService>();

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
    EnsureChatSchema(context);
    SeedStores(context);
    SeedDatabase(context);
    EnsureStoreUrls(context);
    LinkProductsToStores(context);
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

static void EnsureChatSchema(ApplicationDbContext context)
{
    // Some existing databases have the chat migration recorded in
    // __EFMigrationsHistory even though its tables are absent. Keep startup
    // resilient to that historical schema drift.
    context.Database.ExecuteSqlRaw("""
        IF OBJECT_ID(N'[ChatSessions]', N'U') IS NULL
        BEGIN
            CREATE TABLE [ChatSessions] (
                [Id] nvarchar(64) NOT NULL,
                [UserId] int NOT NULL,
                [StartedAt] datetime2 NOT NULL,
                [EndedAt] datetime2 NULL,
                CONSTRAINT [PK_ChatSessions] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_ChatSessions_Users_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
            );
            CREATE INDEX [IX_ChatSessions_UserId_EndedAt_StartedAt]
                ON [ChatSessions] ([UserId], [EndedAt], [StartedAt]);
        END;

        IF OBJECT_ID(N'[ChatMessages]', N'U') IS NULL
        BEGIN
            CREATE TABLE [ChatMessages] (
                [Id] int NOT NULL IDENTITY,
                [UserId] int NOT NULL,
                [Message] nvarchar(4000) NOT NULL,
                [Sender] nvarchar(10) NOT NULL,
                [Timestamp] datetime2 NOT NULL,
                [ChatSessionId] nvarchar(64) NOT NULL,
                CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_ChatMessages_ChatSessions_ChatSessionId]
                    FOREIGN KEY ([ChatSessionId]) REFERENCES [ChatSessions] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_ChatMessages_Users_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
            );
            CREATE INDEX [IX_ChatMessages_ChatSessionId_Timestamp]
                ON [ChatMessages] ([ChatSessionId], [Timestamp]);
            CREATE INDEX [IX_ChatMessages_UserId_Timestamp]
                ON [ChatMessages] ([UserId], [Timestamp]);
        END;

        IF OBJECT_ID(N'[ShoppingListItems]', N'U') IS NULL
        BEGIN
            CREATE TABLE [ShoppingListItems] (
                [Id] int NOT NULL IDENTITY,
                [UserId] int NOT NULL,
                [ProductId] int NOT NULL,
                [ProductName] nvarchar(150) NOT NULL,
                [Price] decimal(18,2) NOT NULL,
                [SelectedDate] datetime2 NOT NULL,
                CONSTRAINT [PK_ShoppingListItems] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_ShoppingListItems_Users_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX [IX_ShoppingListItems_UserId_ProductId]
                ON [ShoppingListItems] ([UserId], [ProductId]);
        END;
        """);
}

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
        new() { Name = "Checkers Family Grocery Hamper", Description = "Pantry staples with maize meal, rice, pasta, tinned food, and tea.", Price = 549.99m, ShippingCost = 35.00m, StoreName = "Checkers", StoreUrl = "https://shop.checkers.co.za/product/family-grocery-hamper", Category = "Groceries", Size = "Family Pack", CreatedAt = now },
        new() { Name = "Samsung 32-inch Smart TV", Description = "HD smart television for streaming and everyday viewing.", Price = 3999.00m, ShippingCost = 149.00m, StoreName = "Game", StoreUrl = "https://www.game.co.za/product/samsung-32-inch-smart-tv", Category = "Electronics", Color = "Black", Size = "32 inch", CreatedAt = now },
        new() { Name = "Woolworths Cotton Chino Shirt", Description = "Classic cotton shirt for work or weekends.", Price = 499.00m, ShippingCost = 60.00m, StoreName = "Woolworths", StoreUrl = "https://www.woolworths.co.za/prod/cotton-chino-shirt", Category = "Clothing", Color = "Navy", Size = "M", CreatedAt = now },
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

    context.PurchaseHistories.AddRange(
        BuildPurchase(users[0].Id, products[0], 1, monthStart.AddDays(4)),
        BuildPurchase(users[1].Id, products[2], 1, monthStart.AddDays(6)),
        BuildPurchase(users[2].Id, products[1], 1, monthStart.AddDays(8)),
        BuildPurchase(users[3].Id, products[3], 1, monthStart.AddDays(10)),
        BuildPurchase(users[4].Id, products[4], 2, monthStart.AddDays(12)));

    context.SaveChanges();
    EnsureShoppingAssistantTestProducts(context);
}

static void SeedStores(ApplicationDbContext context)
{
    var durbanStores = new[]
    {
        new Store { Name = "Gateway Mall", Address = "1 Palm Boulevard, Umhlanga Ridge, Durban, 4320", Latitude = -29.7207, Longitude = 31.0774, Category = "Shopping Mall" },
        new Store { Name = "Woolworths Musgrave", Address = "115 Musgrave Road, Musgrave, Durban, 4001", Latitude = -29.8447, Longitude = 30.9985, Category = "Grocery" },
        new Store { Name = "Checkers Hyper By The Sea", Address = "Cnr Lighthouse Road and Battery Beach Road, Durban, 4001", Latitude = -29.8500, Longitude = 31.0300, Category = "Grocery" },
        new Store { Name = "Pick n Pay Hyper Savages", Address = "144 Helen Joseph Road, Glenwood, Durban, 4001", Latitude = -29.8733, Longitude = 30.9933, Category = "Grocery" },
        new Store { Name = "Game Musgrave Centre", Address = "115 Musgrave Road, Musgrave, Durban, 4001", Latitude = -29.8447, Longitude = 30.9985, Category = "Retail" },
        new Store { Name = "Makro Durban", Address = "1 Bellville Road, Durban, 4001", Latitude = -29.8733, Longitude = 31.0400, Category = "Retail" },
        new Store { Name = "SPAR Glenwood", Address = "474 Roberts Road, Glenwood, Durban, 4001", Latitude = -29.8733, Longitude = 30.9933, Category = "Grocery" },
        new Store { Name = "The Pavilion Shopping Centre", Address = "20 Quarry Road, Westville, Durban, 3629", Latitude = -29.8200, Longitude = 30.9300, Category = "Shopping Mall" },
        new Store { Name = "Clicks Musgrave", Address = "115 Musgrave Road, Musgrave, Durban, 4001", Latitude = -29.8447, Longitude = 30.9985, Category = "Pharmacy" },
        new Store { Name = "Dis-Chem Gateway", Address = "1 Palm Boulevard, Umhlanga Ridge, Durban, 4320", Latitude = -29.7207, Longitude = 31.0774, Category = "Pharmacy" }
    };

    foreach (var store in durbanStores)
    {
        if (!context.Stores.Any(existing => existing.Name == store.Name)) context.Stores.Add(store);
    }

    context.SaveChanges();
}

static void LinkProductsToStores(ApplicationDbContext context)
{
    var stores = context.Stores.ToList();
    var unmatchedProducts = context.Products.Where(product => product.StoreId == null).ToList();
    foreach (var product in unmatchedProducts)
    {
        var store = stores.FirstOrDefault(candidate =>
            candidate.Name.Contains(product.StoreName, StringComparison.OrdinalIgnoreCase) ||
            product.StoreName.Contains(candidate.Name, StringComparison.OrdinalIgnoreCase));
        if (store is not null) product.StoreId = store.Id;
    }

    context.SaveChanges();
}

static void EnsureShoppingAssistantTestProducts(ApplicationDbContext context)
{
    var now = DateTime.UtcNow;
    var products = new[]
    {
        new Product { Name = "Velocity Red Running Shoe", Description = "Lightweight red running shoe for daily training.", Price = 449.99m, ShippingCost = 0m, StoreName = "SportScene", Category = "Shoes", Color = "Red", Size = "8", CreatedAt = now },
        new Product { Name = "Budget Android Smartphone", Description = "Affordable smartphone with mobile apps, camera, and long battery life.", Price = 1899.00m, ShippingCost = 59.00m, StoreName = "Takealot", StoreUrl = "https://www.takealot.com/budget-android-smartphone/PLID123456", Category = "Electronics", Color = "Black", Size = "One Size", CreatedAt = now },
        new Product { Name = "Essentials Grocery Basket", Description = "Groceries with rice, pasta, tinned food, tea, and pantry basics.", Price = 289.99m, ShippingCost = 35.00m, StoreName = "Checkers", StoreUrl = "https://shop.checkers.co.za/product/essentials-grocery-basket", Category = "Groceries", Size = "Basket", CreatedAt = now },
        new Product { Name = "Formal Navy Jacket", Description = "Smart formal jacket suitable for work, events, and interviews.", Price = 749.00m, ShippingCost = 60.00m, StoreName = "Woolworths", StoreUrl = "https://www.woolworths.co.za/prod/formal-navy-jacket", Category = "Outerwear", Color = "Navy", Size = "M", CreatedAt = now }
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

static void EnsureStoreUrls(ApplicationDbContext context)
{
    var urlsByProductName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Checkers Family Grocery Hamper"] = "https://shop.checkers.co.za/product/family-grocery-hamper",
        ["Samsung 32-inch Smart TV"] = "https://www.game.co.za/product/samsung-32-inch-smart-tv",
        ["Woolworths Cotton Chino Shirt"] = "https://www.woolworths.co.za/prod/cotton-chino-shirt",
        ["Budget Android Smartphone"] = "https://www.takealot.com/budget-android-smartphone/PLID123456",
        ["Essentials Grocery Basket"] = "https://shop.checkers.co.za/product/essentials-grocery-basket",
        ["Formal Navy Jacket"] = "https://www.woolworths.co.za/prod/formal-navy-jacket"
    };

    foreach (var product in context.Products.Where(product => string.IsNullOrWhiteSpace(product.StoreUrl)))
    {
        if (urlsByProductName.TryGetValue(product.Name, out var storeUrl)) product.StoreUrl = storeUrl;
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
    var items = new List<PurchaseItemSnapshotDto>
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
