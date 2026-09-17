using MassTransit;
using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.API.Consumers;
using PayFlow.Payment.API.Data;
using PayFlow.Payment.API.Endpoints;
using PayFlow.Payment.API.Soap;
using PayFlow.SharedKernel.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog & Seq Yapılandırması (Observability)
builder.Host.UseSerilog((context, configuration) =>
{
    var seqUrl = context.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "PayFlow.Payment.API")
        .WriteTo.Console()
        .WriteTo.Seq(seqUrl);
});

// 2. EF Core Veritabanı (PostgreSQL / SQLite Fallback)
builder.Services.AddDbContext<PaymentDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("PaymentDb");
    if (!string.IsNullOrEmpty(conn) && (conn.Contains("Host=") || conn.Contains("Server=") || conn.Contains("Port=")))
    {
        options.UseNpgsql(conn);
    }
    else
    {
        options.UseSqlite(conn ?? "Data Source=payment.db");
    }
});

// 3. SOAP Banka Adaptörü (Polly Resilience Pipeline ile Korunan)
builder.Services.AddSingleton<IBankSoapAdapter, BankSoapAdapter>();

// 4. MassTransit & RabbitMQ Konfigürasyonu
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var user = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var pass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(host, "/", h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        cfg.ConfigureEndpoints(context);
    });
});

// 5. Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>("PaymentDb");

// 6. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "PayFlow Payment API",
        Version = "v1",
        Description = "WCF/SOAP Banka Entegrasyonu, Idempotency Koruması ve Polly Circuit Breaker kullanan Ödeme Mikroservisi"
    });
});

var app = builder.Build();

app.UseCorrelationId();

// Otomatik DB oluşturma
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment API v1");
    c.RoutePrefix = string.Empty;
});

app.MapHealthChecks("/health");
app.MapPaymentEndpoints();

app.Run();
