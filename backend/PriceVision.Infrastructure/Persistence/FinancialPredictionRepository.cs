using Microsoft.EntityFrameworkCore;
using PriceVision.Application.Abstractions;
using PriceVision.Domain.Entities;

namespace PriceVision.Infrastructure.Persistence;

public sealed class FinancialPredictionRepository(PriceVisionDbContext dbContext) : IFinancialPredictionRepository
{
    public async Task<FinancialPrediction> AddAsync(FinancialPrediction prediction, CancellationToken cancellationToken = default)
    {
        dbContext.FinancialPredictions.Add(prediction);
        await dbContext.SaveChangesAsync(cancellationToken);
        return prediction;
    }

    public Task<FinancialPrediction?> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return dbContext.FinancialPredictions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialPrediction>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        return await dbContext.FinancialPredictions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return dbContext.FinancialPredictions.AsNoTracking().AnyAsync(x => x.ProjectId == projectId, cancellationToken);
    }
}
