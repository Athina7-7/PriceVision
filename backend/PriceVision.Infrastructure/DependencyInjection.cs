using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceVision.Application.Abstractions;
using PriceVision.Infrastructure.Ml;
using PriceVision.Infrastructure.Persistence;
using PriceVision.Infrastructure.Validation;

namespace PriceVision.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=pricevision.db";

        services.AddDbContext<PriceVisionDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectValidationService, ProjectValidationService>();
        services.AddScoped<IPredictionRepository, PredictionRepository>();
        services.AddScoped<IEvmRepository, EvmRepository>();
        services.AddScoped<IEvmService, EvmService>();
        services.AddSingleton<IModelTrainingService, ModelTrainingService>();
        services.AddSingleton<IPredictiveModelService, PredictiveModelService>();

        return services;
    }
}
