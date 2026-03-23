using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;

namespace PriceVision.Infrastructure.Validation;

public sealed class ProjectValidationService(IProjectRepository projectRepository) : IProjectValidationService
{
    public async Task<IReadOnlyList<ProjectValidationWarningResponse>> ValidateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var history = await projectRepository.GetAllAsync(cancellationToken);
        if (history.Count == 0 || request.AreaM2 <= 0)
        {
            return [];
        }

        var warnings = new List<ProjectValidationWarningResponse>();
        warnings.AddRange(ValidateBaseCostPerSquareMeter(request, history));
        warnings.AddRange(ValidateDurationByType(request, history));

        return warnings;
    }

    private static IEnumerable<ProjectValidationWarningResponse> ValidateBaseCostPerSquareMeter(CreateProjectRequest request, IReadOnlyList<PriceVision.Domain.Entities.Project> history)
    {
        var comparable = history
            .Where(x => x.AreaM2 > 0)
            .Where(x => string.Equals(x.Type, request.Type, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (comparable.Count < 3)
        {
            comparable = history.Where(x => x.AreaM2 > 0).ToList();
        }

        if (comparable.Count < 3)
        {
            yield break;
        }

        var current = (double)request.BaseCostCop / request.AreaM2;
        var samples = comparable.Select(x => (double)x.BaseCostCop / x.AreaM2).ToList();
        var mean = samples.Average();
        var std = CalculateStd(samples, mean);

        var lower = std > 0 ? Math.Max(0d, mean - (2 * std)) : mean * 0.65d;
        var upper = std > 0 ? mean + (2 * std) : mean * 1.35d;

        if (current < lower || current > upper)
        {
            yield return new ProjectValidationWarningResponse(
                Code: "base_cost_per_m2_outlier",
                Title: "Costo base por m2 fuera del comportamiento historico",
                Message: $"El costo base por m2 ingresado ({current:N0} COP/m2) se sale del rango historico esperado ({lower:N0} - {upper:N0} COP/m2).");
        }
    }

    private static IEnumerable<ProjectValidationWarningResponse> ValidateDurationByType(CreateProjectRequest request, IReadOnlyList<PriceVision.Domain.Entities.Project> history)
    {
        var comparable = history
            .Where(x => string.Equals(x.Type, request.Type, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (comparable.Count < 3)
        {
            yield break;
        }

        var samples = comparable.Select(x => (double)x.DurationMonths).ToList();
        var mean = samples.Average();
        var std = CalculateStd(samples, mean);

        var lower = std > 0 ? Math.Max(0d, mean - (2 * std)) : mean * 0.65d;
        var upper = std > 0 ? mean + (2 * std) : mean * 1.35d;
        var current = request.DurationMonths;

        if (current < lower || current > upper)
        {
            yield return new ProjectValidationWarningResponse(
                Code: "duration_inconsistent_for_type",
                Title: "Duracion inconsistente para el tipo de proyecto",
                Message: $"La duracion ingresada ({current:N1} meses) se sale del rango historico para proyectos tipo {request.Type} ({lower:N1} - {upper:N1} meses).");
        }
    }

    private static double CalculateStd(IReadOnlyList<double> samples, double mean)
    {
        if (samples.Count <= 1)
        {
            return 0d;
        }

        var variance = samples.Sum(x => Math.Pow(x - mean, 2)) / samples.Count;
        return Math.Sqrt(variance);
    }
}
