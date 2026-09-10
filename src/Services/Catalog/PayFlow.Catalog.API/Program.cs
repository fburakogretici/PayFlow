using Microsoft.EntityFrameworkCore;
using PayFlow.Catalog.API.Data;
using PayFlow.Catalog.API.Endpoints;
using PayFlow.Catalog.API.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı (EF Core)
builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("CatalogDb") ?? "Data Source=catalog.db");
});

// 2. Redis Dağıtık Önbellekleme (Distributed Cache)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "PayFlow_Catalog_";
});

builder.Services.AddScoped<ICatalogCacheService, CatalogCacheService>();

// 3. Health Checks (K8s Readiness/Liveness Probes için)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>("CatalogDb");

// 4. Swagger & API Explorer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PayFlow Catalog API", Version = "v1", Description = "Yüksek trafikli ürün kataloğu mikroservisi (Redis Cache-Aside destekli)" });
});

var app = builder.Build();

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
    c.RoutePrefix = string.Empty; // Doğrudan root URL'de açılsın
});

app.MapHealthChecks("/health");
app.MapCatalogEndpoints();

app.Run();
