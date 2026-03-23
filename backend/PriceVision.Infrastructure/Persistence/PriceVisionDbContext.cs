using Microsoft.EntityFrameworkCore;
using PriceVision.Domain.Entities;

namespace PriceVision.Infrastructure.Persistence;

public sealed class PriceVisionDbContext(DbContextOptions<PriceVisionDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<EvmRecord> EvmRecords => Set<EvmRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(100).IsRequired();
            entity.Property(x => x.BaseCostCop).HasPrecision(18, 2);
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ModelVersion).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EstimatedMaterialCostCop).HasPrecision(18, 2);
            entity.HasIndex(x => x.ProjectId);
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<EvmRecord>(entity =>
        {
            entity.ToTable("EVM_Records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PV).HasPrecision(18, 2);
            entity.Property(x => x.EV).HasPrecision(18, 2);
            entity.Property(x => x.AC).HasPrecision(18, 2);
            entity.Property(x => x.CPI).HasPrecision(18, 6);
            entity.Property(x => x.SPI).HasPrecision(18, 6);
            entity.Property(x => x.CostInterpretation).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ScheduleInterpretation).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.ProjectId);
            entity.HasIndex(x => x.PeriodDateUtc);
        });
    }
}
