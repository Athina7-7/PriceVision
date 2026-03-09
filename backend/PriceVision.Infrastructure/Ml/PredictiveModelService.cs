using Microsoft.Extensions.Hosting;
using Microsoft.ML;
using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;
using PriceVision.Domain.Entities;

namespace PriceVision.Infrastructure.Ml;

public sealed class PredictiveModelService(IHostEnvironment environment) : IPredictiveModelService
{
    private readonly string _materialsModelPath = Path.Combine(environment.ContentRootPath, "Artifacts", "materials-model.zip");
    private readonly string _laborModelPath = Path.Combine(environment.ContentRootPath, "Artifacts", "labor-model.zip");
    private readonly string _versionPath = Path.Combine(environment.ContentRootPath, "Artifacts", "model-version.txt");

    private readonly object _lock = new();
    private PredictionEngine<PredictionInputModel, RegressionPrediction>? _materialsPredictionEngine;
    private PredictionEngine<PredictionInputModel, RegressionPrediction>? _laborPredictionEngine;
    private DateTime _materialsLastWriteUtc;
    private DateTime _laborLastWriteUtc;

    private static readonly Dictionary<string, decimal> UnitCostByType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Residencial"] = 275_000m,
        ["Comercial"] = 340_000m,
        ["Industrial"] = 410_000m,
        ["Remodelacion"] = 225_000m
    };

    private static readonly Dictionary<string, decimal> CostMultiplierByLocation = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bogota"] = 1.15m,
        ["Medellin"] = 1.08m,
        ["Cali"] = 1.04m,
        ["Barranquilla"] = 1.02m,
        ["Rural"] = 0.95m
    };

    public PredictionResult Predict(PredictionRequest request)
    {
        if (!File.Exists(_materialsModelPath) || !File.Exists(_laborModelPath))
        {
            throw new InvalidOperationException("No hay modelos entrenados. Ejecuta /api/predictions/train primero.");
        }

        EnsureModelsLoaded();

        var normalizedDurationDays = NormalizeDurationToDays(request.Duration, request.DurationUnit);
        var input = new PredictionInputModel
        {
            AreaM2 = request.AreaM2,
            Type = request.Type,
            Location = request.Location,
            DurationDays = normalizedDurationDays
        };

        var quantity = Math.Max(0f, _materialsPredictionEngine!.Predict(input).Score);
        var laborHours = Math.Max(0f, _laborPredictionEngine!.Predict(input).Score);

        var unitCost = UnitCostByType.GetValueOrDefault(request.Type, 250_000m);
        var locationMultiplier = CostMultiplierByLocation.GetValueOrDefault(request.Location, 1.0m);
        var costCop = decimal.Round((decimal)quantity * unitCost * locationMultiplier, 2);

        return new PredictionResult(
            MaterialesEstimados: new MaterialsEstimate(quantity, costCop),
            ManoObraRequeridaHorasPersona: laborHours);
    }

    public Prediction BuildPredictionEntity(PredictionRequest request, PredictionResult result)
    {
        var durationDays = NormalizeDurationToDays(request.Duration, request.DurationUnit);

        return new Prediction
        {
            AreaM2 = request.AreaM2,
            Type = request.Type,
            Location = request.Location,
            DurationDays = (int)MathF.Round(durationDays),
            EstimatedMaterialQuantity = result.MaterialesEstimados.Quantity,
            EstimatedMaterialCostCop = result.MaterialesEstimados.CostCop,
            RequiredLaborHours = result.ManoObraRequeridaHorasPersona,
            ModelVersion = ReadModelVersion(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private void EnsureModelsLoaded()
    {
        var materialsWriteUtc = File.GetLastWriteTimeUtc(_materialsModelPath);
        var laborWriteUtc = File.GetLastWriteTimeUtc(_laborModelPath);

        if (_materialsPredictionEngine is not null &&
            _laborPredictionEngine is not null &&
            materialsWriteUtc <= _materialsLastWriteUtc &&
            laborWriteUtc <= _laborLastWriteUtc)
        {
            return;
        }

        lock (_lock)
        {
            materialsWriteUtc = File.GetLastWriteTimeUtc(_materialsModelPath);
            laborWriteUtc = File.GetLastWriteTimeUtc(_laborModelPath);

            if (_materialsPredictionEngine is not null &&
                _laborPredictionEngine is not null &&
                materialsWriteUtc <= _materialsLastWriteUtc &&
                laborWriteUtc <= _laborLastWriteUtc)
            {
                return;
            }

            var mlContext = new MLContext(seed: 42);
            using var materialsStream = File.OpenRead(_materialsModelPath);
            using var laborStream = File.OpenRead(_laborModelPath);

            var materialsModel = mlContext.Model.Load(materialsStream, out _);
            var laborModel = mlContext.Model.Load(laborStream, out _);

            _materialsPredictionEngine = mlContext.Model.CreatePredictionEngine<PredictionInputModel, RegressionPrediction>(materialsModel);
            _laborPredictionEngine = mlContext.Model.CreatePredictionEngine<PredictionInputModel, RegressionPrediction>(laborModel);

            _materialsLastWriteUtc = materialsWriteUtc;
            _laborLastWriteUtc = laborWriteUtc;
        }
    }

    private static float NormalizeDurationToDays(float duration, string? durationUnit)
    {
        if (duration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "La duracion debe ser mayor que cero.");
        }

        return durationUnit?.Trim().ToLowerInvariant() switch
        {
            "day" or "days" or "dia" or "dias" => duration,
            "month" or "months" or "mes" or "meses" => duration * 30f,
            _ => throw new ArgumentException("DurationUnit invalido. Usa dias o meses.", nameof(durationUnit))
        };
    }

    private string ReadModelVersion()
    {
        if (!File.Exists(_versionPath))
        {
            return "unknown";
        }

        var version = File.ReadAllText(_versionPath).Trim();
        return string.IsNullOrWhiteSpace(version) ? "unknown" : version;
    }
}
