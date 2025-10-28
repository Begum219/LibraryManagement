using Domain.Entities;
using Infrastructure;
using Infrastructure.Middlewares;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Serilog;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ============= RATE LIMITING EKLE =============
builder.Services.AddRateLimiter(options =>
{
    // 1. GENEL API LİMİTİ - Tüm endpoint'ler için
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,                          // Dakikada 100 istek
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // 2. LOGIN LİMİTİ - Brute force koruması
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;                                // 5 dakikada 5 istek
        opt.Window = TimeSpan.FromMinutes(5);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // 3. REGISTER LİMİTİ - Spam hesap açmayı önle
    options.AddFixedWindowLimiter("register", opt =>
    {
        opt.PermitLimit = 3;                                // Saatte 3 kayıt
        opt.Window = TimeSpan.FromHours(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // 4. SEARCH LİMİTİ - Arama endpoint'leri
    options.AddFixedWindowLimiter("search", opt =>
    {
        opt.PermitLimit = 30;                               // Dakikada 30 arama
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Limit aşıldığında ne olacak
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = 429;      // Too Many Requests

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "Çok fazla istek gönderdiniz. Lütfen birkaç dakika bekleyin.",
            retryAfter = "60 saniye"
        }, cancellationToken);
    };
});
// ============= RATE LIMITING BİTİŞ =============

// ✅ GitHub Secrets için Environment Variables
var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("LibraryDB");

var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["JwtSettings:SecretKey"];

// Override configuration
builder.Configuration["ConnectionStrings:LibraryDB"] = connectionString;
builder.Configuration["JwtSettings:SecretKey"] = jwtSecretKey;

// Serilog için connection string override
builder.Configuration["Serilog:WriteTo:2:Args:connectionString"] = connectionString;

// Serilog Configuration
Serilog.Log.Logger = new Serilog.LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();

// Infrastructure Services (DbContext, Repositories, UnitOfWork, Auth Services)
builder.Services.AddInfrastructureServices(builder.Configuration);

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]!)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Swagger Configuration with JWT Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Library Management API",
        Version = "v1",
        Description = "Kütüphane Yönetim Sistemi API"
    });

    // JWT Authentication için Swagger yapılandırması
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header kullanarak giriş yapın. Örnek: 'Bearer {token}'"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

var app = builder.Build();

// Middleware'ler ve sıralaması önemli
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ============= RATE LIMITER EKLE =============
app.UseRateLimiter();  // ← EKLENDI!
// ============================================

app.UseAuthentication(); // ÖNEMLİ: Authorization'dan ÖNCE gelmeli
app.UseAuthorization();

app.MapControllers();

try
{
    Serilog.Log.Information("Uygulama başlatılıyor...");
    app.Run();
}
catch (Exception ex)
{
    Serilog.Log.Fatal(ex, "Uygulama başlatılamadı!");
}
finally
{
    Serilog.Log.CloseAndFlush();
}