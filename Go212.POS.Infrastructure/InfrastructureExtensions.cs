using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Interfaces;
using Go212.POS.Infrastructure.Backup;
using Go212.POS.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Go212.POS.Infrastructure;

/// <summary>Registers all infrastructure services into the DI container.</summary>
public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Database
        services.AddSingleton<DbConnectionFactory>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Backup service
        services.AddScoped<IBackupService, BackupService>();

        return services;
    }
}
