using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;
using PriceVision.Domain.Entities;

namespace PriceVision.Infrastructure.Forecasting;

public sealed class FinancialForecastService(
    IProjectRepository projectRepository,
    IPredictionRepository predictionRepository,
    IFinancialPredictionRepository financialPredictionRepository) : IFinancialForecastService
{
    private const decimal LaborHourRateCop = 38_000m;

    private static readonly Dictionary<string, decimal> LocationTrendFactor = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bogota"] = 1.10m,
        ["Medellin"] = 1.06m,
        ["Cali"] = 1.03m,
        ["Barranquilla"] = 1.02m,
        ["Rural"] = 0.97m
    };

    public async Task<FinancialPredictionResponse> CreateForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException("No se encontro el proyecto.");
        }

        if (await financialPredictionRepository.ExistsForProjectAsync(projectId, cancellationToken))
        {
            throw new InvalidOperationException("Este proyecto ya tiene una prediccion financiera registrada.");
        }

        var resourcePredictions = await predictionRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (resourcePredictions.Count == 0)
        {
            throw new InvalidOperationException("Este proyecto necesita predicciones de recursos antes de generar la prediccion financiera.");
        }

        var latestMaterials = resourcePredictions.LastOrDefault(x => x.PredictedMaterials);
        var latestLabor = resourcePredictions.LastOrDefault(x => x.PredictedLabor);
        if (latestMaterials is null || latestLabor is null)
        {
            throw new InvalidOperationException("Este proyecto necesita predicciones de materiales y mano de obra antes de generar la prediccion financiera.");
        }

        var allProjects = await projectRepository.GetAllAsync(cancellationToken);
        var comparableProjects = allProjects
            .Where(x => x.AreaM2 > 0)
            .Where(x => string.Equals(x.Type, project.Type, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (comparableProjects.Count < 3)
        {
            comparableProjects = allProjects.Where(x => x.AreaM2 > 0).ToList();
        }

        var historicalAverageCostPerM2 = comparableProjects.Count > 0
            ? comparableProjects.Average(x => x.BaseCostCop / (decimal)x.AreaM2)
            : project.BaseCostCop / Math.Max(1m, (decimal)project.AreaM2);

        var locationTrend = LocationTrendFactor.GetValueOrDefault(project.Location, 1.0m);
        var resourcesCost = latestMaterials.EstimatedMaterialCostCop + ((decimal)latestLabor.RequiredLaborHours * LaborHourRateCop);
        var historicalReferenceCost = (decimal)project.AreaM2 * historicalAverageCostPerM2 * locationTrend;
        var weightedTotal = decimal.Round((resourcesCost * 0.65m) + (historicalReferenceCost * 0.35m), 2);

        var dispersion = comparableProjects.Count > 1
            ? CalculateCoefficientOfVariation(comparableProjects.Select(x => (double)(x.BaseCostCop / Math.Max(1m, (decimal)x.AreaM2))).ToList())
            : 0.18d;

        var confidencePercentage = CalculateConfidencePercentage(comparableProjects.Count, dispersion);
        var confidenceLevel = confidencePercentage switch
        {
            >= 80f => "Alto",
            >= 60f => "Medio",
            _ => "Bajo"
        };

        var rangeFactor = confidencePercentage switch
        {
            >= 80f => 0.08m,
            >= 60f => 0.15m,
            _ => 0.25m
        };

        var minimum = decimal.Round(weightedTotal * (1m - rangeFactor), 2);
        var maximum = decimal.Round(weightedTotal * (1m + rangeFactor), 2);

        var entity = new FinancialPrediction
        {
            ProjectId = projectId,
            EstimatedTotalCostCop = weightedTotal,
            MinimumEstimatedCostCop = minimum,
            MaximumEstimatedCostCop = maximum,
            ConfidencePercentage = confidencePercentage,
            ConfidenceLevel = confidenceLevel,
            HistoricalAverageCostPerM2Cop = decimal.Round(historicalAverageCostPerM2, 2),
            LocationTrendFactor = locationTrend,
            CreatedAtUtc = DateTime.UtcNow
        };

        await financialPredictionRepository.AddAsync(entity, cancellationToken);

        return new FinancialPredictionResponse(
            FinancialPredictionId: entity.Id,
            ProjectId: project.Id,
            ProjectName: project.Name,
            AreaM2: project.AreaM2,
            Type: project.Type,
            Location: project.Location,
            DurationMonths: project.DurationMonths,
            BaseCostCop: project.BaseCostCop,
            EstimatedTotalCostCop: entity.EstimatedTotalCostCop,
            MinimumEstimatedCostCop: entity.MinimumEstimatedCostCop,
            MaximumEstimatedCostCop: entity.MaximumEstimatedCostCop,
            ConfidencePercentage: entity.ConfidencePercentage,
            ConfidenceLevel: entity.ConfidenceLevel,
            HistoricalAverageCostPerM2Cop: entity.HistoricalAverageCostPerM2Cop,
            LocationTrendFactor: entity.LocationTrendFactor,
            CreatedAtUtc: entity.CreatedAtUtc);
    }

    private static float CalculateConfidencePercentage(int sampleCount, double coefficientOfVariation)
    {
        var sampleFactor = Math.Min(1d, sampleCount / 12d);
        var variabilityPenalty = Math.Min(0.55d, coefficientOfVariation);
        var confidence = 0.45d + (sampleFactor * 0.4d) - (variabilityPenalty * 0.35d);
        return (float)Math.Clamp(confidence * 100d, 35d, 95d);
    }

    private static double CalculateCoefficientOfVariation(IReadOnlyList<double> samples)
    {
        if (samples.Count <= 1)
        {
            return 0.18d;
        }

        var mean = samples.Average();
        if (mean <= 0d)
        {
            return 0.18d;
        }

        var variance = samples.Sum(x => Math.Pow(x - mean, 2)) / samples.Count;
        var std = Math.Sqrt(variance);
        return std / mean;
    }
}
