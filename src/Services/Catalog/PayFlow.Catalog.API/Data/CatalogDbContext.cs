using Microsoft.EntityFrameworkCore;
using PayFlow.Catalog.API.Models;

namespace PayFlow.Catalog.API.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(p => p.Description)
                .HasMaxLength(1000);

            entity.Property(p => p.Category)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(p => p.Price)
                .HasPrecision(18, 2);

            // 10.000+ kullanıcı/yüksek trafik için indeksleme optimizasyonu:
            entity.HasIndex(p => p.Category);
            entity.HasIndex(p => p.Name);
        });
    }
}
