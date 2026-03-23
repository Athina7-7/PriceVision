using Microsoft.EntityFrameworkCore;
using PriceVision.Application.Abstractions;
using PriceVision.Domain.Entities;

namespace PriceVision.Infrastructure.Persistence;

public sealed class PredictionRepository(PriceVisionDbContext dbContext) : IPredictionRepository
{
    public async Task<Prediction> AddAsync(Prediction prediction, CancellationToken cancellationToken = default)
    {
        dbContext.Predictions.Add(prediction);
        await dbContext.SaveChangesAsync(cancellationToken);
        return prediction;
    }

    public Task<Prediction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Predictions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Prediction>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await dbContext.Predictions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Prediction>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Predictions
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return dbContext.Predictions.AsNoTracking().AnyAsync(x => x.ProjectId == projectId, cancellationToken);
    }

    public Task<bool> ExistsForProjectAsync(Guid projectId, bool predictedMaterials, bool predictedLabor, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Predictions.AsNoTracking().Where(x => x.ProjectId == projectId);

        if (predictedMaterials)
        {
            query = query.Where(x => x.PredictedMaterials);
        }

        if (predictedLabor)
        {
            query = query.Where(x => x.PredictedLabor);
        }

        return query.AnyAsync(cancellationToken);
    }
}
