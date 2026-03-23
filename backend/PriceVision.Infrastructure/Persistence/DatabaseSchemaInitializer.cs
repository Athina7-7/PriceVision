using Microsoft.EntityFrameworkCore;

namespace PriceVision.Infrastructure.Persistence;

public static class DatabaseSchemaInitializer
{
    public static async Task EnsureSchemaAsync(PriceVisionDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureProjectsTableAsync(dbContext, cancellationToken);
        await EnsureProjectColumnAsync(dbContext, "Name", "TEXT NOT NULL DEFAULT ''", cancellationToken);

        await EnsurePredictionColumnAsync(dbContext, "ProjectId", "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'", cancellationToken);
        await EnsurePredictionColumnAsync(dbContext, "AreaM2", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsurePredictionColumnAsync(dbContext, "PredictedMaterials", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await EnsurePredictionColumnAsync(dbContext, "PredictedLabor", "INTEGER NOT NULL DEFAULT 1", cancellationToken);

        const string createEvmTableSql = """
            CREATE TABLE IF NOT EXISTS "EVM_Records" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_EVM_Records" PRIMARY KEY,
                "ProjectId" TEXT NOT NULL,
                "PeriodDateUtc" TEXT NOT NULL,
                "PV" TEXT NOT NULL,
                "EV" TEXT NOT NULL,
                "AC" TEXT NOT NULL,
                "CPI" TEXT NOT NULL,
                "SPI" TEXT NOT NULL,
                "CostInterpretation" TEXT NOT NULL,
                "ScheduleInterpretation" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """;

        await dbContext.Database.ExecuteSqlRawAsync(createEvmTableSql, cancellationToken);
    }

    private static async Task EnsureProjectsTableAsync(PriceVisionDbContext dbContext, CancellationToken cancellationToken)
    {
        const string createProjectsTableSql = """
            CREATE TABLE IF NOT EXISTS "Projects" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Projects" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "AreaM2" REAL NOT NULL,
                "Location" TEXT NOT NULL,
                "Type" TEXT NOT NULL,
                "DurationMonths" REAL NOT NULL,
                "BaseCostCop" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """;

        await dbContext.Database.ExecuteSqlRawAsync(createProjectsTableSql, cancellationToken);
    }

    private static async Task EnsureProjectColumnAsync(PriceVisionDbContext dbContext, string columnName, string columnSqlType, CancellationToken cancellationToken)
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var inspectCommand = connection.CreateCommand();
        inspectCommand.CommandText = "PRAGMA table_info('Projects');";

        var exists = false;
        await using (var reader = await inspectCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (!exists)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE \"Projects\" ADD COLUMN \"{columnName}\" {columnSqlType};";
            await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task EnsurePredictionColumnAsync(PriceVisionDbContext dbContext, string columnName, string columnSqlType, CancellationToken cancellationToken)
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var inspectCommand = connection.CreateCommand();
        inspectCommand.CommandText = "PRAGMA table_info('Predictions');";

        var exists = false;
        await using (var reader = await inspectCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (!exists)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE \"Predictions\" ADD COLUMN \"{columnName}\" {columnSqlType};";
            await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
