using Microsoft.Extensions.Hosting;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;

namespace PriceVision.Infrastructure.Ml;

public sealed class ModelTrainingService(IHostEnvironment environment) : IModelTrainingService
{
    private readonly string _artifactsPath = Path.Combine(environment.ContentRootPath, "Artifacts");
    private readonly string _datasetPath = Path.Combine(environment.ContentRootPath, "Artifacts", "synthetic-training-data.csv");
    private readonly string _materialsModelPath = Path.Combine(environment.ContentRootPath, "Artifacts", "materials-model.zip");
    private readonly string _laborModelPath = Path.Combine(environment.ContentRootPath, "Artifacts", "labor-model.zip");
    private readonly string _versionPath = Path.Combine(environment.ContentRootPath, "Artifacts", "model-version.txt");

    public TrainingResult Train(int rowCount)
    {
        if (rowCount < 500)
        {
            rowCount = 500;
        }

        SyntheticDatasetGenerator.Generate(_datasetPath, rowCount);
        Directory.CreateDirectory(_artifactsPath);

        var mlContext = new MLContext(seed: 42);
        var data = mlContext.Data.LoadFromTextFile<PredictionTrainingRow>(_datasetPath, hasHeader: true, separatorChar: ',');
        var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

        var commonPipeline = mlContext.Transforms.Categorical.OneHotEncoding("TypeEncoded", nameof(PredictionTrainingRow.Type))
            .Append(mlContext.Transforms.Categorical.OneHotEncoding("LocationEncoded", nameof(PredictionTrainingRow.Location)))
            .Append(mlContext.Transforms.Concatenate("Features",
                nameof(PredictionTrainingRow.AreaM2),
                nameof(PredictionTrainingRow.DurationDays),
                "TypeEncoded",
                "LocationEncoded"));

        var materialPipeline = commonPipeline.Append(mlContext.Regression.Trainers.Sdca(new SdcaRegressionTrainer.Options
        {
            FeatureColumnName = "Features",
            LabelColumnName = nameof(PredictionTrainingRow.MaterialQuantity),
            MaximumNumberOfIterations = 300
        }));

        var laborPipeline = commonPipeline.Append(mlContext.Regression.Trainers.Sdca(new SdcaRegressionTrainer.Options
        {
            FeatureColumnName = "Features",
            LabelColumnName = nameof(PredictionTrainingRow.LaborHours),
            MaximumNumberOfIterations = 300
        }));

        var materialModel = materialPipeline.Fit(split.TrainSet);
        var laborModel = laborPipeline.Fit(split.TrainSet);

        var materialPredictions = materialModel.Transform(split.TestSet);
        var laborPredictions = laborModel.Transform(split.TestSet);

        var materialMetrics = mlContext.Regression.Evaluate(materialPredictions, labelColumnName: nameof(PredictionTrainingRow.MaterialQuantity));
        var laborMetrics = mlContext.Regression.Evaluate(laborPredictions, labelColumnName: nameof(PredictionTrainingRow.LaborHours));

        mlContext.Model.Save(materialModel, split.TrainSet.Schema, _materialsModelPath);
        mlContext.Model.Save(laborModel, split.TrainSet.Schema, _laborModelPath);

        var modelVersion = $"v{DateTime.UtcNow:yyyyMMddHHmmss}";
        File.WriteAllText(_versionPath, modelVersion);

        return new TrainingResult(
            DatasetRows: rowCount,
            MaterialQuantityR2: materialMetrics.RSquared,
            MaterialQuantityRmse: materialMetrics.RootMeanSquaredError,
            LaborHoursR2: laborMetrics.RSquared,
            LaborHoursRmse: laborMetrics.RootMeanSquaredError,
            ModelVersion: modelVersion);
    }
}
