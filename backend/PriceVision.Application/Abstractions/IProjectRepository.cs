using PriceVision.Domain.Entities;

namespace PriceVision.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default);
}
