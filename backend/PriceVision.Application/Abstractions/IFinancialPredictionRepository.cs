using PriceVision.Domain.Entities;

namespace PriceVision.Application.Abstractions;

public interface IFinancialPredictionRepository
{
    Task<FinancialPrediction> AddAsync(FinancialPrediction prediction, CancellationToken cancellationToken = default);
    Task<FinancialPrediction?> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialPrediction>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
    Task<bool> ExistsForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
