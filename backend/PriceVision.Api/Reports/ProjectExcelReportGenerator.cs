using System.IO.Compression;
using System.Text;
using PriceVision.Domain.Entities;

namespace PriceVision.Api.Reports;

internal sealed class ProjectExcelReportGenerator
{
    private const decimal LaborHourRateCop = 38_000m;

    public byte[] GeneratePredictionReport(
        Project project,
        Prediction selectedPrediction,
        IReadOnlyList<Prediction> projectPredictions,
        FinancialPrediction? financialPrediction,
        IReadOnlyList<EvmRecord> evmHistory)
    {
        var aggregate = PredictionAggregate.Create(projectPredictions);
        var latestEvm = evmHistory.OrderByDescending(item => item.PeriodDateUtc).FirstOrDefault();
        var workbook = new XlsxWorkbookBuilder();

        BuildPredictionSummarySheet(workbook.AddSheet("Resumen"), project, selectedPrediction, aggregate, financialPrediction, latestEvm);
        BuildPredictionChartsSheet(workbook.AddSheet("Graficos"), project, selectedPrediction, aggregate, financialPrediction, latestEvm);
        BuildPredictionHistorySheet(workbook.AddSheet("Historial"), projectPredictions, evmHistory);

        return workbook.Build();
    }

    public byte[] GenerateEvmReport(
        Project project,
        EvmRecord selectedRecord,
        IReadOnlyList<Prediction> projectPredictions,
        FinancialPrediction? financialPrediction,
        IReadOnlyList<EvmRecord> evmHistory)
    {
        var aggregate = PredictionAggregate.Create(projectPredictions);
        var workbook = new XlsxWorkbookBuilder();

        BuildEvmSummarySheet(workbook.AddSheet("Resumen"), project, selectedRecord, aggregate, financialPrediction);
        BuildEvmChartsSheet(workbook.AddSheet("Graficos"), project, selectedRecord, aggregate, financialPrediction, evmHistory);
        BuildEvmHistorySheet(workbook.AddSheet("Historial"), projectPredictions, evmHistory);

        return workbook.Build();
    }

    private static void BuildPredictionSummarySheet(
        XlsxSheet sheet,
        Project project,
        Prediction prediction,
        PredictionAggregate aggregate,
        FinancialPrediction? financialPrediction,
        EvmRecord? latestEvm)
    {
        sheet.SetColumnWidths(24, 32, 24, 32);
        sheet.Merge("A1", "D1");
        sheet.Merge("A2", "D2");
        sheet.AddRow(Cell.Title("Informe de Prediccion"));
        sheet.AddRow(Cell.Subtitle($"{project.Location} - {project.Name}"));
        sheet.AddEmptyRow();

        AddSection(sheet, "Datos del proyecto");
        sheet.AddRow(
            Cell.Label("Nombre"), Cell.Value(project.Name),
            Cell.Label("Tipo"), Cell.Value(project.Type));
        sheet.AddRow(
            Cell.Label("Ubicacion"), Cell.Value(project.Location),
            Cell.Label("Area"), Cell.Value($"{FormatNumber(project.AreaM2)} m2"));
        sheet.AddRow(
            Cell.Label("Duracion"), Cell.Value($"{FormatNumber(project.DurationMonths)} meses"),
            Cell.Label("Costo base"), Cell.Value(FormatCop(project.BaseCostCop)));
        sheet.AddRow(
            Cell.Label("Creado"), Cell.Value(FormatDateTime(project.CreatedAtUtc)),
            Cell.Label("ProyectoId"), Cell.Value(project.Id.ToString()));
        sheet.AddEmptyRow();

        AddSection(sheet, "Prediccion seleccionada");
        sheet.AddRow(
            Cell.Label("Registro"), Cell.Value(prediction.Id.ToString()),
            Cell.Label("Fecha"), Cell.Value(FormatDateTime(prediction.CreatedAtUtc)));
        sheet.AddRow(
            Cell.Label("Modelos"), Cell.Value(DescribeModels(prediction.PredictedMaterials, prediction.PredictedLabor)),
            Cell.Label("Cobertura"), Cell.Value(aggregate.CoverageLabel));
        sheet.AddRow(
            Cell.Label("Materiales"), Cell.Value(prediction.PredictedMaterials ? $"{FormatNumber(prediction.EstimatedMaterialQuantity)} unidades" : "No incluido"),
            Cell.Label("Costo materiales"), Cell.Value(prediction.PredictedMaterials ? FormatCop(prediction.EstimatedMaterialCostCop) : "No incluido"));
        sheet.AddRow(
            Cell.Label("Mano de obra"), Cell.Value(prediction.PredictedLabor ? $"{FormatNumber(prediction.RequiredLaborHours)} horas-persona" : "No incluido"),
            Cell.Label("Costo laboral eq."), Cell.Value(prediction.PredictedLabor ? FormatCop((decimal)prediction.RequiredLaborHours * LaborHourRateCop) : "No incluido"));
        sheet.AddRow(
            Cell.Label("Total directo"), Cell.Value(FormatCop(aggregate.TotalDirectCostCop)),
            Cell.Label("Ultima actualizacion"), Cell.Value(FormatDateTime(aggregate.LatestPredictionAtUtc ?? prediction.CreatedAtUtc)));
        sheet.AddEmptyRow();

        AddSection(sheet, "Control complementario");
        sheet.AddRow(
            Cell.Label("Prediccion financiera"), Cell.Value(financialPrediction is null ? "Pendiente" : FormatCop(financialPrediction.EstimatedTotalCostCop)),
            Cell.Label("Confianza"), Cell.Value(financialPrediction is null ? "Pendiente" : $"{FormatPercent(financialPrediction.ConfidencePercentage)} / {financialPrediction.ConfidenceLevel}"));
        sheet.AddRow(
            Cell.Label("Rango financiero"), Cell.Value(financialPrediction is null ? "Pendiente" : $"{FormatCop(financialPrediction.MinimumEstimatedCostCop)} - {FormatCop(financialPrediction.MaximumEstimatedCostCop)}"),
            Cell.Label("Historico por m2"), Cell.Value(financialPrediction is null ? "Pendiente" : FormatCop(financialPrediction.HistoricalAverageCostPerM2Cop)));
        sheet.AddRow(
            Cell.Label("EVM"), Cell.Value(latestEvm is null ? "Pendiente" : $"{latestEvm.CostInterpretation} / {latestEvm.ScheduleInterpretation}"),
            Cell.Label("CPI / SPI"), Cell.Value(latestEvm is null ? "Pendiente" : $"{FormatRatio(latestEvm.CPI)} / {FormatRatio(latestEvm.SPI)}"));
        sheet.AddRow(
            Cell.Label("PV / EV / AC"), Cell.Value(latestEvm is null ? "Pendiente" : $"{FormatCop(latestEvm.PV)} / {FormatCop(latestEvm.EV)} / {FormatCop(latestEvm.AC)}"),
            Cell.Label("Observacion"), Cell.Value("Reporte exportable para seguimiento de costos, recursos y control del proyecto."));
    }

    private static void BuildPredictionChartsSheet(
        XlsxSheet sheet,
        Project project,
        Prediction prediction,
        PredictionAggregate aggregate,
        FinancialPrediction? financialPrediction,
        EvmRecord? latestEvm)
    {
        var costItems = new List<ChartItem>
        {
            new("Costo base", project.BaseCostCop, FormatCop(project.BaseCostCop), "Referencia presupuestal del proyecto"),
            new("Materiales", aggregate.MaterialCostCop, FormatCop(aggregate.MaterialCostCop), prediction.PredictedMaterials ? "Estimado por el modelo de materiales" : "Sin modelo activo"),
            new("Labor eq.", aggregate.LaborCostCop, FormatCop(aggregate.LaborCostCop), prediction.PredictedLabor ? "Horas-persona equivalentes en costo" : "Sin modelo activo"),
            new("Total directo", aggregate.TotalDirectCostCop, FormatCop(aggregate.TotalDirectCostCop), "Suma de materiales y labor")
        };

        if (financialPrediction is not null)
        {
            costItems.Add(new ChartItem("Financiero", financialPrediction.EstimatedTotalCostCop, FormatCop(financialPrediction.EstimatedTotalCostCop), "Estimacion total considerando historicos y tendencia"));
        }

        var maxCost = Math.Max(1m, costItems.Max(item => item.Value));

        sheet.SetColumnWidths(22, 18, 28, 42);
        sheet.Merge("A1", "D1");
        sheet.AddRow(Cell.Title("Graficos de Prediccion"));
        sheet.AddRow(Cell.Subtitle("Desglose visual para abrir directamente en Excel"));
        sheet.AddEmptyRow();

        AddSection(sheet, "Desglose economico");
        sheet.AddRow(Cell.Header("Metrica"), Cell.Header("Valor"), Cell.Header("Grafico"), Cell.Header("Comentario"));
        foreach (var item in costItems)
        {
            sheet.AddRow(
                Cell.Label(item.Label),
                Cell.Value(item.DisplayValue),
                Cell.Value(BuildBar(item.Value, maxCost)),
                Cell.Value(item.Note));
        }

        sheet.AddEmptyRow();
        AddSection(sheet, "Cobertura y control");
        sheet.AddRow(Cell.Header("Indicador"), Cell.Header("Valor"), Cell.Header("Grafico"), Cell.Header("Lectura"));

        var indicators = new List<IndicatorItem>
        {
            new("Modelo materiales", aggregate.HasMaterials ? 100m : 0m, aggregate.HasMaterials ? "100%" : "0%", aggregate.HasMaterials ? "Activo" : "Pendiente"),
            new("Modelo mano de obra", aggregate.HasLabor ? 100m : 0m, aggregate.HasLabor ? "100%" : "0%", aggregate.HasLabor ? "Activo" : "Pendiente")
        };

        if (financialPrediction is not null)
        {
            indicators.Add(new IndicatorItem("Confianza financiera", (decimal)financialPrediction.ConfidencePercentage, $"{financialPrediction.ConfidencePercentage:0.0}%", financialPrediction.ConfidenceLevel));
        }

        if (latestEvm is not null)
        {
            indicators.Add(new IndicatorItem("CPI", latestEvm.CPI * 100m, FormatRatio(latestEvm.CPI), latestEvm.CostInterpretation));
            indicators.Add(new IndicatorItem("SPI", latestEvm.SPI * 100m, FormatRatio(latestEvm.SPI), latestEvm.ScheduleInterpretation));
        }

        foreach (var item in indicators)
        {
            sheet.AddRow(
                Cell.Label(item.Label),
                Cell.Value(item.DisplayValue),
                Cell.Value(BuildBar(item.Value, 160m)),
                Cell.Value(item.Note));
        }
    }

    private static void BuildPredictionHistorySheet(XlsxSheet sheet, IReadOnlyList<Prediction> projectPredictions, IReadOnlyList<EvmRecord> evmHistory)
    {
        sheet.SetColumnWidths(20, 18, 18, 18, 18, 18);
        sheet.Merge("A1", "F1");
        sheet.AddRow(Cell.Title("Historial"));
        sheet.AddRow(Cell.Subtitle("Predicciones del proyecto y registros EVM asociados"));
        sheet.AddEmptyRow();

        AddSection(sheet, "Predicciones registradas", "F");
        sheet.AddRow(Cell.Header("Fecha"), Cell.Header("Materiales"), Cell.Header("Mano de obra"), Cell.Header("Cant. materiales"), Cell.Header("Costo materiales"), Cell.Header("Horas persona"));
        foreach (var item in projectPredictions.OrderByDescending(x => x.CreatedAtUtc))
        {
            sheet.AddRow(
                Cell.Value(FormatDateTime(item.CreatedAtUtc)),
                Cell.Value(item.PredictedMaterials ? "Si" : "No"),
                Cell.Value(item.PredictedLabor ? "Si" : "No"),
                Cell.Value(item.PredictedMaterials ? FormatNumber(item.EstimatedMaterialQuantity) : "-"),
                Cell.Value(item.PredictedMaterials ? FormatCop(item.EstimatedMaterialCostCop) : "-"),
                Cell.Value(item.PredictedLabor ? FormatNumber(item.RequiredLaborHours) : "-"));
        }

        sheet.AddEmptyRow();
        AddSection(sheet, "Historial EVM", "F");
        sheet.AddRow(Cell.Header("Periodo"), Cell.Header("PV"), Cell.Header("EV"), Cell.Header("AC"), Cell.Header("CPI"), Cell.Header("SPI"));
        foreach (var item in evmHistory.OrderByDescending(x => x.PeriodDateUtc))
        {
            sheet.AddRow(
                Cell.Value(FormatDate(item.PeriodDateUtc)),
                Cell.Value(FormatCop(item.PV)),
                Cell.Value(FormatCop(item.EV)),
                Cell.Value(FormatCop(item.AC)),
                Cell.Value(FormatRatio(item.CPI)),
                Cell.Value(FormatRatio(item.SPI)));
        }
    }

    private static void BuildEvmSummarySheet(
        XlsxSheet sheet,
        Project project,
        EvmRecord record,
        PredictionAggregate aggregate,
        FinancialPrediction? financialPrediction)
    {
        sheet.SetColumnWidths(24, 32, 24, 32);
        sheet.Merge("A1", "D1");
        sheet.Merge("A2", "D2");
        sheet.AddRow(Cell.Title("Informe EVM"));
        sheet.AddRow(Cell.Subtitle($"{project.Location} - {project.Name}"));
        sheet.AddEmptyRow();

        AddSection(sheet, "Datos del proyecto");
        sheet.AddRow(
            Cell.Label("Nombre"), Cell.Value(project.Name),
            Cell.Label("Tipo"), Cell.Value(project.Type));
        sheet.AddRow(
            Cell.Label("Ubicacion"), Cell.Value(project.Location),
            Cell.Label("Area"), Cell.Value($"{FormatNumber(project.AreaM2)} m2"));
        sheet.AddRow(
            Cell.Label("Duracion"), Cell.Value($"{FormatNumber(project.DurationMonths)} meses"),
            Cell.Label("Costo base"), Cell.Value(FormatCop(project.BaseCostCop)));
        sheet.AddEmptyRow();

        AddSection(sheet, "Metricas EVM");
        sheet.AddRow(
            Cell.Label("Registro"), Cell.Value(record.Id.ToString()),
            Cell.Label("Periodo"), Cell.Value(FormatDate(record.PeriodDateUtc)));
        sheet.AddRow(
            Cell.Label("Fecha de calculo"), Cell.Value(FormatDateTime(record.CreatedAtUtc)),
            Cell.Label("CPI / SPI"), Cell.Value($"{FormatRatio(record.CPI)} / {FormatRatio(record.SPI)}"));
        sheet.AddRow(
            Cell.Label("PV / EV / AC"), Cell.Value($"{FormatCop(record.PV)} / {FormatCop(record.EV)} / {FormatCop(record.AC)}"),
            Cell.Label("Presupuesto"), Cell.Value(record.CostInterpretation));
        sheet.AddRow(
            Cell.Label("Cronograma"), Cell.Value(record.ScheduleInterpretation),
            Cell.Label("Prediccion financiera"), Cell.Value(financialPrediction is null ? "Pendiente" : FormatCop(financialPrediction.EstimatedTotalCostCop)));
        sheet.AddEmptyRow();

        AddSection(sheet, "Base de prediccion");
        sheet.AddRow(
            Cell.Label("Cobertura"), Cell.Value(aggregate.CoverageLabel),
            Cell.Label("Materiales"), Cell.Value(aggregate.HasMaterials ? $"{FormatNumber(aggregate.MaterialQuantity)} unidades" : "Pendiente"));
        sheet.AddRow(
            Cell.Label("Costo materiales"), Cell.Value(aggregate.HasMaterials ? FormatCop(aggregate.MaterialCostCop) : "Pendiente"),
            Cell.Label("Mano de obra"), Cell.Value(aggregate.HasLabor ? $"{FormatNumber(aggregate.LaborHours)} horas-persona" : "Pendiente"));
        sheet.AddRow(
            Cell.Label("Costo laboral eq."), Cell.Value(aggregate.HasLabor ? FormatCop(aggregate.LaborCostCop) : "Pendiente"),
            Cell.Label("Total directo"), Cell.Value(FormatCop(aggregate.TotalDirectCostCop)));
        sheet.AddRow(
            Cell.Label("Tendencia ubicacion"), Cell.Value(financialPrediction is null ? "Pendiente" : $"x{financialPrediction.LocationTrendFactor:0.00}"),
            Cell.Label("Confianza"), Cell.Value(financialPrediction is null ? "Pendiente" : $"{FormatPercent(financialPrediction.ConfidencePercentage)} / {financialPrediction.ConfidenceLevel}"));
    }

    private static void BuildEvmChartsSheet(
        XlsxSheet sheet,
        Project project,
        EvmRecord record,
        PredictionAggregate aggregate,
        FinancialPrediction? financialPrediction,
        IReadOnlyList<EvmRecord> evmHistory)
    {
        var costItems = new List<ChartItem>
        {
            new("Costo base", project.BaseCostCop, FormatCop(project.BaseCostCop), "Presupuesto base"),
            new("Total directo", aggregate.TotalDirectCostCop, FormatCop(aggregate.TotalDirectCostCop), "Materiales + labor equivalente"),
            new("PV", record.PV, FormatCop(record.PV), "Valor planeado"),
            new("EV", record.EV, FormatCop(record.EV), "Valor ganado"),
            new("AC", record.AC, FormatCop(record.AC), "Costo actual")
        };

        if (financialPrediction is not null)
        {
            costItems.Insert(2, new ChartItem("Financiero", financialPrediction.EstimatedTotalCostCop, FormatCop(financialPrediction.EstimatedTotalCostCop), "Estimacion financiera del proyecto"));
        }

        var maxCost = Math.Max(1m, costItems.Max(item => item.Value));

        sheet.SetColumnWidths(22, 18, 28, 42);
        sheet.Merge("A1", "D1");
        sheet.AddRow(Cell.Title("Graficos EVM"));
        sheet.AddRow(Cell.Subtitle("Comparativo visual para control del proyecto"));
        sheet.AddEmptyRow();

        AddSection(sheet, "Costos plan vs ejecucion");
        sheet.AddRow(Cell.Header("Metrica"), Cell.Header("Valor"), Cell.Header("Grafico"), Cell.Header("Comentario"));
        foreach (var item in costItems)
        {
            sheet.AddRow(
                Cell.Label(item.Label),
                Cell.Value(item.DisplayValue),
                Cell.Value(BuildBar(item.Value, maxCost)),
                Cell.Value(item.Note));
        }

        sheet.AddEmptyRow();
        AddSection(sheet, "Indicadores de desempeno");
        sheet.AddRow(Cell.Header("Indicador"), Cell.Header("Valor"), Cell.Header("Grafico"), Cell.Header("Lectura"));

        var indicators = new List<IndicatorItem>
        {
            new("CPI", record.CPI * 100m, FormatRatio(record.CPI), record.CostInterpretation),
            new("SPI", record.SPI * 100m, FormatRatio(record.SPI), record.ScheduleInterpretation),
            new("Historial disponible", evmHistory.Count, $"{evmHistory.Count} registro(s)", "Cantidad de puntos EVM almacenados")
        };

        if (financialPrediction is not null)
        {
            indicators.Add(new IndicatorItem("Confianza", (decimal)financialPrediction.ConfidencePercentage, $"{financialPrediction.ConfidencePercentage:0.0}%", financialPrediction.ConfidenceLevel));
            indicators.Add(new IndicatorItem("Tendencia ubicacion", financialPrediction.LocationTrendFactor * 100m, $"x{financialPrediction.LocationTrendFactor:0.00}", "Factor geografico aplicado"));
        }

        foreach (var item in indicators)
        {
            sheet.AddRow(
                Cell.Label(item.Label),
                Cell.Value(item.DisplayValue),
                Cell.Value(BuildBar(item.Value, 160m)),
                Cell.Value(item.Note));
        }
    }

    private static void BuildEvmHistorySheet(XlsxSheet sheet, IReadOnlyList<Prediction> projectPredictions, IReadOnlyList<EvmRecord> evmHistory)
    {
        sheet.SetColumnWidths(20, 18, 18, 18, 18, 18, 24, 24);
        sheet.Merge("A1", "H1");
        sheet.AddRow(Cell.Title("Historial"));
        sheet.AddRow(Cell.Subtitle("Detalle de predicciones base y evolucion EVM"));
        sheet.AddEmptyRow();

        AddSection(sheet, "Base predictiva", "H");
        sheet.AddRow(Cell.Header("Fecha"), Cell.Header("Materiales"), Cell.Header("Costo materiales"), Cell.Header("Labor"), Cell.Header("Costo laboral eq."), Cell.Header("Cobertura"), Cell.Header("Modelo"), Cell.Header("Registro"));
        foreach (var item in projectPredictions.OrderByDescending(x => x.CreatedAtUtc))
        {
            var laborCost = (decimal)item.RequiredLaborHours * LaborHourRateCop;
            sheet.AddRow(
                Cell.Value(FormatDateTime(item.CreatedAtUtc)),
                Cell.Value(item.PredictedMaterials ? FormatNumber(item.EstimatedMaterialQuantity) : "-"),
                Cell.Value(item.PredictedMaterials ? FormatCop(item.EstimatedMaterialCostCop) : "-"),
                Cell.Value(item.PredictedLabor ? FormatNumber(item.RequiredLaborHours) : "-"),
                Cell.Value(item.PredictedLabor ? FormatCop(laborCost) : "-"),
                Cell.Value(DescribeModels(item.PredictedMaterials, item.PredictedLabor)),
                Cell.Value(item.ModelVersion),
                Cell.Value(item.Id.ToString()));
        }

        sheet.AddEmptyRow();
        AddSection(sheet, "Historial EVM", "H");
        sheet.AddRow(Cell.Header("Periodo"), Cell.Header("PV"), Cell.Header("EV"), Cell.Header("AC"), Cell.Header("CPI"), Cell.Header("SPI"), Cell.Header("Presupuesto"), Cell.Header("Cronograma"));
        foreach (var item in evmHistory.OrderByDescending(x => x.PeriodDateUtc))
        {
            sheet.AddRow(
                Cell.Value(FormatDate(item.PeriodDateUtc)),
                Cell.Value(FormatCop(item.PV)),
                Cell.Value(FormatCop(item.EV)),
                Cell.Value(FormatCop(item.AC)),
                Cell.Value(FormatRatio(item.CPI)),
                Cell.Value(FormatRatio(item.SPI)),
                Cell.Value(item.CostInterpretation),
                Cell.Value(item.ScheduleInterpretation));
        }
    }

    private static void AddSection(XlsxSheet sheet, string title, string endColumn = "D")
    {
        sheet.Merge("A" + sheet.NextRowIndex, endColumn + sheet.NextRowIndex);
        sheet.AddRow(Cell.Section(title));
    }

    private static string BuildBar(decimal value, decimal maxValue, int width = 20)
    {
        if (maxValue <= 0m)
        {
            return string.Empty;
        }

        var ratio = Math.Clamp(value / maxValue, 0m, 1m);
        var filled = (int)Math.Round((double)(ratio * width), MidpointRounding.AwayFromZero);
        var safeFilled = Math.Clamp(filled, 0, width);
        return $"{new string('#', safeFilled)}{new string('-', width - safeFilled)}";
    }

    private static string DescribeModels(bool predictedMaterials, bool predictedLabor) => predictedMaterials && predictedLabor
        ? "Materiales y mano de obra"
        : predictedMaterials
            ? "Materiales"
            : predictedLabor
                ? "Mano de obra"
                : "Sin modelos";

    private static string FormatCop(decimal value) => $"COP {value:N0}";
    private static string FormatNumber(float value) => value.ToString("N1");
    private static string FormatPercent(float value) => $"{value:N1}%";
    private static string FormatRatio(decimal value) => value.ToString("N2");
    private static string FormatDate(DateTime value) => value.ToString("yyyy-MM-dd");
    private static string FormatDateTime(DateTime value) => value.ToString("yyyy-MM-dd HH:mm");

    private sealed record ChartItem(string Label, decimal Value, string DisplayValue, string Note);
    private sealed record IndicatorItem(string Label, decimal Value, string DisplayValue, string Note);

    private sealed record PredictionAggregate(
        bool HasMaterials,
        bool HasLabor,
        float MaterialQuantity,
        decimal MaterialCostCop,
        float LaborHours,
        decimal LaborCostCop,
        decimal TotalDirectCostCop,
        DateTime? LatestPredictionAtUtc,
        string CoverageLabel)
    {
        public static PredictionAggregate Create(IReadOnlyList<Prediction> predictions)
        {
            var latestMaterials = predictions
                .Where(item => item.PredictedMaterials)
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();

            var latestLabor = predictions
                .Where(item => item.PredictedLabor)
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();

            var hasMaterials = latestMaterials is not null;
            var hasLabor = latestLabor is not null;
            var materialQuantity = latestMaterials?.EstimatedMaterialQuantity ?? 0f;
            var materialCost = latestMaterials?.EstimatedMaterialCostCop ?? 0m;
            var laborHours = latestLabor?.RequiredLaborHours ?? 0f;
            var laborCost = (decimal)laborHours * LaborHourRateCop;
            DateTime? latestAt = predictions.Count == 0 ? null : predictions.Max(item => item.CreatedAtUtc);

            var coverageLabel = hasMaterials && hasLabor
                ? "Materiales y mano de obra"
                : hasMaterials
                    ? "Materiales"
                    : hasLabor
                        ? "Mano de obra"
                        : "Sin modelos";

            return new PredictionAggregate(
                hasMaterials,
                hasLabor,
                materialQuantity,
                materialCost,
                laborHours,
                laborCost,
                materialCost + laborCost,
                latestAt,
                coverageLabel);
        }
    }
}

internal sealed class XlsxWorkbookBuilder
{
    private readonly List<XlsxSheet> sheets = [];

    public XlsxSheet AddSheet(string name)
    {
        var sheet = new XlsxSheet(SanitizeSheetName(name));
        sheets.Add(sheet);
        return sheet;
    }

    public byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            WriteEntry(archive, "_rels/.rels", BuildRootRelationshipsXml());
            WriteEntry(archive, "docProps/app.xml", BuildAppPropertiesXml());
            WriteEntry(archive, "docProps/core.xml", BuildCorePropertiesXml());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationshipsXml());
            WriteEntry(archive, "xl/styles.xml", BuildStylesXml());

            for (var index = 0; index < sheets.Count; index++)
            {
                WriteEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", sheets[index].BuildXml());
            }
        }

        return stream.ToArray();
    }

    private string BuildContentTypesXml()
    {
        var builder = new StringBuilder();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        builder.Append("""<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        builder.Append("""<Default Extension="xml" ContentType="application/xml"/>""");
        builder.Append("""<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""");
        builder.Append("""<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
        builder.Append("""<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>""");
        builder.Append("""<Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>""");

        for (var index = 0; index < sheets.Count; index++)
        {
            builder.Append($"""<Override PartName="/xl/worksheets/sheet{index + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        }

        builder.Append("</Types>");
        return builder.ToString();
    }

    private static string BuildRootRelationshipsXml() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/></Relationships>""";

    private string BuildWorkbookXml()
    {
        var builder = new StringBuilder();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>""");

        for (var index = 0; index < sheets.Count; index++)
        {
            builder.Append($"""<sheet name="{EscapeXml(sheets[index].Name)}" sheetId="{index + 1}" r:id="rId{index + 1}"/>""");
        }

        builder.Append("""</sheets></workbook>""");
        return builder.ToString();
    }

    private string BuildWorkbookRelationshipsXml()
    {
        var builder = new StringBuilder();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");

        for (var index = 0; index < sheets.Count; index++)
        {
            builder.Append($"""<Relationship Id="rId{index + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{index + 1}.xml"/>""");
        }

        builder.Append($"""<Relationship Id="rId{sheets.Count + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");
        builder.Append("</Relationships>");
        return builder.ToString();
    }

    private string BuildAppPropertiesXml()
    {
        var titles = string.Concat(sheets.Select(sheet => $"""<vt:lpstr>{EscapeXml(sheet.Name)}</vt:lpstr>"""));
        return $$"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"><Application>PriceVision</Application><DocSecurity>0</DocSecurity><ScaleCrop>false</ScaleCrop><HeadingPairs><vt:vector size="2" baseType="variant"><vt:variant><vt:lpstr>Worksheets</vt:lpstr></vt:variant><vt:variant><vt:i4>{{sheets.Count}}</vt:i4></vt:variant></vt:vector></HeadingPairs><TitlesOfParts><vt:vector size="{{sheets.Count}}" baseType="lpstr">{{titles}}</vt:vector></TitlesOfParts><Company>OpenAI</Company><LinksUpToDate>false</LinksUpToDate><SharedDoc>false</SharedDoc><HyperlinksChanged>false</HyperlinksChanged><AppVersion>1.0</AppVersion></Properties>""";
    }

    private static string BuildCorePropertiesXml()
    {
        var now = DateTime.UtcNow.ToString("s") + "Z";
        return $$"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><dc:creator>PriceVision</dc:creator><cp:lastModifiedBy>PriceVision</cp:lastModifiedBy><dcterms:created xsi:type="dcterms:W3CDTF">{{now}}</dcterms:created><dcterms:modified xsi:type="dcterms:W3CDTF">{{now}}</dcterms:modified></cp:coreProperties>""";
    }

    private static string BuildStylesXml() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="4"><font><sz val="11"/><name val="Calibri"/><family val="2"/></font><font><b/><sz val="16"/><color rgb="FFFFFFFF"/><name val="Calibri"/><family val="2"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/><family val="2"/></font><font><b/><sz val="11"/><name val="Calibri"/><family val="2"/></font></fonts><fills count="5"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF1F4E79"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FF2A9D8F"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFF1F5F9"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color auto="1"/></left><right style="thin"><color auto="1"/></right><top style="thin"><color auto="1"/></top><bottom style="thin"><color auto="1"/></bottom><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="6"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf><xf numFmtId="0" fontId="3" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf><xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf><xf numFmtId="0" fontId="3" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf><xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string SanitizeSheetName(string value)
    {
        var invalidChars = new[] { '[', ']', ':', '*', '?', '/', '\\' };
        var cleaned = new string(value.Select(character => invalidChars.Contains(character) ? '-' : character).ToArray());
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    internal static string EscapeXml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
}

internal sealed class XlsxSheet(string name)
{
    private readonly List<XlsxRow> rows = [];
    private readonly List<(string Start, string End)> merges = [];
    private readonly List<double> columnWidths = [];

    public string Name { get; } = name;
    public int NextRowIndex => rows.Count + 1;

    public void AddRow(params XlsxCell[] cells) => rows.Add(new XlsxRow(cells.ToList()));
    public void AddEmptyRow() => rows.Add(new XlsxRow([]));
    public void Merge(string startCell, string endCell) => merges.Add((startCell, endCell));

    public void SetColumnWidths(params double[] widths)
    {
        columnWidths.Clear();
        columnWidths.AddRange(widths);
    }

    public string BuildXml()
    {
        var builder = new StringBuilder();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");

        if (columnWidths.Count > 0)
        {
            builder.Append("<cols>");
            for (var index = 0; index < columnWidths.Count; index++)
            {
                builder.Append($"""<col min="{index + 1}" max="{index + 1}" width="{columnWidths[index]:0.##}" customWidth="1"/>""");
            }

            builder.Append("</cols>");
        }

        builder.Append("<sheetData>");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            builder.Append($"""<row r="{rowIndex + 1}">""");

            for (var columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
            {
                var cell = row.Cells[columnIndex];
                if (string.IsNullOrEmpty(cell.Value))
                {
                    continue;
                }

                var cellReference = $"{ToColumnName(columnIndex + 1)}{rowIndex + 1}";
                builder.Append($"""<c r="{cellReference}" s="{cell.StyleId}" t="inlineStr"><is><t xml:space="preserve">{XlsxWorkbookBuilder.EscapeXml(cell.Value)}</t></is></c>""");
            }

            builder.Append("</row>");
        }
        builder.Append("</sheetData>");

        if (merges.Count > 0)
        {
            builder.Append($"""<mergeCells count="{merges.Count}">""");
            foreach (var merge in merges)
            {
                builder.Append($"""<mergeCell ref="{merge.Start}:{merge.End}"/>""");
            }

            builder.Append("</mergeCells>");
        }

        builder.Append("""<pageMargins left="0.5" right="0.5" top="0.75" bottom="0.75" header="0.3" footer="0.3"/></worksheet>""");
        return builder.ToString();
    }

    private static string ToColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar(65 + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }
}

internal sealed record XlsxRow(List<XlsxCell> Cells);
internal sealed record XlsxCell(string Value, int StyleId)
{
    public static XlsxCell Title(string value) => new(value, 1);
    public static XlsxCell Subtitle(string value) => new(value, 2);
    public static XlsxCell Section(string value) => new(value, 3);
    public static XlsxCell Header(string value) => new(value, 4);
    public static XlsxCell Label(string value) => new(value, 4);
    public static XlsxCell Text(string value) => new(value, 5);
}

internal static class Cell
{
    public static XlsxCell Title(string value) => XlsxCell.Title(value);
    public static XlsxCell Subtitle(string value) => XlsxCell.Subtitle(value);
    public static XlsxCell Section(string value) => XlsxCell.Section(value);
    public static XlsxCell Header(string value) => XlsxCell.Header(value);
    public static XlsxCell Label(string value) => XlsxCell.Label(value);
    public static XlsxCell Value(string value) => XlsxCell.Text(value);
}
