using Microsoft.EntityFrameworkCore;
using PriceVision.Domain.Entities;

namespace PriceVision.Infrastructure.Persistence;

public sealed class PriceVisionDbContext(DbContextOptions<PriceVisionDbContext> options) : DbContext(options)
{
    public DbSet<Prediction> Predictions => Set<Prediction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ModelVersion).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EstimatedMaterialCostCop).HasPrecision(18, 2);
        });
    }
}
