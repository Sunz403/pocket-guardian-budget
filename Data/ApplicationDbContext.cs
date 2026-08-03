using System.Text.Json;
using AIShoppingAssistant.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AIShoppingAssistant.Data;

public class ApplicationDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    public DbSet<Product> Products { get; set; }

    public DbSet<Store> Stores { get; set; }

    public DbSet<UserPreference> UserPreferences { get; set; }

    public DbSet<SearchHistory> SearchHistories { get; set; }

    public DbSet<Budget> Budgets { get; set; }

    public DbSet<ShoppingListItem> ShoppingListItems { get; set; }

    public DbSet<PurchaseHistory> PurchaseHistories { get; set; }

    public DbSet<ChatSession> ChatSessions { get; set; }

    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.Email)
                .IsUnique();

            entity.HasOne(user => user.UserPreference)
                .WithOne(preference => preference.User)
                .HasForeignKey<UserPreference>(preference => preference.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasIndex(store => store.Name).IsUnique();
            entity.Property(store => store.Latitude).HasColumnType("float");
            entity.Property(store => store.Longitude).HasColumnType("float");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasOne(product => product.Store)
                .WithMany(store => store.Products)
                .HasForeignKey(product => product.StoreId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(product => product.StoreId);
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.Property(preference => preference.FavoriteStyles)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, JsonSerializerOptions),
                    value => JsonSerializer.Deserialize<List<string>>(value, JsonSerializerOptions) ?? new List<string>())
                .Metadata.SetValueComparer(CreateStringListComparer());

            entity.Property(preference => preference.FavoriteColors)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, JsonSerializerOptions),
                    value => JsonSerializer.Deserialize<List<string>>(value, JsonSerializerOptions) ?? new List<string>())
                .Metadata.SetValueComparer(CreateStringListComparer());

            entity.Property(preference => preference.FavoriteStores)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, JsonSerializerOptions),
                    value => JsonSerializer.Deserialize<List<string>>(value, JsonSerializerOptions) ?? new List<string>())
                .Metadata.SetValueComparer(CreateStringListComparer());

            entity.Property(preference => preference.FavoriteStyles)
                .HasColumnType("nvarchar(max)");

            entity.Property(preference => preference.FavoriteColors)
                .HasColumnType("nvarchar(max)");

            entity.Property(preference => preference.FavoriteStores)
                .HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<SearchHistory>(entity =>
        {
            entity.HasOne(history => history.User)
                .WithMany(user => user.SearchHistories)
                .HasForeignKey(history => history.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Budget>(entity =>
        {
            entity.HasOne(budget => budget.User)
                .WithMany(user => user.Budgets)
                .HasForeignKey(budget => budget.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(budget => new { budget.UserId, budget.Month, budget.Year })
                .IsUnique();
        });

        modelBuilder.Entity<ShoppingListItem>(entity =>
        {
            entity.HasOne(item => item.User)
                .WithMany(user => user.ShoppingListItems)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(item => new { item.UserId, item.ProductId })
                .IsUnique();
        });

        modelBuilder.Entity<PurchaseHistory>(entity =>
        {
            entity.HasOne(purchase => purchase.User)
                .WithMany(user => user.PurchaseHistories)
                .HasForeignKey(purchase => purchase.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(purchase => purchase.Items).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasOne(session => session.User)
                .WithMany(user => user.ChatSessions)
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(session => new { session.UserId, session.EndedAt, session.StartedAt });
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasOne(message => message.ChatSession)
                .WithMany(session => session.Messages)
                .HasForeignKey(message => message.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(message => message.User)
                .WithMany(user => user.ChatMessages)
                .HasForeignKey(message => message.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(message => new { message.ChatSessionId, message.Timestamp });
            entity.HasIndex(message => new { message.UserId, message.Timestamp });
        });

        /* Product data is seeded at application startup so images are not represented by stale external URLs.
            new Product { Id = 2, Name = "Slim Fit Jeans", Description = "Denim jeans with a modern slim fit.", Price = 49.99m, Color = "Blue", Size = "32", ShippingCost = 6.99m, StoreName = "Denim Corner", Category = "Clothing", ImageUrl = "https://example.com/images/slim-jeans.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 3, Name = "Running Sneakers", Description = "Lightweight sneakers for daily runs.", Price = 89.50m, Color = "Black", Size = "9", ShippingCost = 8.99m, StoreName = "FastFeet", Category = "Shoes", ImageUrl = "https://example.com/images/running-sneakers.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 4, Name = "Leather Handbag", Description = "Medium-sized handbag with zip closure.", Price = 120.00m, Color = "Brown", Size = "Medium", ShippingCost = 10.50m, StoreName = "Urban Vogue", Category = "Accessories", ImageUrl = "https://example.com/images/leather-handbag.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 5, Name = "Wireless Headphones", Description = "Noise-reducing over-ear headphones.", Price = 159.99m, Color = "Silver", Size = "One Size", ShippingCost = 12.99m, StoreName = "TechNest", Category = "Electronics", ImageUrl = "https://example.com/images/wireless-headphones.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 6, Name = "Yoga Leggings", Description = "Stretch-fit leggings for workouts.", Price = 34.95m, Color = "Purple", Size = "S", ShippingCost = 5.50m, StoreName = "ActiveLife", Category = "Activewear", ImageUrl = "https://example.com/images/yoga-leggings.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 7, Name = "Wool Scarf", Description = "Warm scarf for cooler weather.", Price = 24.99m, Color = "Gray", Size = "One Size", ShippingCost = 3.99m, StoreName = "CozyWear", Category = "Accessories", ImageUrl = "https://example.com/images/wool-scarf.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 8, Name = "Smart Watch", Description = "Fitness tracking smartwatch with notifications.", Price = 199.99m, Color = "Black", Size = "42mm", ShippingCost = 9.99m, StoreName = "TechNest", Category = "Electronics", ImageUrl = "https://example.com/images/smart-watch.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 9, Name = "Formal Shirt", Description = "Button-up shirt for office and events.", Price = 39.99m, Color = "Light Blue", Size = "L", ShippingCost = 5.99m, StoreName = "StyleHub", Category = "Clothing", ImageUrl = "https://example.com/images/formal-shirt.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 10, Name = "Canvas Backpack", Description = "Durable backpack for school or travel.", Price = 54.99m, Color = "Olive", Size = "Large", ShippingCost = 7.25m, StoreName = "TrailLine", Category = "Bags", ImageUrl = "https://example.com/images/canvas-backpack.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 11, Name = "Summer Dress", Description = "Light floral dress for warm days.", Price = 62.49m, Color = "Yellow", Size = "M", ShippingCost = 6.49m, StoreName = "Urban Vogue", Category = "Clothing", ImageUrl = "https://example.com/images/summer-dress.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 12, Name = "Leather Belt", Description = "Classic leather belt with metal buckle.", Price = 22.00m, Color = "Black", Size = "34", ShippingCost = 4.49m, StoreName = "Denim Corner", Category = "Accessories", ImageUrl = "https://example.com/images/leather-belt.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 13, Name = "Gaming Mouse", Description = "Ergonomic mouse with adjustable DPI.", Price = 45.75m, Color = "Red", Size = "One Size", ShippingCost = 5.99m, StoreName = "ClickZone", Category = "Electronics", ImageUrl = "https://example.com/images/gaming-mouse.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 14, Name = "Puffer Jacket", Description = "Insulated jacket for winter weather.", Price = 140.00m, Color = "Navy", Size = "XL", ShippingCost = 11.99m, StoreName = "CozyWear", Category = "Outerwear", ImageUrl = "https://example.com/images/puffer-jacket.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 15, Name = "Kitchen Blender", Description = "High-speed blender for smoothies and soups.", Price = 79.99m, Color = "White", Size = "1.5L", ShippingCost = 13.50m, StoreName = "HomeEase", Category = "Home Appliances", ImageUrl = "https://example.com/images/kitchen-blender.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 16, Name = "Sports Cap", Description = "Adjustable cap with breathable fabric.", Price = 18.50m, Color = "Green", Size = "One Size", ShippingCost = 3.75m, StoreName = "ActiveLife", Category = "Accessories", ImageUrl = "https://example.com/images/sports-cap.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 17, Name = "Ankle Boots", Description = "Stylish boots with side zipper.", Price = 95.00m, Color = "Tan", Size = "8", ShippingCost = 8.50m, StoreName = "FastFeet", Category = "Shoes", ImageUrl = "https://example.com/images/ankle-boots.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 18, Name = "Desk Lamp", Description = "LED desk lamp with adjustable brightness.", Price = 31.20m, Color = "Silver", Size = "Medium", ShippingCost = 6.25m, StoreName = "HomeEase", Category = "Home Decor", ImageUrl = "https://example.com/images/desk-lamp.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 19, Name = "Sunglasses", Description = "UV-protected sunglasses with slim frame.", Price = 27.99m, Color = "Black", Size = "One Size", ShippingCost = 4.20m, StoreName = "StyleHub", Category = "Accessories", ImageUrl = "https://example.com/images/sunglasses.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 20, Name = "Portable Speaker", Description = "Bluetooth speaker with rich sound.", Price = 68.80m, Color = "Blue", Size = "Compact", ShippingCost = 7.80m, StoreName = "ClickZone", Category = "Electronics", ImageUrl = "https://example.com/images/portable-speaker.jpg", CreatedAt = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc) }
        ); */
    }

    private static ValueComparer<List<string>> CreateStringListComparer()
    {
        return new ValueComparer<List<string>>(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            value => value.ToList());
    }
}
