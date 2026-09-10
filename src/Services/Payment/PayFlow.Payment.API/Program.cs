using MassTransit;
using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.API.Consumers;
using PayFlow.Payment.API.Data;
using PayFlow.Payment.API.Endpoints;
using PayFlow.Payment.API.Soap;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı (EF Core)
builder.Services.AddDbContext<PaymentDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("PaymentDb") ?? "Data Source=payment.db");
});

// 2. SOAP Banka Entegrasyon Adaptörü
builder.Services.AddScoped<IBankSoapAdapter, BankSoapAdapter>();

// 3. MassTransit & RabbitMQ Konfigürasyonu
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

// 4. Swagger & API Explorer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PayFlow Payment API", Version = "v1", Description = "Event-Driven & SOAP Banka Entegrasyonlu Ödeme Mikroservisi" });
});

var app = builder.Build();

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

app.MapPaymentEndpoints();

app.Run();
