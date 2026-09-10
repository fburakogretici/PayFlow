using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 1. YARP Reverse Proxy Yapılandırması
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// 2. Yüksek Trafik (10.000+ kullanıcı) için Rate Limiter (İstek Sınırlama)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global sabit pencere rate limiter: IP/İstemci başına dakikada maksimum 100 istek
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueLimit = 20;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// 3. CORS Yapılandırması
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseRateLimiter();

// Gateway durum kontrolü
app.MapGet("/", () => Results.Ok(new
{
    Name = "PayFlow API Gateway (YARP)",
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Endpoints = new[]
    {
        "/api/catalog -> Catalog Service",
        "/api/orders  -> Ordering Service",
        "/api/payments -> Payment Service"
    }
}));

app.MapReverseProxy();

app.Run();
