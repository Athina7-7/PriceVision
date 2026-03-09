using PriceVision.Application.Contracts;
using PriceVision.Domain.Entities;

namespace PriceVision.Application.Abstractions;

public interface IPredictiveModelService
{
    PredictionResult Predict(PredictionRequest request);
    Prediction BuildPredictionEntity(PredictionRequest request, PredictionResult result);
}
