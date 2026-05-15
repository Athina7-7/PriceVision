using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;

namespace PriceVision.Infrastructure.Forecasting;

public sealed class FinancialSimulationService(
    IProjectRepository projectRepository,
    IFinancialPredictionRepository financialPredictionRepository) : IFinancialSimulationService
{
    public async Task<SimulationResult> SimulateAsync(Guid projectId, SimulationRequest request, CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("ProjectId es obligatorio.", nameof(projectId));
        }

        if (request.SimulatedDurationMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.SimulatedDurationMonths), "La duracion simulada debe ser mayor que cero.");
        }

        if (request.SimulatedBaseCostCop < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.SimulatedBaseCostCop), "El costo base simulado debe ser mayor o igual que cero.");
        }

        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException("No se encontro el proyecto.");
        }

        var financialPrediction = await financialPredictionRepository.GetByProjectIdAsync(projectId, cancellationToken);
        var originalEstimatedTotal = financialPrediction?.EstimatedTotalCostCop ?? project.BaseCostCop;

        var baseCostRatio = project.BaseCostCop > 0m
            ? request.SimulatedBaseCostCop / project.BaseCostCop
            : 1m;
        var durationRatio = project.DurationMonths > 0f
            ? (decimal)(request.SimulatedDurationMonths / project.DurationMonths)
            : 1m;

        var simulatedEstimatedTotal = Round2(originalEstimatedTotal * ((baseCostRatio * 0.75m) + (durationRatio * 0.25m)));
        var metrics = new List<SimulationMetricComparison>
        {
            BuildMetric("Duracion (meses)", (decimal)project.DurationMonths, (decimal)request.SimulatedDurationMonths),
            BuildMetric("Costo base (COP)", project.BaseCostCop, request.SimulatedBaseCostCop),
            BuildMetric("Costo estimado total (COP)", originalEstimatedTotal, simulatedEstimatedTotal)
        };

        if (financialPrediction is not null)
        {
            var simulatedMinimum = Round2(financialPrediction.MinimumEstimatedCostCop * ((baseCostRatio * 0.75m) + (durationRatio * 0.25m)));
            var simulatedMaximum = Round2(financialPrediction.MaximumEstimatedCostCop * ((baseCostRatio * 0.75m) + (durationRatio * 0.25m)));
            metrics.Add(BuildMetric("Rango minimo financiero (COP)", financialPrediction.MinimumEstimatedCostCop, simulatedMinimum));
            metrics.Add(BuildMetric("Rango maximo financiero (COP)", financialPrediction.MaximumEstimatedCostCop, simulatedMaximum));
        }

        var totalDifference = Round2(simulatedEstimatedTotal - originalEstimatedTotal);

        return new SimulationResult(
            ProjectId: project.Id,
            ProjectName: project.Name,
            SimulatedAtUtc: DateTime.UtcNow,
            Metrics: metrics,
            OriginalEstimatedTotalCostCop: originalEstimatedTotal,
            SimulatedEstimatedTotalCostCop: simulatedEstimatedTotal,
            EstimatedTotalCostDifferenceCop: totalDifference,
            EstimatedTotalCostPercentageDifference: PercentageDifference(originalEstimatedTotal, simulatedEstimatedTotal));
    }

    private static SimulationMetricComparison BuildMetric(string label, decimal originalValue, decimal simulatedValue)
    {
        var roundedOriginal = Round2(originalValue);
        var roundedSimulated = Round2(simulatedValue);

        return new SimulationMetricComparison(
            Label: label,
            OriginalValue: roundedOriginal,
            SimulatedValue: roundedSimulated,
            AbsoluteDifference: Round2(roundedSimulated - roundedOriginal),
            PercentageDifference: PercentageDifference(roundedOriginal, roundedSimulated));
    }

    private static decimal PercentageDifference(decimal originalValue, decimal simulatedValue)
    {
        if (originalValue == 0m)
        {
            return simulatedValue == 0m ? 0m : 100m;
        }

        return Round2(((simulatedValue - originalValue) / originalValue) * 100m);
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
