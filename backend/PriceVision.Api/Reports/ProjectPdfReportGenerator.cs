using PriceVision.Domain.Entities;

namespace PriceVision.Api.Reports;

internal sealed class ProjectPdfReportGenerator
{
    private const decimal LaborHourRateCop = 38_000m;
    private const float PageWidth = 842f;
    private const float PageHeight = 595f;
    private const float Margin = 32f;

    public byte[] GeneratePredictionReport(
        Project project,
        Prediction selectedPrediction,
        IReadOnlyList<Prediction> projectPredictions,
        FinancialPrediction? financialPrediction,
        IReadOnlyList<EvmRecord> evmHistory)
    {
        var aggregate = PredictionAggregate.Create(projectPredictions);
        var latestEvm = evmHistory.OrderByDescending(item => item.PeriodDateUtc).FirstOrDefault();

        var document = new PdfDocumentWriter();
        document.AddPage(canvas => DrawPredictionPage(canvas, project, selectedPrediction, aggregate, financialPrediction, latestEvm), PageWidth, PageHeight);
        return document.Build();
    }

    public byte[] GenerateEvmReport(
        Project project,
        EvmRecord selectedRecord,
        IReadOnlyList<Prediction> projectPredictions,
        FinancialPrediction? financialPrediction,
        IReadOnlyList<EvmRecord> evmHistory)
    {
        var aggregate = PredictionAggregate.Create(projectPredictions);

        var document = new PdfDocumentWriter();
        document.AddPage(canvas => DrawEvmPage(canvas, project, selectedRecord, aggregate, financialPrediction, evmHistory), PageWidth, PageHeight);
        return document.Build();
    }

    private static void DrawPredictionPage(
        PdfCanvas canvas,
        Project project,
        Prediction prediction,
        PredictionAggregate aggregate,
        FinancialPrediction? financialPrediction,
        EvmRecord? latestEvm)
    {
        canvas.FillRect(0f, 0f, canvas.Width, canvas.Height, PdfTheme.PageBackground);

        DrawHeader(
            canvas,
            "Informe de Prediccion",
            project.Name,
            $"Registro {ShortId(prediction.Id)}",
            prediction.CreatedAtUtc,
            "Prediccion por proyecto");

        var contentWidth = canvas.Width - (Margin * 2f);
        var cardWidth = (contentWidth - 24f) / 3f;
        var summaryY = 116f;

        DrawSummaryCard(
            canvas,
            Margin,
            summaryY,
            cardWidth,
            78f,
            "Proyecto",
            $"{project.Type} en {project.Location}",
            $"{FormatNumber(project.AreaM2)} m2, {FormatNumber(project.DurationMonths)} meses y costo base {FormatCop(project.BaseCostCop)}.",
            PdfTheme.Accent);

        DrawSummaryCard(
            canvas,
            Margin + cardWidth + 12f,
            summaryY,
            cardWidth,
            78f,
            "Cobertura",
            aggregate.CoverageLabel,
            $"Ultima actualizacion {FormatDateTime(aggregate.LatestPredictionAtUtc ?? prediction.CreatedAtUtc)}.",
            PdfTheme.Info);

        DrawSummaryCard(
            canvas,
            Margin + ((cardWidth + 12f) * 2f),
            summaryY,
            cardWidth,
            78f,
            "Control",
            latestEvm is null ? "EVM pendiente" : $"CPI {FormatRatio(latestEvm.CPI)} / SPI {FormatRatio(latestEvm.SPI)}",
            financialPrediction is null
                ? "Sin prediccion financiera asociada."
                : $"Confianza financiera {FormatPercent(financialPrediction.ConfidencePercentage)} y rango validado.",
            PdfTheme.AccentStrong);

        DrawPanel(
            canvas,
            Margin,
            214f,
            382f,
            136f,
            "Datos del proyecto",
            [
                new("Nombre", project.Name),
                new("Registro", ShortId(project.Id)),
                new("Tipo", project.Type),
                new("Ubicacion", project.Location),
                new("Area", $"{FormatNumber(project.AreaM2)} m2"),
                new("Duracion", $"{FormatNumber(project.DurationMonths)} meses"),
                new("Costo base", FormatCop(project.BaseCostCop)),
                new("Creado", FormatDateTime(project.CreatedAtUtc))
            ]);

        DrawPanel(
            canvas,
            Margin + 396f,
            214f,
            382f,
            136f,
            "Predicciones y metricas",
            [
                new("Modelos del registro", DescribeModels(prediction.PredictedMaterials, prediction.PredictedLabor)),
                new("Fecha de prediccion", FormatDateTime(prediction.CreatedAtUtc)),
                new("Materiales", prediction.PredictedMaterials ? $"{FormatNumber(prediction.EstimatedMaterialQuantity)} unidades" : "No incluido"),
                new("Costo de materiales", prediction.PredictedMaterials ? FormatCop(prediction.EstimatedMaterialCostCop) : "No incluido"),
                new("Mano de obra", prediction.PredictedLabor ? $"{FormatNumber(prediction.RequiredLaborHours)} horas-persona" : "No incluido"),
                new("Costo laboral eq.", prediction.PredictedLabor ? FormatCop((decimal)prediction.RequiredLaborHours * LaborHourRateCop) : "No incluido"),
                new("Total directo", FormatCop(aggregate.TotalDirectCostCop)),
                new("EVM", latestEvm is null ? "Pendiente" : $"{latestEvm.CostInterpretation} / {latestEvm.ScheduleInterpretation}")
            ]);

        canvas.DrawBarChart(
            Margin,
            368f,
            382f,
            164f,
            "Desglose economico",
            BuildPredictionCostBars(project, aggregate, financialPrediction),
            "No hay suficientes datos de costos para construir el grafico.");

        canvas.DrawGaugeChart(
            Margin + 396f,
            368f,
            382f,
            164f,
            "Cobertura y control",
            BuildPredictionGauges(aggregate, financialPrediction, latestEvm),
            "No hay indicadores disponibles para este registro.");

        canvas.DrawText(
            "Documento generado automaticamente por PriceVision. Resume proyecto, predicciones, soporte grafico y estado EVM relacionado.",
            Margin,
            556f,
            8.5f,
            PdfTheme.MutedTextColor);
    }

    private static void DrawEvmPage(
        PdfCanvas canvas,
        Project project,
        EvmRecord record,
        PredictionAggregate aggregate,
        FinancialPrediction? financialPrediction,
        IReadOnlyList<EvmRecord> evmHistory)
    {
        canvas.FillRect(0f, 0f, canvas.Width, canvas.Height, PdfTheme.PageBackground);

        DrawHeader(
            canvas,
            "Informe EVM",
            project.Name,
            $"Registro {ShortId(record.Id)}",
            record.CreatedAtUtc,
            "Control de valor ganado");

        var contentWidth = canvas.Width - (Margin * 2f);
        var cardWidth = (contentWidth - 24f) / 3f;
        var summaryY = 116f;

        DrawSummaryCard(
            canvas,
            Margin,
            summaryY,
            cardWidth,
            78f,
            "Proyecto",
            $"{project.Type} en {project.Location}",
            $"{FormatNumber(project.AreaM2)} m2, presupuesto base {FormatCop(project.BaseCostCop)}.",
            PdfTheme.Accent);

        DrawSummaryCard(
            canvas,
            Margin + cardWidth + 12f,
            summaryY,
            cardWidth,
            78f,
            "Periodo",
            FormatDate(record.PeriodDateUtc),
            $"{record.CostInterpretation}. {record.ScheduleInterpretation}.",
            PdfTheme.Info);

        DrawSummaryCard(
            canvas,
            Margin + ((cardWidth + 12f) * 2f),
            summaryY,
            cardWidth,
            78f,
            "Indicadores",
            $"CPI {FormatRatio(record.CPI)} / SPI {FormatRatio(record.SPI)}",
            financialPrediction is null
                ? "Sin prediccion financiera complementaria."
                : $"Confianza financiera {FormatPercent(financialPrediction.ConfidencePercentage)}.",
            PdfTheme.AccentStrong);

        DrawPanel(
            canvas,
            Margin,
            214f,
            382f,
            152f,
            "Proyecto y predicciones",
            [
                new("Proyecto", project.Name),
                new("Tipo / ubicacion", $"{project.Type} / {project.Location}"),
                new("Area / duracion", $"{FormatNumber(project.AreaM2)} m2 / {FormatNumber(project.DurationMonths)} meses"),
                new("Costo base", FormatCop(project.BaseCostCop)),
                new("Materiales", aggregate.HasMaterials ? $"{FormatNumber(aggregate.MaterialQuantity)} unidades" : "Pendiente"),
                new("Costo materiales", aggregate.HasMaterials ? FormatCop(aggregate.MaterialCostCop) : "Pendiente"),
                new("Mano de obra", aggregate.HasLabor ? $"{FormatNumber(aggregate.LaborHours)} horas-persona" : "Pendiente"),
                new("Costo laboral eq.", aggregate.HasLabor ? FormatCop(aggregate.LaborCostCop) : "Pendiente"),
                new("Total directo", FormatCop(aggregate.TotalDirectCostCop)),
                new("Prediccion financiera", financialPrediction is null ? "Pendiente" : FormatCop(financialPrediction.EstimatedTotalCostCop))
            ]);

        DrawPanel(
            canvas,
            Margin + 396f,
            214f,
            382f,
            152f,
            "Metricas EVM",
            [
                new("Registro", ShortId(record.Id)),
                new("Fecha de calculo", FormatDateTime(record.CreatedAtUtc)),
                new("Periodo", FormatDate(record.PeriodDateUtc)),
                new("PV", FormatCop(record.PV)),
                new("EV", FormatCop(record.EV)),
                new("AC", FormatCop(record.AC)),
                new("CPI", FormatRatio(record.CPI)),
                new("SPI", FormatRatio(record.SPI)),
                new("Presupuesto", record.CostInterpretation),
                new("Cronograma", record.ScheduleInterpretation)
            ]);

        canvas.DrawBarChart(
            Margin,
            384f,
            382f,
            148f,
            "Costos plan vs ejecucion",
            BuildEvmCostBars(project, aggregate, financialPrediction, record),
            "No hay suficientes datos para construir el grafico EVM.");

        canvas.DrawGaugeChart(
            Margin + 396f,
            384f,
            382f,
            148f,
            "Indicadores de desempeno",
            BuildEvmGauges(record, financialPrediction, evmHistory),
            "No hay indicadores secundarios disponibles para este registro.");

        canvas.DrawText(
            "Documento generado automaticamente por PriceVision. Incluye datos del proyecto, predicciones consolidadas, graficos y metricas EVM.",
            Margin,
            556f,
            8.5f,
            PdfTheme.MutedTextColor);
    }

    private static void DrawHeader(PdfCanvas canvas, string title, string subtitle, string badge, DateTime generatedAtUtc, string caption)
    {
        canvas.FillRect(0f, 0f, canvas.Width, 88f, PdfTheme.HeaderBackground);
        canvas.FillRect(0f, 0f, 8f, 88f, PdfTheme.AccentStrong);

        canvas.DrawText(title, Margin, 22f, 24f, PdfTheme.White, bold: true);
        canvas.DrawWrappedText(subtitle, Margin, 50f, 360f, 13f, PdfTheme.White, maxLines: 2);

        canvas.DrawText(caption, canvas.Width - Margin, 22f, 9f, PdfTheme.White, bold: true, align: PdfTextAlign.Right);
        canvas.DrawText(badge, canvas.Width - Margin, 42f, 13f, PdfTheme.White, bold: true, align: PdfTextAlign.Right);
        canvas.DrawText($"Generado {FormatDateTime(generatedAtUtc)} UTC", canvas.Width - Margin, 62f, 9f, PdfTheme.White, align: PdfTextAlign.Right);
    }

    private static void DrawSummaryCard(
        PdfCanvas canvas,
        float x,
        float y,
        float width,
        float height,
        string eyebrow,
        string title,
        string description,
        PdfColor accentColor)
    {
        canvas.FillStrokeRect(x, y, width, height, PdfTheme.PanelBackground, PdfTheme.PanelBorder);
        canvas.FillRect(x, y, 6f, height, accentColor);
        canvas.DrawText(eyebrow, x + 18f, y + 16f, 8f, PdfTheme.MutedTextColor, bold: true);
        canvas.DrawWrappedText(title, x + 18f, y + 32f, width - 36f, 13f, PdfTheme.TitleColor, bold: true, maxLines: 2);
        canvas.DrawWrappedText(description, x + 18f, y + 54f, width - 36f, 9f, PdfTheme.MutedTextColor, maxLines: 2);
    }

    private static void DrawPanel(PdfCanvas canvas, float x, float y, float width, float height, string title, IReadOnlyList<ReportItem> items)
    {
        canvas.FillStrokeRect(x, y, width, height, PdfTheme.PanelBackground, PdfTheme.PanelBorder);
        canvas.DrawText(title, x + 16f, y + 18f, 12f, PdfTheme.TitleColor, bold: true);

        var columnCount = 2;
        var columnGap = 14f;
        var cellWidth = (width - 32f - columnGap) / columnCount;
        var rowHeight = 28f;
        var startY = y + 48f;

        for (var index = 0; index < items.Count; index++)
        {
            var row = index / columnCount;
            var column = index % columnCount;
            var itemY = startY + (row * rowHeight);
            if (itemY + rowHeight > y + height - 8f)
            {
                break;
            }

            var itemX = x + 16f + column * (cellWidth + columnGap);
            canvas.DrawText(items[index].Label, itemX, itemY, 8f, PdfTheme.MutedTextColor, bold: true);
            canvas.DrawWrappedText(items[index].Value, itemX, itemY + 11f, cellWidth, 10f, PdfTheme.TextColor, bold: true, maxLines: 2);
        }
    }

    private static IReadOnlyList<PdfBarItem> BuildPredictionCostBars(Project project, PredictionAggregate aggregate, FinancialPrediction? financialPrediction)
    {
        var items = new List<PdfBarItem>
        {
            new("Costo base", project.BaseCostCop, FormatCompactCop(project.BaseCostCop), PdfTheme.Info),
            new("Materiales", aggregate.MaterialCostCop, FormatCompactCop(aggregate.MaterialCostCop), PdfTheme.Accent),
            new("Labor eq.", aggregate.LaborCostCop, FormatCompactCop(aggregate.LaborCostCop), PdfTheme.Warning),
            new("Total directo", aggregate.TotalDirectCostCop, FormatCompactCop(aggregate.TotalDirectCostCop), PdfTheme.AccentStrong)
        };

        if (financialPrediction is not null)
        {
            items.Add(new("Financiero", financialPrediction.EstimatedTotalCostCop, FormatCompactCop(financialPrediction.EstimatedTotalCostCop), PdfTheme.Danger));
        }

        return items;
    }

    private static IReadOnlyList<PdfGaugeItem> BuildPredictionGauges(PredictionAggregate aggregate, FinancialPrediction? financialPrediction, EvmRecord? latestEvm)
    {
        var items = new List<PdfGaugeItem>
        {
            new("Modelo materiales", aggregate.HasMaterials ? 100m : 0m, 0m, 100m, 100m, aggregate.HasMaterials ? "Activo" : "Pendiente", PdfTheme.Accent),
            new("Modelo mano de obra", aggregate.HasLabor ? 100m : 0m, 0m, 100m, 100m, aggregate.HasLabor ? "Activo" : "Pendiente", PdfTheme.Info)
        };

        if (financialPrediction is not null)
        {
            items.Add(new("Confianza financiera", (decimal)financialPrediction.ConfidencePercentage, 0m, 100m, 75m, FormatPercent(financialPrediction.ConfidencePercentage), PdfTheme.AccentStrong));
        }

        if (latestEvm is not null)
        {
            items.Add(new("CPI", latestEvm.CPI, 0m, 1.6m, 1m, FormatRatio(latestEvm.CPI), latestEvm.CPI >= 1m ? PdfTheme.AccentStrong : PdfTheme.Danger));
            items.Add(new("SPI", latestEvm.SPI, 0m, 1.6m, 1m, FormatRatio(latestEvm.SPI), latestEvm.SPI >= 1m ? PdfTheme.AccentStrong : PdfTheme.Warning));
        }

        return items;
    }

    private static IReadOnlyList<PdfBarItem> BuildEvmCostBars(Project project, PredictionAggregate aggregate, FinancialPrediction? financialPrediction, EvmRecord record)
    {
        var items = new List<PdfBarItem>
        {
            new("Costo base", project.BaseCostCop, FormatCompactCop(project.BaseCostCop), PdfTheme.Info),
            new("Total directo", aggregate.TotalDirectCostCop, FormatCompactCop(aggregate.TotalDirectCostCop), PdfTheme.Accent),
            new("PV", record.PV, FormatCompactCop(record.PV), PdfTheme.Warning),
            new("EV", record.EV, FormatCompactCop(record.EV), PdfTheme.AccentStrong),
            new("AC", record.AC, FormatCompactCop(record.AC), PdfTheme.Danger)
        };

        if (financialPrediction is not null)
        {
            items.Insert(2, new PdfBarItem("Financiero", financialPrediction.EstimatedTotalCostCop, FormatCompactCop(financialPrediction.EstimatedTotalCostCop), PdfTheme.TitleColor));
        }

        return items;
    }

    private static IReadOnlyList<PdfGaugeItem> BuildEvmGauges(EvmRecord record, FinancialPrediction? financialPrediction, IReadOnlyList<EvmRecord> evmHistory)
    {
        var items = new List<PdfGaugeItem>
        {
            new("CPI", record.CPI, 0m, 1.6m, 1m, FormatRatio(record.CPI), record.CPI >= 1m ? PdfTheme.AccentStrong : PdfTheme.Danger),
            new("SPI", record.SPI, 0m, 1.6m, 1m, FormatRatio(record.SPI), record.SPI >= 1m ? PdfTheme.AccentStrong : PdfTheme.Warning)
        };

        if (financialPrediction is not null)
        {
            items.Add(new("Confianza", (decimal)financialPrediction.ConfidencePercentage, 0m, 100m, 75m, FormatPercent(financialPrediction.ConfidencePercentage), PdfTheme.Info));
            var factorMax = Math.Max(1.6m, financialPrediction.LocationTrendFactor + 0.25m);
            items.Add(new("Tendencia ubicacion", financialPrediction.LocationTrendFactor, 0m, factorMax, 1m, $"x{financialPrediction.LocationTrendFactor:0.00}", PdfTheme.TitleColor));
        }

        if (evmHistory.Count > 1)
        {
            var latestCount = evmHistory.Count;
            items.Add(new("Historial disponible", latestCount, 0m, Math.Max(5, latestCount), 1m, $"{latestCount} registro(s)", PdfTheme.Accent));
        }

        return items;
    }

    private static string ShortId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();
    private static string DescribeModels(bool predictedMaterials, bool predictedLabor) => predictedMaterials && predictedLabor
        ? "Materiales y mano de obra"
        : predictedMaterials
            ? "Materiales"
            : predictedLabor
                ? "Mano de obra"
                : "Sin modelos";

    private static string FormatCop(decimal value) => $"COP {value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}";
    private static string FormatCompactCop(decimal value)
    {
        var absolute = Math.Abs(value);
        var sign = value < 0 ? "-" : string.Empty;

        return absolute switch
        {
            >= 1_000_000_000m => $"{sign}{absolute / 1_000_000_000m:0.0}B",
            >= 1_000_000m => $"{sign}{absolute / 1_000_000m:0.0}M",
            >= 1_000m => $"{sign}{absolute / 1_000m:0.0}K",
            _ => $"{sign}{absolute:0}"
        };
    }

    private static string FormatNumber(float value) => value.ToString("N1", System.Globalization.CultureInfo.InvariantCulture);
    private static string FormatPercent(float value) => $"{value.ToString("N1", System.Globalization.CultureInfo.InvariantCulture)}%";
    private static string FormatRatio(decimal value) => value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
    private static string FormatDate(DateTime value) => value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static string FormatDateTime(DateTime value) => value.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private sealed record ReportItem(string Label, string Value);

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
            var totalDirectCost = materialCost + laborCost;
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
                totalDirectCost,
                latestAt,
                coverageLabel);
        }
    }
}
