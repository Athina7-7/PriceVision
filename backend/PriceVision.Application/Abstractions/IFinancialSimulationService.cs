using PriceVision.Application.Contracts;

namespace PriceVision.Application.Abstractions;

public interface IFinancialSimulationService
{
    Task<SimulationResult> SimulateAsync(Guid projectId, SimulationRequest request, CancellationToken cancellationToken = default);
}
