namespace PriceVision.Api.Reports;

internal static class ProjectReportFileNameBuilder
{
    public static string BuildPdf(string location, string projectName) => Build(location, projectName, "pdf");
    public static string BuildExcel(string location, string projectName) => Build(location, projectName, "xlsx");

    private static string Build(string location, string projectName, string extension)
    {
        var safeLocation = SanitizePart(location);
        var safeProjectName = SanitizeFileName(projectName);
        var parts = new[] { safeLocation, safeProjectName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        var baseName = parts.Length > 0
            ? string.Join(" - ", parts)
            : "reporte";

        return $"{baseName}.{extension}";
    }

    private static string SanitizeFileName(string value)
    {
        var cleaned = SanitizePart(value);
        return string.IsNullOrWhiteSpace(cleaned) ? "reporte" : cleaned;
    }

    private static string SanitizePart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value
            .Where(character => !invalidChars.Contains(character))
            .ToArray());

        cleaned = string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Trim(' ', '.', '-');
    }
}
