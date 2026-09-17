using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PayFlow.Ordering.API.Consumers;
using PayFlow.Ordering.API.Endpoints;
using PayFlow.Ordering.API.Infrastructure.BackgroundServices;
using PayFlow.Ordering.API.Infrastructure.Persistence;
using PayFlow.SharedKernel.Logging;
using PayFlow.SharedKernel.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog & Seq Yapılandırması (Observability)
builder.Host.UseSerilog((context, configuration) =>
{
    var seqUrl = context.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "PayFlow.Ordering.API")
        .WriteTo.Console()
        .WriteTo.Seq(seqUrl);
});

// 2. EF Core Veritabanı (PostgreSQL / SQLite Fallback)
builder.Services.AddDbContext<OrderDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("OrderingDb");
    if (!string.IsNullOrEmpty(conn) && (conn.Contains("Host=") || conn.Contains("Server=") || conn.Contains("Port=")))
    {
        options.UseNpgsql(conn);
    }
    else
    {
        options.UseSqlite(conn ?? "Data Source=ordering.db");
    }
});

// 3. JWT Kimlik Doğrulama & Yetkilendirme
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = JwtTokenGenerator.GetValidationParameters();
    });
builder.Services.AddAuthorization();

// 4. MediatR (CQRS Handler'larını otomatik tara ve kaydet)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// 5. MassTransit & RabbitMQ Konfigürasyonu
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

// 6. Transactional Outbox Worker (Background Service)
builder.Services.AddHostedService<OutboxProcessor>();

// 7. Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("OrderingDb");

// 8. Swagger & JWT Entegrasyonu
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PayFlow Ordering API",
        Version = "v1",
        Description = "Clean Architecture, CQRS, Transactional Outbox Pattern ve JWT Güvenliği kullanan Sipariş Mikroservisi"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Token giriniz: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseCorrelationId();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapOrderEndpoints();

app.Run();
