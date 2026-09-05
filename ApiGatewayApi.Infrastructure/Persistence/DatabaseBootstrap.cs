using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ApiGatewayApi.Infrastructure.Persistence;

public static class DatabaseBootstrap
{
    public const int Utf8EncodingId = 6;

    public static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("GatewayBootstrap");

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        await EnsureUtf8DatabaseAsync(connectionString, configuration, logger);

        var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Gateway database migration completed. Chế độ tạo thủ công: Không nạp dữ liệu mẫu (No seed data applied).");
    }

    private static async Task EnsureUtf8DatabaseAsync(string connectionString, IConfiguration configuration, ILogger logger)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDatabase = builder.Database;
        if (string.IsNullOrWhiteSpace(targetDatabase))
        {
            return;
        }

        builder.Database = "postgres";
        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();

        var encodingId = await GetDatabaseEncodingIdAsync(conn, targetDatabase);
        if (encodingId == null)
        {
            logger.LogInformation("Creating UTF8 database {Database}", targetDatabase);
            await CreateUtf8DatabaseAsync(conn, targetDatabase);
            return;
        }

        if (encodingId != Utf8EncodingId)
        {
            var allowDrop = configuration.GetValue<bool>("Database:AllowDropNonUtf8");
            if (!allowDrop)
            {
                throw new InvalidOperationException(
                    $"Database '{targetDatabase}' is not UTF8 (encoding id {encodingId}). " +
                    "Set Database:AllowDropNonUtf8=true only in local development to recreate it.");
            }

            logger.LogWarning("Recreating non-UTF8 database {Database} because Database:AllowDropNonUtf8=true.", targetDatabase);
            await using var dropCmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{targetDatabase}\" WITH (FORCE);", conn);
            await dropCmd.ExecuteNonQueryAsync();
            await CreateUtf8DatabaseAsync(conn, targetDatabase);
        }
    }

    private static async Task CreateUtf8DatabaseAsync(NpgsqlConnection conn, string databaseName)
    {
        var createSql = $"""
            CREATE DATABASE "{databaseName}"
            WITH ENCODING 'UTF8'
                 LC_COLLATE='C'
                 LC_CTYPE='C'
                 TEMPLATE template0;
            """;
        await using var createCmd = new NpgsqlCommand(createSql, conn);
        await createCmd.ExecuteNonQueryAsync();
    }

    private static async Task<int?> GetDatabaseEncodingIdAsync(NpgsqlConnection conn, string databaseName)
    {
        await using var cmd = new NpgsqlCommand("SELECT encoding FROM pg_database WHERE datname = @dbname;", conn);
        cmd.Parameters.AddWithValue("dbname", databaseName);
        var res = await cmd.ExecuteScalarAsync();
        return res is null or DBNull ? null : Convert.ToInt32(res);
    }
}
