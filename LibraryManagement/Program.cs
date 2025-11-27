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
using LibraryManagement.Application.Converters;
using Hangfire;
using Hangfire.SqlServer;
using Infrastructure.Jobs;
using Infrastructure.Hangfire;
using Application.Interfaces.Services;
using System.Net.Mail;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// ============= RATE LIMITING =============
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(5);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("register", opt =>
    {
        opt.PermitLimit = 3;
        opt.Window = TimeSpan.FromHours(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("search", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "Çok fazla istek gönderdiniz. Lütfen birkaç dakika bekleyin.",
            retryAfter = "60 saniye"
        }, cancellationToken);
    };
});

// ============= ENVIRONMENT VARIABLES =============
var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("LibraryDB");

var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["JwtSettings:SecretKey"];

builder.Configuration["ConnectionStrings:LibraryDB"] = connectionString;
builder.Configuration["JwtSettings:SecretKey"] = jwtSecretKey;
builder.Configuration["Serilog:WriteTo:2:Args:connectionString"] = connectionString;

// ============= SERILOG =============
Serilog.Log.Logger = new Serilog.LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// ============= CONTROLLERS =============
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new Base64Converter());
    });

// ============= INFRASTRUCTURE SERVICES =============
builder.Services.AddInfrastructureServices(builder.Configuration);

// ============= JWT AUTHENTICATION =============
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

// ============= HANGFIRE CONFIGURATION =============
builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
          {
              CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
              SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
              QueuePollInterval = TimeSpan.Zero,
              UseRecommendedIsolationLevel = true,
              DisableGlobalLocks = true
          });
});

builder.Services.AddHangfireServer();

// ============= SWAGGER =============
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Library Management API",
        Version = "v1",
        Description = "Kütüphane Yönetim Sistemi API"
    });

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

// ============= MIDDLEWARES =============
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ============= HANGFIRE DASHBOARD =============
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthFilter() }
});

// ============= RECURRING JOB - TEST İÇİN 2 DAKİKADA BİR ============= 
RecurringJob.AddOrUpdate<OverdueBookJob>(
    "overdue-book-notifications",
    job => job.SendOverdueNotifications(),
    "*/2 * * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time")
    }
);

app.MapControllers();

// ============= ADMIN & DEBUG ENDPOINTS =============

// ✅ Email ayarlarını kontrol et
app.MapGet("/debug-email-settings", (IConfiguration config) =>
{
    return Results.Ok(new
    {
        SmtpHost = config["EmailSettings:SmtpHost"],
        SmtpPort = config["EmailSettings:SmtpPort"],
        EnableSsl = config["EmailSettings:EnableSsl"],
        Username = config["EmailSettings:Username"],
        Password = config["EmailSettings:Password"]?.Length > 4
            ? config["EmailSettings:Password"]?.Substring(0, 4) + "***"
            : "NULL",
        FromEmail = config["EmailSettings:FromEmail"],
        FromName = config["EmailSettings:FromName"]
    });
});

// ✅ Direkt SMTP test
app.MapGet("/test-email", async (IConfiguration config) =>
{
    try
    {
        using var client = new SmtpClient(
            config["EmailSettings:SmtpHost"],
            int.Parse(config["EmailSettings:SmtpPort"])
        )
        {
            EnableSsl = bool.Parse(config["EmailSettings:EnableSsl"]),
            Credentials = new NetworkCredential(
                config["EmailSettings:Username"],
                config["EmailSettings:Password"]
            ),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30000
        };

        var message = new MailMessage(
            from: config["EmailSettings:FromEmail"],
            to: "test@example.com",
            subject: "Test Email",
            body: "Bu bir test email'idir."
        );

        await client.SendMailAsync(message);

        return Results.Ok(new { Success = true, Message = "Email gönderildi!" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Success = false, Error = ex.Message, StackTrace = ex.StackTrace });
    }
});

// ✅ Tüm user'ları re-encrypt et
app.MapGet("/admin/re-encrypt-all-users", async (IServiceProvider serviceProvider) =>
{
    using var scope = serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<LibraryContext>();

    var users = await context.Users.Where(u => !u.IsDeleted).ToListAsync();

    foreach (var user in users)
    {
        user.UpdatedDate = DateTime.UtcNow;
    }

    await context.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "All users re-encrypted!",
        Count = users.Count
    });
});

// ✅ User 13 ve 14'ü düz text yap ve şifrele
app.MapGet("/admin/encrypt-users", async (IServiceProvider serviceProvider) =>
{
    using var scope = serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<LibraryContext>();

    var user13 = await context.Users.FindAsync(13);
    var user14 = await context.Users.FindAsync(14);

    if (user13 != null)
    {
        user13.Email = "user13@test.com";
        user13.FullName = "User 13";
        user13.TwoFactorSecretKey = null;
        user13.RefreshToken = null;
    }

    if (user14 != null)
    {
        user14.Email = "string5@gmail.com";
        user14.FullName = "Test User";
        user14.TwoFactorSecretKey = null;
        user14.RefreshToken = null;
    }

    await context.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "Users encrypted!",
        User13Email = user13?.Email,
        User14Email = user14?.Email
    });
});

try
{
    Serilog.Log.Information("🚀 Uygulama başlatılıyor...");
    Serilog.Log.Information("📊 Hangfire Dashboard: {HangfireUrl}",
        $"{(app.Environment.IsDevelopment() ? "https://localhost:7229" : "")}/hangfire");
    app.Run();
}
catch (Exception ex)
{
    Serilog.Log.Fatal(ex, "❌ Uygulama başlatılamadı!");
}
finally
{
    Serilog.Log.CloseAndFlush();
}
