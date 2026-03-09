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
}
