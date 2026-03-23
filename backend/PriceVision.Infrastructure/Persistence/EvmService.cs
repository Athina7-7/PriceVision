using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;
using PriceVision.Domain.Entities;

namespace PriceVision.Infrastructure.Persistence;

public sealed class EvmService(IEvmRepository evmRepository, IPredictionRepository predictionRepository, IProjectRepository projectRepository) : IEvmService
{
    private const decimal LaborHourRateCop = 38_000m;

    public async Task<EvmCalculationResponse> CalculateAndStoreAsync(Guid projectId, DateTime? periodDateUtc = null, CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("ProjectId es obligatorio.", nameof(projectId));
        }

        var records = await predictionRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (records.Count == 0)
        {
            throw new InvalidOperationException("No hay predicciones para este proyecto. Crea predicciones antes de calcular EVM.");
        }

        var asOf = (periodDateUtc ?? DateTime.UtcNow).Date;
        var relevant = records.Where(x => x.CreatedAtUtc.Date <= asOf).ToList();
        if (relevant.Count == 0)
        {
            throw new InvalidOperationException("No hay predicciones con fecha igual o anterior al periodo solicitado.");
        }

        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException("No se encontro el proyecto.");
        }

        var baseline = records.First();
        var predictedTotalCost = baseline.EstimatedMaterialCostCop + ((decimal)baseline.RequiredLaborHours * LaborHourRateCop);
        var baselineBudget = Math.Max(project.BaseCostCop, predictedTotalCost);
        var totalPlannedDays = Math.Max(1, baseline.DurationDays);
        var startDate = records.First().CreatedAtUtc.Date;

        var expectedSnapshots = Math.Max(1m, Math.Ceiling(totalPlannedDays / 7m));

        var elapsedDays = (asOf - startDate).TotalDays;
        var rawPlannedProgress = Clamp((decimal)(elapsedDays / totalPlannedDays), 0m, 1m);
        var minimumProgress = Math.Min(1m, 1m / expectedSnapshots);
        var plannedProgress = Math.Max(rawPlannedProgress, minimumProgress);
        var pv = Round2(baselineBudget * plannedProgress);

        var ac = Round2(relevant.Sum(x => x.EstimatedMaterialCostCop + ((decimal)x.RequiredLaborHours * LaborHourRateCop)));

        var actualProgress = Clamp(relevant.Count / expectedSnapshots, 0m, 1m);
        var ev = Round2(predictedTotalCost * actualProgress);

        var cpi = ac > 0m ? Round6(ev / ac) : 0m;
        var spi = pv > 0m ? Round6(ev / pv) : 0m;

        var costInterpretation = cpi < 1m ? "Sobre presupuesto" : "En o bajo presupuesto";
        var scheduleInterpretation = spi < 1m ? "Retrasado" : "En tiempo o adelantado";

        var evmRecord = new EvmRecord
        {
            ProjectId = projectId,
            PeriodDateUtc = asOf,
            PV = pv,
            EV = ev,
            AC = ac,
            CPI = cpi,
            SPI = spi,
            CostInterpretation = costInterpretation,
            ScheduleInterpretation = scheduleInterpretation,
            CreatedAtUtc = DateTime.UtcNow
        };

        var saved = await evmRepository.AddAsync(evmRecord, cancellationToken);

        return new EvmCalculationResponse(
            RecordId: saved.Id,
            ProjectId: saved.ProjectId,
            PeriodDateUtc: saved.PeriodDateUtc,
            PV: saved.PV,
            EV: saved.EV,
            AC: saved.AC,
            CPI: saved.CPI,
            SPI: saved.SPI,
            CostInterpretation: saved.CostInterpretation,
            ScheduleInterpretation: saved.ScheduleInterpretation);
    }

    public async Task<IReadOnlyList<EvmHistoryPoint>> GetHistoryAsync(Guid projectId, int take = 20, CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("ProjectId es obligatorio.", nameof(projectId));
        }

        var limit = Math.Clamp(take, 1, 120);
        var history = await evmRepository.GetByProjectIdAsync(projectId, limit, cancellationToken);

        return history.Select(x => new EvmHistoryPoint(
            PeriodDateUtc: x.PeriodDateUtc,
            PV: x.PV,
            EV: x.EV,
            AC: x.AC,
            CPI: x.CPI,
            SPI: x.SPI,
            CostInterpretation: x.CostInterpretation,
            ScheduleInterpretation: x.ScheduleInterpretation)).ToList();
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) => value < min ? min : value > max ? max : value;
    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal Round6(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
