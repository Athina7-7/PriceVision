using PriceVision.Application.Contracts;

namespace PriceVision.Application.Abstractions;

public interface IEvmService
{
    Task<EvmCalculationResponse> CalculateAndStoreAsync(Guid projectId, DateTime? periodDateUtc = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvmHistoryPoint>> GetHistoryAsync(Guid projectId, int take = 20, CancellationToken cancellationToken = default);
}
