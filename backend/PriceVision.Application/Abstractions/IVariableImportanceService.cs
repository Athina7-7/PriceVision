using PriceVision.Application.Contracts;

namespace PriceVision.Application.Abstractions;

public interface IVariableImportanceService
{
    IReadOnlyList<VariableImportanceResponse> GetCostVariableImportance();
}
