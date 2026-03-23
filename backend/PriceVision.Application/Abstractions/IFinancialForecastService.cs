using PriceVision.Application.Contracts;

namespace PriceVision.Application.Abstractions;

public interface IFinancialForecastService
{
    Task<FinancialPredictionResponse> CreateForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
