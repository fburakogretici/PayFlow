using Microsoft.EntityFrameworkCore;
using PayFlow.Catalog.API.Data;
using PayFlow.Catalog.API.Endpoints;
using PayFlow.Catalog.API.Services;
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
        .Enrich.WithProperty("Application", "PayFlow.Catalog.API")
        .WriteTo.Console()
        .WriteTo.Seq(seqUrl);
});

// 2. Veritabanı Katmanı (PostgreSQL / SQLite Fallback)
builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("CatalogDb");
    if (!string.IsNullOrEmpty(conn) && (conn.Contains("Host=") || conn.Contains("Server=") || conn.Contains("Port=")))
    {
        options.UseNpgsql(conn);
    }
    else
    {
        options.UseSqlite(conn ?? "Data Source=catalog.db");
    }
});

// 3. Redis Dağıtık Önbellekleme (Distributed Cache)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "PayFlow_Catalog_";
});

builder.Services.AddScoped<ICatalogCacheService, CatalogCacheService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();


// 4. Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>("CatalogDb");

// 5. Swagger & API Explorer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "PayFlow Catalog API",
        Version = "v1",
        Description = "PostgreSQL, Redis Cache-Aside ve Polly Resilience Pipeline kullanan Ürün Kataloğu Servisi"
    });
});

var app = builder.Build();

app.UseCorrelationId();

// Otomatik DB oluşturma ve tohumlama (Seed)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await CatalogDataSeeder.SeedAsync(db);
}

// Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog API v1");
    c.RoutePrefix = string.Empty;
});

app.MapHealthChecks("/health");
app.MapCatalogEndpoints();

app.Run();
