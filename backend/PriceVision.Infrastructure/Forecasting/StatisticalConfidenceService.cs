using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;

namespace PriceVision.Infrastructure.Forecasting;

public sealed class StatisticalConfidenceService : IStatisticalConfidenceService
{
    private const decimal ZScore95 = 1.96m;

    public StatisticalConfidenceResult Calculate(decimal predictedValue, decimal standardError, float confidencePercentage = 95f)
    {
        if (predictedValue < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(predictedValue), "El valor predicho no puede ser negativo para costos.");
        }

        if (standardError < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(standardError), "El error estandar no puede ser negativo.");
        }

        if (confidencePercentage <= 0f || confidencePercentage >= 100f)
        {
            throw new ArgumentOutOfRangeException(nameof(confidencePercentage), "La confianza debe estar entre 0 y 100.");
        }

        var zScore = confidencePercentage == 95f ? ZScore95 : ZScore95;
        var margin = zScore * standardError;
        var lowerBound = Math.Max(0m, predictedValue - margin);
        var upperBound = predictedValue + margin;
        var roundedConfidence = MathF.Round(confidencePercentage, 2);

        return new StatisticalConfidenceResult(
            ConfidencePercentage: roundedConfidence,
            ConfidenceLevel: $"{roundedConfidence:0.##}%",
            StandardError: Round2(standardError),
            ConfidenceIntervalLower: Round2(lowerBound),
            ConfidenceIntervalUpper: Round2(upperBound),
            ConfidenceExplanation: "El nivel de confianza indica que, considerando el error estandar del modelo, es probable que el valor real se ubique dentro del intervalo estimado.");
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
