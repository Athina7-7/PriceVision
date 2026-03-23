using PriceVision.Domain.Entities;

namespace PriceVision.Application.Abstractions;

public interface IEvmRepository
{
    Task<EvmRecord> AddAsync(EvmRecord record, CancellationToken cancellationToken = default);
    Task<EvmRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvmRecord>> GetByProjectIdAsync(Guid projectId, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvmRecord>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
    Task<bool> ExistsForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
