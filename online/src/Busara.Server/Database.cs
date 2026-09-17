using Npgsql;

namespace Busara.Server;

public sealed class Database : IAsyncDisposable
{
    public NpgsqlDataSource Source { get; }
    private readonly ServerSettings settings;
    public Database(ServerSettings settings)
    {
        this.settings = settings;
        Source = NpgsqlDataSource.Create(settings.ConnectionString);
    }

    public async Task CheckAsync(CancellationToken ct = default)
    {
        await using var connection = await Source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            "SELECT version, key_fingerprint FROM schema_version WHERE singleton", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.GetInt32(0) != 1 ||
            !ServerSettings.Equal(reader.GetString(1), settings.KeyFingerprint))
            throw new InvalidOperationException("Database schema or persisted secret key does not match.");
    }

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await using var connection = await Source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(782340198); " +
            "CREATE TABLE IF NOT EXISTS schema_version (" +
            "singleton boolean PRIMARY KEY DEFAULT true CHECK(singleton), version integer NOT NULL, key_fingerprint text NOT NULL)",
            connection, tx))
            await command.ExecuteNonQueryAsync(ct);
        await using (var command = new NpgsqlCommand("SELECT count(*) FROM schema_version", connection, tx))
        {
            if ((long)(await command.ExecuteScalarAsync(ct))! == 0)
            {
                using var stream = typeof(Database).Assembly.GetManifestResourceStream("Busara.Server.schema.sql")!;
                using var reader = new StreamReader(stream);
                await using var migration = new NpgsqlCommand(await reader.ReadToEndAsync(ct), connection, tx);
                await migration.ExecuteNonQueryAsync(ct);
                await using var version = new NpgsqlCommand(
                    "INSERT INTO schema_version(version,key_fingerprint) VALUES (1,@key)", connection, tx);
                version.Parameters.AddWithValue("key", settings.KeyFingerprint);
                await version.ExecuteNonQueryAsync(ct);
            }
        }
        await tx.CommitAsync(ct);
        await CheckAsync(ct);
    }

    public ValueTask DisposeAsync() => Source.DisposeAsync();
}
