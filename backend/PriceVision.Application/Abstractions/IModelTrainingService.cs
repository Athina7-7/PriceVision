using PriceVision.Application.Contracts;

namespace PriceVision.Application.Abstractions;

public interface IModelTrainingService
{
    TrainingResult Train(int rowCount);
}
