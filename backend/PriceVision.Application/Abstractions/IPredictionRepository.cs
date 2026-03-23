using PriceVision.Domain.Entities;

namespace PriceVision.Application.Abstractions;

public interface IPredictionRepository
{
    Task<Prediction> AddAsync(Prediction prediction, CancellationToken cancellationToken = default);
    Task<Prediction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Prediction>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Prediction>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> ExistsForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> ExistsForProjectAsync(Guid projectId, bool predictedMaterials, bool predictedLabor, CancellationToken cancellationToken = default);
}
