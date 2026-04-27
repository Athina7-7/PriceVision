using System.Globalization;
using System.Text;

namespace PriceVision.Api.Reports;

internal sealed class PdfDocumentWriter
{
    private static readonly byte[] HeaderBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A];

    private readonly List<byte[]> objects = [];
    private readonly List<int> pageObjectIds = [];
    private readonly int fontRegularObjectId;
    private readonly int fontBoldObjectId;
    private readonly int pagesObjectId;
    private readonly int catalogObjectId;

    public PdfDocumentWriter()
    {
        fontRegularObjectId = AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        fontBoldObjectId = AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        pagesObjectId = ReserveObject();
        catalogObjectId = ReserveObject();
    }

    public void AddPage(Action<PdfCanvas> drawPage, float width = 842f, float height = 595f)
    {
        var canvas = new PdfCanvas(width, height);
        drawPage(canvas);

        var contentObjectId = AddStreamObject(canvas.Build());
        var pageObjectId = AddObject(FormattableString.Invariant(
            $"<< /Type /Page /Parent {pagesObjectId} 0 R /MediaBox [0 0 {width:0.###} {height:0.###}] /Resources << /Font << /F1 {fontRegularObjectId} 0 R /F2 {fontBoldObjectId} 0 R >> >> /Contents {contentObjectId} 0 R >>"));

        pageObjectIds.Add(pageObjectId);
    }

    public byte[] Build()
    {
        var kids = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));
        SetObject(pagesObjectId, FormattableString.Invariant($"<< /Type /Pages /Count {pageObjectIds.Count} /Kids [{kids}] >>"));
        SetObject(catalogObjectId, FormattableString.Invariant($"<< /Type /Catalog /Pages {pagesObjectId} 0 R >>"));

        using var stream = new MemoryStream();
        stream.Write(HeaderBytes);

        var offsets = new List<long>(objects.Count + 1) { 0L };

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{index + 1} 0 obj\n");
            stream.Write(objects[index]);
            WriteAscii(stream, "\nendobj\n");
        }

        var xrefPosition = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");

        foreach (var offset in offsets.Skip(1))
        {
            WriteAscii(stream, $"{offset.ToString("0000000000", CultureInfo.InvariantCulture)} 00000 n \n");
        }

        WriteAscii(
            stream,
            $"trailer\n<< /Size {objects.Count + 1} /Root {catalogObjectId} 0 R >>\nstartxref\n{xrefPosition.ToString(CultureInfo.InvariantCulture)}\n%%EOF");

        return stream.ToArray();
    }

    private int AddStreamObject(byte[] content)
    {
        using var stream = new MemoryStream();
        WriteAscii(stream, $"<< /Length {content.Length} >>\nstream\n");
        stream.Write(content);
        WriteAscii(stream, "\nendstream");
        return AddObject(stream.ToArray());
    }

    private int AddObject(string body) => AddObject(Encoding.ASCII.GetBytes(body));

    private int AddObject(byte[] body)
    {
        objects.Add(body);
        return objects.Count;
    }

    private int ReserveObject()
    {
        objects.Add([]);
        return objects.Count;
    }

    private void SetObject(int objectId, string body)
    {
        objects[objectId - 1] = Encoding.ASCII.GetBytes(body);
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes);
    }
}

internal sealed class PdfCanvas(float width, float height)
{
    private static readonly byte[] NewLine = [(byte)'\n'];

    private readonly MemoryStream stream = new();

    public float Width { get; } = width;
    public float Height { get; } = height;

    public byte[] Build() => stream.ToArray();

    public void FillRect(float x, float y, float rectWidth, float rectHeight, PdfColor color)
    {
        SetFillColor(color);
        WriteLine(FormattableString.Invariant($"{Format(x)} {Format(ToPdfY(y, rectHeight))} {Format(rectWidth)} {Format(rectHeight)} re f"));
    }

    public void StrokeRect(float x, float y, float rectWidth, float rectHeight, PdfColor color, float lineWidth = 1f)
    {
        SetStrokeColor(color);
        WriteLine(FormattableString.Invariant($"{Format(lineWidth)} w {Format(x)} {Format(ToPdfY(y, rectHeight))} {Format(rectWidth)} {Format(rectHeight)} re S"));
    }

    public void FillStrokeRect(float x, float y, float rectWidth, float rectHeight, PdfColor fill, PdfColor stroke, float lineWidth = 1f)
    {
        SetFillColor(fill);
        SetStrokeColor(stroke);
        WriteLine(FormattableString.Invariant($"{Format(lineWidth)} w {Format(x)} {Format(ToPdfY(y, rectHeight))} {Format(rectWidth)} {Format(rectHeight)} re B"));
    }

    public void DrawLine(float startX, float startY, float endX, float endY, PdfColor color, float lineWidth = 1f)
    {
        SetStrokeColor(color);
        WriteLine(FormattableString.Invariant($"{Format(lineWidth)} w {Format(startX)} {Format(ToPdfY(startY))} m {Format(endX)} {Format(ToPdfY(endY))} l S"));
    }

    public void DrawText(string text, float x, float y, float fontSize, PdfColor color, bool bold = false, PdfTextAlign align = PdfTextAlign.Left)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var content = text.Trim();
        var textWidth = MeasureTextWidth(content, fontSize, bold);
        var alignedX = align switch
        {
            PdfTextAlign.Center => x - (textWidth / 2f),
            PdfTextAlign.Right => x - textWidth,
            _ => x
        };

        WriteTextCommand(content, alignedX, y, fontSize, color, bold);
    }

    public float DrawWrappedText(
        string text,
        float x,
        float y,
        float maxWidth,
        float fontSize,
        PdfColor color,
        bool bold = false,
        int maxLines = 3,
        float? lineHeight = null,
        PdfTextAlign align = PdfTextAlign.Left)
    {
        var lines = WrapText(text, maxWidth, fontSize, bold, maxLines);
        var effectiveLineHeight = lineHeight ?? (fontSize * 1.28f);

        for (var index = 0; index < lines.Count; index++)
        {
            DrawText(lines[index], x, y + (index * effectiveLineHeight), fontSize, color, bold, align);
        }

        return lines.Count * effectiveLineHeight;
    }

    public void DrawBarChart(float x, float y, float width, float height, string title, IReadOnlyList<PdfBarItem> items, string emptyText)
    {
        FillStrokeRect(x, y, width, height, PdfTheme.PanelBackground, PdfTheme.PanelBorder);
        DrawText(title, x + 16f, y + 18f, 12f, PdfTheme.TitleColor, bold: true);

        if (items.Count == 0 || items.All(item => item.Value <= 0m))
        {
            DrawWrappedText(emptyText, x + 16f, y + 56f, width - 32f, 10f, PdfTheme.MutedTextColor, maxLines: 4);
            return;
        }

        var chartX = x + 18f;
        var chartWidth = width - 36f;
        var baselineY = y + height - 26f;
        var chartTop = y + 54f;
        var labelAreaHeight = 30f;
        var usableHeight = Math.Max(24f, baselineY - chartTop - labelAreaHeight);
        var maxValue = items.Max(item => item.Value);
        var gap = items.Count <= 3 ? 18f : 12f;
        var barWidth = Math.Max(24f, (chartWidth - ((items.Count - 1) * gap)) / items.Count);
        var startX = chartX + Math.Max(0f, (chartWidth - ((barWidth * items.Count) + ((items.Count - 1) * gap))) / 2f);

        DrawLine(chartX, baselineY, chartX + chartWidth, baselineY, PdfTheme.PanelBorder, 1f);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var ratio = maxValue <= 0m ? 0f : (float)(item.Value / maxValue);
            var barHeight = Math.Max(item.Value > 0m ? 6f : 0f, usableHeight * ratio);
            var barX = startX + index * (barWidth + gap);
            var barY = baselineY - barHeight;

            FillRect(barX, barY, barWidth, barHeight, item.Color);
            DrawText(item.DisplayValue, barX + (barWidth / 2f), barY - 16f, 8f, PdfTheme.MutedTextColor, align: PdfTextAlign.Center);
            DrawWrappedText(item.Label, barX + (barWidth / 2f), baselineY + 10f, barWidth + 10f, 8f, PdfTheme.TextColor, bold: true, maxLines: 2, align: PdfTextAlign.Center);
        }
    }

    public void DrawGaugeChart(float x, float y, float width, float height, string title, IReadOnlyList<PdfGaugeItem> items, string emptyText)
    {
        FillStrokeRect(x, y, width, height, PdfTheme.PanelBackground, PdfTheme.PanelBorder);
        DrawText(title, x + 16f, y + 18f, 12f, PdfTheme.TitleColor, bold: true);

        if (items.Count == 0)
        {
            DrawWrappedText(emptyText, x + 16f, y + 56f, width - 32f, 10f, PdfTheme.MutedTextColor, maxLines: 4);
            return;
        }

        var rowHeight = 24f;
        var gaugeWidth = width - 32f;
        var startY = y + 48f;

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var rowY = startY + index * rowHeight;
            if (rowY + rowHeight > y + height - 12f)
            {
                break;
            }

            DrawText(item.Label, x + 16f, rowY, 9f, PdfTheme.TextColor, bold: true);
            DrawText(item.DisplayValue, x + width - 16f, rowY, 9f, PdfTheme.MutedTextColor, bold: true, align: PdfTextAlign.Right);

            var gaugeY = rowY + 12f;
            FillRect(x + 16f, gaugeY, gaugeWidth, 8f, PdfTheme.GaugeBackground);

            var range = item.Max - item.Min;
            var ratio = range <= 0m ? 0m : (item.Value - item.Min) / range;
            var clampedRatio = Math.Clamp(ratio, 0m, 1m);
            var fillWidth = gaugeWidth * (float)clampedRatio;
            if (fillWidth > 0f)
            {
                FillRect(x + 16f, gaugeY, fillWidth, 8f, item.Color);
            }

            if (range > 0m)
            {
                var targetRatio = Math.Clamp((item.Target - item.Min) / range, 0m, 1m);
                var targetX = x + 16f + (gaugeWidth * (float)targetRatio);
                DrawLine(targetX, gaugeY - 2f, targetX, gaugeY + 10f, PdfTheme.GaugeTarget, 1f);
            }
        }
    }

    public float MeasureTextWidth(string text, float fontSize, bool bold = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var units = 0d;
        foreach (var character in text)
        {
            units += character switch
            {
                'i' or 'l' or 'I' or 'j' or '.' or ',' or ':' or ';' or '!' or '\'' or '|' or ' ' => 0.24d,
                'f' or 't' or 'r' => 0.34d,
                'm' or 'w' or 'M' or 'W' or '@' or '#' or '%' => 0.86d,
                _ when char.IsUpper(character) => 0.62d,
                _ when char.IsDigit(character) => 0.56d,
                _ => 0.5d
            };
        }

        var multiplier = bold ? 1.04d : 1d;
        return (float)(units * fontSize * multiplier);
    }

    private List<string> WrapText(string text, float maxWidth, float fontSize, bool bold, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [string.Empty];
        }

        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [string.Empty];
        }

        var lines = new List<string>();
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (MeasureTextWidth(candidate, fontSize, bold) <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                lines.Add(TrimToWidth(word, maxWidth, fontSize, bold));
                currentLine = string.Empty;
            }

            if (lines.Count == maxLines)
            {
                lines[maxLines - 1] = AppendEllipsis(lines[maxLines - 1], maxWidth, fontSize, bold);
                return lines;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }

        if (lines.Count > maxLines)
        {
            return lines.Take(maxLines - 1).Append(AppendEllipsis(lines[maxLines - 1], maxWidth, fontSize, bold)).ToList();
        }

        if (lines.Count == maxLines && words.Length > 0 && string.Join(" ", lines) != normalized)
        {
            lines[maxLines - 1] = AppendEllipsis(lines[maxLines - 1], maxWidth, fontSize, bold);
        }

        return lines;
    }

    private string TrimToWidth(string text, float maxWidth, float fontSize, bool bold)
    {
        if (MeasureTextWidth(text, fontSize, bold) <= maxWidth)
        {
            return text;
        }

        var current = text;
        while (current.Length > 1 && MeasureTextWidth($"{current}...", fontSize, bold) > maxWidth)
        {
            current = current[..^1];
        }

        return $"{current}...";
    }

    private string AppendEllipsis(string text, float maxWidth, float fontSize, bool bold)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (MeasureTextWidth($"{text}...", fontSize, bold) <= maxWidth)
        {
            return $"{text}...";
        }

        return TrimToWidth(text, maxWidth, fontSize, bold);
    }

    private void WriteTextCommand(string text, float x, float y, float fontSize, PdfColor color, bool bold)
    {
        WriteLine("BT");
        WriteLine(FormattableString.Invariant($"/{(bold ? "F2" : "F1")} {Format(fontSize)} Tf"));
        SetFillColor(color);
        WriteLine(FormattableString.Invariant($"1 0 0 1 {Format(x)} {Format(ToPdfY(y) - (fontSize * 0.85f))} Tm"));

        WriteRaw([(byte)'(']);
        WriteRaw(EscapeText(text));
        WriteRaw([(byte)')']);
        WriteLine(" Tj");
        WriteLine("ET");
    }

    private void SetFillColor(PdfColor color)
    {
        WriteLine(FormattableString.Invariant($"{Format(color.R)} {Format(color.G)} {Format(color.B)} rg"));
    }

    private void SetStrokeColor(PdfColor color)
    {
        WriteLine(FormattableString.Invariant($"{Format(color.R)} {Format(color.G)} {Format(color.B)} RG"));
    }

    private float ToPdfY(float y, float elementHeight = 0f) => Height - y - elementHeight;

    private void WriteLine(string value)
    {
        WriteRaw(Encoding.ASCII.GetBytes(value));
        stream.Write(NewLine);
    }

    private void WriteRaw(byte[] value)
    {
        stream.Write(value);
    }

    private static byte[] EscapeText(string value)
    {
        var bytes = Encoding.Latin1.GetBytes(value);
        using var escaped = new MemoryStream(bytes.Length + 16);

        foreach (var current in bytes)
        {
            switch (current)
            {
                case (byte)'(':
                case (byte)')':
                case (byte)'\\':
                    escaped.WriteByte((byte)'\\');
                    escaped.WriteByte(current);
                    break;
                case 10:
                case 13:
                    escaped.WriteByte((byte)' ');
                    break;
                default:
                    escaped.WriteByte(current);
                    break;
            }
        }

        return escaped.ToArray();
    }

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

internal readonly record struct PdfColor(double R, double G, double B)
{
    public static PdfColor FromRgb(byte red, byte green, byte blue) => new(red / 255d, green / 255d, blue / 255d);
}

internal enum PdfTextAlign
{
    Left,
    Center,
    Right
}

internal sealed record PdfBarItem(string Label, decimal Value, string DisplayValue, PdfColor Color);
internal sealed record PdfGaugeItem(string Label, decimal Value, decimal Min, decimal Max, decimal Target, string DisplayValue, PdfColor Color);

internal static class PdfTheme
{
    public static readonly PdfColor PageBackground = PdfColor.FromRgb(247, 249, 251);
    public static readonly PdfColor HeaderBackground = PdfColor.FromRgb(31, 78, 121);
    public static readonly PdfColor Accent = PdfColor.FromRgb(42, 157, 143);
    public static readonly PdfColor AccentStrong = PdfColor.FromRgb(111, 191, 74);
    public static readonly PdfColor PanelBackground = PdfColor.FromRgb(255, 255, 255);
    public static readonly PdfColor PanelBorder = PdfColor.FromRgb(226, 232, 240);
    public static readonly PdfColor TitleColor = PdfColor.FromRgb(31, 41, 55);
    public static readonly PdfColor TextColor = PdfColor.FromRgb(31, 41, 55);
    public static readonly PdfColor MutedTextColor = PdfColor.FromRgb(100, 116, 139);
    public static readonly PdfColor White = PdfColor.FromRgb(255, 255, 255);
    public static readonly PdfColor GaugeBackground = PdfColor.FromRgb(226, 232, 240);
    public static readonly PdfColor GaugeTarget = PdfColor.FromRgb(244, 162, 97);
    public static readonly PdfColor Danger = PdfColor.FromRgb(230, 57, 70);
    public static readonly PdfColor Warning = PdfColor.FromRgb(244, 162, 97);
    public static readonly PdfColor Info = PdfColor.FromRgb(31, 78, 121);
}
