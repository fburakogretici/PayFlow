using Microsoft.EntityFrameworkCore;
using PayFlow.Ordering.API.Domain.Models;
using PayFlow.SharedKernel.Outbox;

namespace PayFlow.Ordering.API.Infrastructure.Persistence;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Order Yapılandırması
        modelBuilder.Entity<Order>(builder =>
        {
            builder.HasKey(o => o.Id);

            builder.Property(o => o.CustomerEmail)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(o => o.PaymentTransactionId)
                .HasMaxLength(100);

            // DDD Value Object: ShippingAddress gömülü nesne (Owned Entity)
            builder.OwnsOne(o => o.ShippingAddress, nav =>
            {
                nav.Property(a => a.Street).HasMaxLength(200).IsRequired();
                nav.Property(a => a.City).HasMaxLength(100).IsRequired();
                nav.Property(a => a.Country).HasMaxLength(100).IsRequired();
                nav.Property(a => a.ZipCode).HasMaxLength(20).IsRequired();
            });

            // 1-to-many ilişki (Order -> OrderItems)
            builder.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(o => o.CustomerId);
            builder.HasIndex(o => o.Status);
        });

        // 2. OrderItem Yapılandırması
        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.HasKey(i => i.Id);
            builder.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
            builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        });

        // 3. OutboxMessage Yapılandırması (Transactional Outbox Pattern)
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Type).IsRequired().HasMaxLength(250);
            builder.Property(m => m.Content).IsRequired();

            // Yüksek performans: İşlenmemiş mesajları hızlıca sorgulamak için index
            builder.HasIndex(m => m.ProcessedOnUtc);
        });
    }
}
