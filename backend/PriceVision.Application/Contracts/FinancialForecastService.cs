using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;
using PriceVision.Domain.Entities;
using PriceVision.Infrastructure.Persistence;

namespace PriceVision.Application.Services;

public sealed class FinancialForecastService : IFinancialForecastService
{
    private readonly PriceVisionDbContext _dbContext;

    public FinancialForecastService(PriceVisionDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FinancialPredictionResponse> CreateForProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _dbContext.Set<Project>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
            throw new InvalidOperationException("Proyecto no encontrado.");

        var predictions = await _dbContext.Set<Prediction>()
            .AsNoTracking()
            .Where(p => p.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var latestMaterials = predictions.Where(p => p.PredictedMaterials).OrderByDescending(p => p.CreatedAtUtc).FirstOrDefault();
        var latestLabor = predictions.Where(p => p.PredictedLabor).OrderByDescending(p => p.CreatedAtUtc).FirstOrDefault();

        if (latestMaterials == null || latestLabor == null)
            throw new InvalidOperationException("El proyecto requiere predicciones previas de materiales y mano de obra para calcular la viabilidad.");

        decimal materialCost = latestMaterials.EstimatedMaterialCostCop;
        decimal laborCost = (decimal)latestLabor.RequiredLaborHours * 38000m; // Tarifa estándar
        decimal directCost = materialCost + laborCost;

        var historicalProjects = await _dbContext.Set<Project>()
            .AsNoTracking()
            .Where(p => p.Id != projectId && p.BaseCostCop > 0)
            .ToListAsync(cancellationToken);

        // Factor de tendencia por ubicación
        decimal globalAvgM2 = historicalProjects.Count > 0 ? historicalProjects.Average(p => p.BaseCostCop / (decimal)Math.Max(p.AreaM2, 1)) : 0m;
        var locationProjects = historicalProjects.Where(p => p.Location == project.Location).ToList();
        decimal locAvgM2 = locationProjects.Count > 0 ? locationProjects.Average(p => p.BaseCostCop / (decimal)Math.Max(p.AreaM2, 1)) : globalAvgM2;

        decimal locationTrend = globalAvgM2 > 0 ? locAvgM2 / globalAvgM2 : 1.0m;
        if (locationTrend <= 0) locationTrend = 1.0m;

        decimal estimatedTotalCost;
        decimal standardError;

        // Regresión Lineal Múltiple simplificada a Simple ponderada (X = Area * TendenciaGeografica, Y = Costo Base)
        if (historicalProjects.Count >= 3)
        {
            var xValues = historicalProjects.Select(p => (double)((decimal)p.AreaM2 * locationTrend)).ToList();
            var yValues = historicalProjects.Select(p => (double)p.BaseCostCop).ToList();
            double xAvg = xValues.Average();
            double yAvg = yValues.Average();
            double sumX2 = xValues.Sum(x => Math.Pow(x - xAvg, 2));
            double sumXY = xValues.Zip(yValues, (x, y) => (x - xAvg) * (y - yAvg)).Sum();

            double beta = sumX2 == 0 ? 0 : sumXY / sumX2;
            double alpha = yAvg - beta * xAvg;
            double xCurrent = (double)((decimal)project.AreaM2 * locationTrend);
            
            estimatedTotalCost = Math.Max(directCost * 1.05m, (decimal)(alpha + beta * xCurrent)); // Minimum threshold safeguard
            double sumResiduals2 = xValues.Zip(yValues, (x, y) => Math.Pow(y - (alpha + beta * x), 2)).Sum();
            standardError = (decimal)Math.Sqrt(sumResiduals2 / Math.Max(1, historicalProjects.Count - 2));
        }
        else
        {
            // Fallback heurístico en caso de faltar data histórica
            estimatedTotalCost = directCost * 1.15m * locationTrend;
            standardError = estimatedTotalCost * 0.10m;
        }

        decimal zScore = 1.96m; // 95% Intervalo de Confianza
        decimal marginOfError = zScore * standardError;
        decimal minCost = Math.Max(directCost, estimatedTotalCost - marginOfError);
        decimal maxCost = estimatedTotalCost + marginOfError;
        decimal cv = estimatedTotalCost > 0 ? standardError / estimatedTotalCost : 0;
        decimal confidencePercent = Math.Clamp(100m - (cv * 100m), 0m, 99.9m);

        var prediction = new FinancialPrediction
        {
            Id = Guid.NewGuid(), ProjectId = projectId, EstimatedTotalCostCop = estimatedTotalCost,
            MinimumEstimatedCostCop = minCost, MaximumEstimatedCostCop = maxCost, ConfidencePercentage = (float)confidencePercent,
            ConfidenceLevel = confidencePercent >= 85 ? "Alta" : confidencePercent >= 70 ? "Media" : "Baja",
            StandardError = standardError, ConfidenceIntervalLower = minCost, ConfidenceIntervalUpper = maxCost,
            ConfidenceExplanation = $"Basado en {historicalProjects.Count} proyectos usando regresión. Confianza del {confidencePercent:N1}%.",
            HistoricalAverageCostPerM2Cop = locAvgM2, LocationTrendFactor = locationTrend, ModelType = "LinearRegression",
            ModelVersion = "1.0", CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Set<FinancialPrediction>().Add(prediction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new FinancialPredictionResponse(prediction.Id, project.Id, project.Name, project.AreaM2, project.Type, project.Location,
            project.DurationMonths, project.BaseCostCop, prediction.EstimatedTotalCostCop, prediction.MinimumEstimatedCostCop,
            prediction.MaximumEstimatedCostCop, prediction.ConfidencePercentage, prediction.ConfidenceLevel, prediction.StandardError,
            prediction.ConfidenceIntervalLower, prediction.ConfidenceIntervalUpper, prediction.ConfidenceExplanation,
            prediction.HistoricalAverageCostPerM2Cop, prediction.LocationTrendFactor, prediction.ModelType, prediction.ModelVersion, prediction.CreatedAtUtc);
    }
}