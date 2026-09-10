using Microsoft.EntityFrameworkCore;
using PayFlow.Catalog.API.Models;

namespace PayFlow.Catalog.API.Data;

public static class CatalogDataSeeder
{
    public static async Task SeedAsync(CatalogDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (await context.Products.AnyAsync()) return;

        var sampleProducts = new List<Product>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "MacBook Pro 16\"",
                Description = "Apple M3 Max, 36GB RAM, 1TB SSD Space Black",
                Price = 3499.99m,
                StockQuantity = 50,
                Category = "Electronics"
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Dell UltraSharp 27\" 4K Monitor",
                Description = "USB-C Hub Monitor, IPS Black, HDR 400",
                Price = 629.50m,
                StockQuantity = 120,
                Category = "Electronics"
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Logitech MX Master 3S Mouse",
                Description = "Ergonomic wireless mouse with Quiet Clicks and 8K DPI sensor",
                Price = 99.99m,
                StockQuantity = 300,
                Category = "Accessories"
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Keychron Q1 Pro Wireless Mechanical Keyboard",
                Description = "QMK/VIA wireless custom mechanical keyboard, Hot-swappable",
                Price = 199.00m,
                StockQuantity = 85,
                Category = "Accessories"
            }
        };

        await context.Products.AddRangeAsync(sampleProducts);
        await context.SaveChangesAsync();
    }
}
