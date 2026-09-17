using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
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
        .Enrich.WithProperty("Application", "PayFlow.ApiGateway")
        .WriteTo.Console()
        .WriteTo.Seq(seqUrl);
});

// 2. YARP Reverse Proxy Yapılandırması
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// 3. JWT Kimlik Doğrulama & Yetkilendirme
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = JwtTokenGenerator.GetValidationParameters();
    });
builder.Services.AddAuthorization();

// 4. Rate Limiter (Yüksek Trafik 10.000+ İstek Koruması)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueLimit = 20;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// 5. CORS Yapılandırması
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 6. Swagger UI & JWT Desteği
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PayFlow Enterprise API Gateway",
        Version = "v1",
        Description = "YARP tabanlı API Gateway, JWT Yetkilendirme, Rate Limiting ve Seq İzleme"
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
app.UseCors();
app.UseRateLimiter();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PayFlow Gateway API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

// Gateway Bilgilendirme ve Health Endpoint'i
app.MapGet("/health", () => Results.Ok(new
{
    Service = "PayFlow.ApiGateway",
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Observability = "Seq (http://localhost:5341)",
    Clusters = new[] { "catalog", "ordering", "payment" }
})).WithTags("Health");

// Auth Endpoint: Test Kullanıcıları için JWT Üretimi
app.MapPost("/api/auth/login", (LoginRequest request) =>
{
    // CV & Test Ortamı İçin Ön Tanımlı Kullanıcılar
    if (request.Email.Equals("admin@payflow.com", StringComparison.OrdinalIgnoreCase) && request.Password == "Password123!")
    {
        var token = JwtTokenGenerator.GenerateToken(Guid.Parse("11111111-1111-1111-1111-111111111111"), request.Email, "Admin");
        return Results.Ok(new AuthResponse(token, request.Email, "Admin", JwtConstants.ExpirationMinutes * 60));
    }

    if (request.Email.Equals("customer@payflow.com", StringComparison.OrdinalIgnoreCase) && request.Password == "Password123!")
    {
        var token = JwtTokenGenerator.GenerateToken(Guid.Parse("22222222-2222-2222-2222-222222222222"), request.Email, "Customer");
        return Results.Ok(new AuthResponse(token, request.Email, "Customer", JwtConstants.ExpirationMinutes * 60));
    }

    return Results.Unauthorized();
}).WithTags("Authentication").WithSummary("JWT Token Üretme (admin@payflow.com veya customer@payflow.com / Password123!)");

app.MapReverseProxy();

app.Run();

public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, string Email, string Role, int ExpiresInSeconds);
