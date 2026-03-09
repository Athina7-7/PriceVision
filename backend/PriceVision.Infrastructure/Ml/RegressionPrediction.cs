using Microsoft.ML.Data;

namespace PriceVision.Infrastructure.Ml;

public sealed class RegressionPrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
}
