using System.Globalization;

namespace PriceVision.Infrastructure.Ml;

internal static class SyntheticDatasetGenerator
{
    private static readonly string[] Types = ["Residencial", "Comercial", "Industrial", "Remodelacion"];
    private static readonly string[] Locations = ["Bogota", "Medellin", "Cali", "Barranquilla", "Rural"];

    private static readonly Dictionary<string, float> TypeMaterialFactor = new()
    {
        ["Residencial"] = 1.00f,
        ["Comercial"] = 1.25f,
        ["Industrial"] = 1.40f,
        ["Remodelacion"] = 0.85f
    };

    private static readonly Dictionary<string, float> TypeLaborFactor = new()
    {
        ["Residencial"] = 1.00f,
        ["Comercial"] = 1.10f,
        ["Industrial"] = 1.30f,
        ["Remodelacion"] = 1.20f
    };

    private static readonly Dictionary<string, float> LocationFactor = new()
    {
        ["Bogota"] = 1.15f,
        ["Medellin"] = 1.08f,
        ["Cali"] = 1.04f,
        ["Barranquilla"] = 1.02f,
        ["Rural"] = 0.95f
    };

    public static void Generate(string path, int rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var writer = new StreamWriter(path, false);
        writer.WriteLine("AreaM2,Type,Location,DurationDays,MaterialQuantity,LaborHours");

        var random = new Random(42);
        for (var i = 0; i < rows; i++)
        {
            var area = NextFloat(random, 35f, 4000f);
            var type = Types[random.Next(Types.Length)];
            var location = Locations[random.Next(Locations.Length)];
            var durationDays = random.Next(20, 480);

            var materialBase = area * TypeMaterialFactor[type] * LocationFactor[location] * (1.0f + durationDays / 1400f);
            var laborBase = area * 0.18f * TypeLaborFactor[type] * LocationFactor[location] * (0.90f + durationDays / 1800f);

            var materialNoise = NextFloat(random, -0.08f, 0.08f);
            var laborNoise = NextFloat(random, -0.10f, 0.10f);

            var materialQuantity = Math.Max(10f, materialBase * (1f + materialNoise));
            var laborHours = Math.Max(8f, laborBase * (1f + laborNoise));

            writer.WriteLine(string.Join(",",
                area.ToString("0.###", CultureInfo.InvariantCulture),
                type,
                location,
                durationDays.ToString(CultureInfo.InvariantCulture),
                materialQuantity.ToString("0.###", CultureInfo.InvariantCulture),
                laborHours.ToString("0.###", CultureInfo.InvariantCulture)));
        }
    }

    private static float NextFloat(Random random, float min, float max)
    {
        return (float)(min + random.NextDouble() * (max - min));
    }
}
