using Microsoft.Data.Sqlite;

namespace SharpTimer.Storage;

public sealed class SharpTimerDatabase
{
    public const int CurrentSchemaVersion = 1;

    private readonly SqliteConnectionFactory _connectionFactory;

    public SharpTimerDatabase(string databasePath)
        : this(new SqliteConnectionFactory(databasePath))
    {
    }

    public SharpTimerDatabase(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateOpenConnection();
        await using var transaction = connection.BeginTransaction();

        await ExecuteAsync(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER NOT NULL PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """,
            cancellationToken);

        var appliedVersion = await GetAppliedVersionAsync(connection, transaction, cancellationToken);
        if (appliedVersion < 1)
        {
            await ApplyVersion1Async(connection, transaction, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<int> GetAppliedVersionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private static async Task ApplyVersion1Async(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            CREATE TABLE sessions (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                puzzle TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                is_archived INTEGER NOT NULL DEFAULT 0 CHECK (is_archived IN (0, 1)),
                sort_order INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE solves (
                id TEXT NOT NULL PRIMARY KEY,
                session_id TEXT NOT NULL,
                duration_ms INTEGER NOT NULL CHECK (duration_ms >= 0),
                penalty INTEGER NOT NULL DEFAULT 0 CHECK (penalty IN (0, 1, 2)),
                scramble TEXT NULL,
                comment TEXT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE
            );

            CREATE INDEX idx_sessions_active_sort
                ON sessions(is_archived, sort_order, created_at);

            CREATE INDEX idx_solves_session_created
                ON solves(session_id, created_at);

            CREATE INDEX idx_solves_created
                ON solves(created_at);
            """,
            cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO schema_migrations(version, applied_at) VALUES (1, $appliedAt);",
            cancellationToken,
            command => command.Parameters.AddWithValue("$appliedAt", StorageValueConverter.ToStorageText(DateTimeOffset.UtcNow)));

        await ExecuteAsync(connection, transaction, "PRAGMA user_version = 1;", cancellationToken);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        Action<SqliteCommand>? configure = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        configure?.Invoke(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
