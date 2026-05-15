using PriceVision.Application.Contracts;

namespace PriceVision.Application.Abstractions;

public interface IStatisticalConfidenceService
{
    StatisticalConfidenceResult Calculate(decimal predictedValue, decimal standardError, float confidencePercentage = 95f);
}
