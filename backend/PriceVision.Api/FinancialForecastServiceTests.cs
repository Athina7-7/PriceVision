using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using PriceVision.Application.Services;
using PriceVision.Domain.Entities;
using PriceVision.Infrastructure.Persistence;

namespace PriceVision.Tests;

public class FinancialForecastServiceTests
{
    private PriceVisionDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PriceVisionDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PriceVisionDbContext(options);
    }

    [Fact]
    public async Task CreateForProjectAsync_WithValidData_ReturnsAccurateRegressionAndConfidence()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        var projectId = Guid.NewGuid();
        
        dbContext.Set<Project>().Add(new Project { Id = projectId, AreaM2 = 100, Location = "Bogota", BaseCostCop = 0 });
        dbContext.Set<Project>().Add(new Project { Id = Guid.NewGuid(), AreaM2 = 50, Location = "Bogota", BaseCostCop = 5_000_000 });
        dbContext.Set<Project>().Add(new Project { Id = Guid.NewGuid(), AreaM2 = 150, Location = "Bogota", BaseCostCop = 15_000_000 });
        dbContext.Set<Project>().Add(new Project { Id = Guid.NewGuid(), AreaM2 = 200, Location = "Cali", BaseCostCop = 18_000_000 });
        
        dbContext.Set<Prediction>().Add(new Prediction { ProjectId = projectId, PredictedMaterials = true, EstimatedMaterialCostCop = 2_000_000, CreatedAtUtc = DateTime.UtcNow });
        dbContext.Set<Prediction>().Add(new Prediction { ProjectId = projectId, PredictedLabor = true, RequiredLaborHours = 50, CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var service = new FinancialForecastService(dbContext);

        // Act
        var result = await service.CreateForProjectAsync(projectId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        
        // Matemáticamente, el factor Bogotá debe ser ~ 100K/m2. Un proyecto de 100m2 debería proyectar 10 Millones (+/- factor Location).
        Assert.True(result.EstimatedTotalCostCop >= 9_000_000m);
        
        // Validar integridad estructural
        Assert.True(result.MinimumEstimatedCostCop <= result.EstimatedTotalCostCop);
        Assert.True(result.MaximumEstimatedCostCop >= result.EstimatedTotalCostCop);
        Assert.InRange(result.ConfidencePercentage, 0, 100);
        Assert.Equal("LinearRegression", result.ModelType);
    }

    [Fact]
    public async Task CreateForProjectAsync_WithoutPriorPredictions_ThrowsException()
    {
        var dbContext = GetInMemoryDbContext();
        var projectId = Guid.NewGuid();
        dbContext.Set<Project>().Add(new Project { Id = projectId });
        await dbContext.SaveChangesAsync();
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => new FinancialForecastService(dbContext).CreateForProjectAsync(projectId, CancellationToken.None));
    }
}