using Go212.POS.Application.Interfaces;
using Go212.POS.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Go212.POS.Application;

/// <summary>Registers all application use-case services into the DI container.</summary>
public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICurrentUserService, CurrentUserService>();

        services.AddScoped<IAuthService,    AuthService>();
        services.AddScoped<ISaleService,    SaleService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IStockService,   StockService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IReportService,  ReportService>();
        services.AddScoped<IReturnService,  ReturnService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        return services;
    }
}
