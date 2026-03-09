using PriceVision.Domain.Entities;

namespace PriceVision.Application.Abstractions;

public interface IPredictionRepository
{
    Task<Prediction> AddAsync(Prediction prediction, CancellationToken cancellationToken = default);
    Task<Prediction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Prediction>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}
