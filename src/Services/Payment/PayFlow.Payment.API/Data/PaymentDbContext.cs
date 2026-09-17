using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.API.Models;

namespace PayFlow.Payment.API.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PaymentRecord>(builder =>
        {
            builder.HasKey(p => p.Id);

            // Dağıtık Sistem Idempotency Garantisi: Aynı OrderId için ikinci kez ödeme çekilemez!
            builder.HasIndex(p => p.OrderId).IsUnique();

            builder.Property(p => p.Amount).HasPrecision(18, 2);

            // PaymentStatus enum → DB'de integer olarak saklanır (performans + type-safety)
            builder.Property(p => p.Status)
                .HasConversion<string>()  // string olarak sakla, DB'den okuyunca otomatik enum'a çevir
                .HasMaxLength(50)
                .IsRequired();
            builder.Property(p => p.BankTransactionCode).HasMaxLength(100);
        });
    }
}
