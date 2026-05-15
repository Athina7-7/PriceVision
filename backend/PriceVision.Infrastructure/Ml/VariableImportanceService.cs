using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;

namespace PriceVision.Infrastructure.Ml;

public sealed class VariableImportanceService : IVariableImportanceService
{
    private static readonly IReadOnlyList<VariableCoefficient> CostCoefficients =
    [
        new("AreaM2", "Area construida", 1.00m, "Mayor area aumenta el costo estimado."),
        new("DurationDays", "Duracion", 0.18m, "Mayor duracion aumenta el costo estimado por recursos sostenidos en el tiempo."),
        new("TypeMultiplier", "Tipo de proyecto", 0.40m, "Proyectos mas complejos tienden a aumentar materiales y mano de obra."),
        new("LocationMultiplier", "Ubicacion", 0.20m, "Ubicaciones con mayor multiplicador incrementan el costo estimado.")
    ];

    public IReadOnlyList<VariableImportanceResponse> GetCostVariableImportance()
    {
        var total = CostCoefficients.Sum(item => Math.Abs(item.Coefficient));
        if (total == 0m)
        {
            return CostCoefficients
                .Select((item, index) => BuildResponse(item, 0m, index + 1))
                .ToList();
        }

        return CostCoefficients
            .OrderByDescending(item => Math.Abs(item.Coefficient))
            .Select((item, index) => BuildResponse(item, total, index + 1))
            .ToList();
    }

    private static VariableImportanceResponse BuildResponse(VariableCoefficient item, decimal totalAbsoluteCoefficient, int rank)
    {
        var absoluteCoefficient = Math.Abs(item.Coefficient);
        var percentage = totalAbsoluteCoefficient == 0m
            ? 0m
            : decimal.Round((absoluteCoefficient / totalAbsoluteCoefficient) * 100m, 2, MidpointRounding.AwayFromZero);

        return new VariableImportanceResponse(
            TechnicalName: item.TechnicalName,
            DisplayName: item.DisplayName,
            Coefficient: item.Coefficient,
            AbsoluteCoefficient: absoluteCoefficient,
            ImportancePercentage: percentage,
            Rank: rank,
            Direction: item.Coefficient >= 0m ? "Positiva" : "Negativa",
            Interpretation: item.Interpretation);
    }

    private sealed record VariableCoefficient(string TechnicalName, string DisplayName, decimal Coefficient, string Interpretation);
}
