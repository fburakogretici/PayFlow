using MassTransit;
using Microsoft.EntityFrameworkCore;
using PayFlow.Ordering.API.Consumers;
using PayFlow.Ordering.API.Endpoints;
using PayFlow.Ordering.API.Infrastructure.BackgroundServices;
using PayFlow.Ordering.API.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. EF Core Veritabanı
builder.Services.AddDbContext<OrderDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("OrderingDb") ?? "Data Source=ordering.db");
});

// 2. MediatR (CQRS Handler'larını otomatik tara ve kaydet)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// 3. MassTransit & RabbitMQ Konfigürasyonu
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentCompletedConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();

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

// 4. Transactional Outbox Worker (Background Service)
builder.Services.AddHostedService<OutboxProcessor>();

// 5. Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("OrderingDb");

// 6. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PayFlow Ordering API", Version = "v1", Description = "Clean Architecture, CQRS ve Outbox Pattern kullanan Sipariş Mikroservisi" });
});

var app = builder.Build();

// Otomatik DB oluşturma
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ordering API v1");
    c.RoutePrefix = string.Empty;
});

app.MapHealthChecks("/health");
app.MapOrderEndpoints();

app.Run();
