using PriceVision.Application.Contracts;

namespace PriceVision.Application.Abstractions;

public interface IProjectValidationService
{
    Task<IReadOnlyList<ProjectValidationWarningResponse>> ValidateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);
}
