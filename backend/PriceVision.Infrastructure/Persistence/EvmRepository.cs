using Microsoft.EntityFrameworkCore;
using PriceVision.Application.Abstractions;
using PriceVision.Domain.Entities;

namespace PriceVision.Infrastructure.Persistence;

public sealed class EvmRepository(PriceVisionDbContext dbContext) : IEvmRepository
{
    public async Task<EvmRecord> AddAsync(EvmRecord record, CancellationToken cancellationToken = default)
    {
        dbContext.EvmRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return record;
    }

    public Task<EvmRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.EvmRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EvmRecord>> GetByProjectIdAsync(Guid projectId, int take, CancellationToken cancellationToken = default)
    {
        return await dbContext.EvmRecords
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.PeriodDateUtc)
            .Take(take)
            .OrderBy(x => x.PeriodDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EvmRecord>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        return await dbContext.EvmRecords
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return dbContext.EvmRecords.AsNoTracking().AnyAsync(x => x.ProjectId == projectId, cancellationToken);
    }
}
