using LibraryManagement.Application.Interfaces;
using LibraryManagement.Application.Interfaces.Repositories;
using LibraryManagement.Application.Interfaces.Services;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LibraryManagement.Application.Interfaces.UnitOfWork;
using StackExchange.Redis;
using Application.Interfaces.Services;
using Infrastructure.Interceptors;

namespace Infrastructure
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // DbContext
            // DbContext with Encryption Interceptor
            services.AddScoped<EncryptionInterceptor>();
            services.AddScoped<DecryptionInterceptor>();

            services.AddDbContext<Domain.Entities.LibraryContext>((serviceProvider, options) =>
            {
                var encryptionInterceptor = serviceProvider.GetRequiredService<EncryptionInterceptor>();
                var decryptionInterceptor = serviceProvider.GetRequiredService<DecryptionInterceptor>();

                options.UseSqlServer(configuration.GetConnectionString("LibraryDB"))
                       .AddInterceptors(encryptionInterceptor, decryptionInterceptor);  // ✅ Interceptor ekle
            });

            // Repositories
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IBookRepository, BookRepository>();
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // UnitOfWork
            services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

            // Services (YENİ)
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<ITwoFactorService, TwoFactorService>();
            services.AddScoped<IEncryptionService, AesEncryptionService>();
            // ✅ REDIS CACHE
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis")
                    ?? "localhost:6379";
                options.InstanceName = "LibraryManagement:";
            });

            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(
                    configuration.GetConnectionString("Redis") ?? "localhost:6379"));

            services.AddScoped<ICacheService, CacheService>();

            return services;
        }
    }
}