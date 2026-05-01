using Microsoft.Data.Sqlite;
using SharpTimer.Core.Models;

namespace SharpTimer.Storage.Repositories;

public sealed class SqliteSolveRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteSolveRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveAsync(Solve solve, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solve);

        var now = DateTimeOffset.UtcNow;

        await using var connection = _connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO solves (
                id,
                session_id,
                duration_ms,
                penalty,
                scramble,
                comment,
                created_at,
                updated_at
            )
            VALUES (
                $id,
                $sessionId,
                $durationMs,
                $penalty,
                $scramble,
                $comment,
                $createdAt,
                $updatedAt
            )
            ON CONFLICT(id) DO UPDATE SET
                session_id = excluded.session_id,
                duration_ms = excluded.duration_ms,
                penalty = excluded.penalty,
                scramble = excluded.scramble,
                comment = excluded.comment,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue("$id", StorageValueConverter.ToStorageText(solve.Id));
        command.Parameters.AddWithValue("$sessionId", StorageValueConverter.ToStorageText(solve.SessionId));
        command.Parameters.AddWithValue("$durationMs", StorageValueConverter.ToDurationMilliseconds(solve.Duration));
        command.Parameters.AddWithValue("$penalty", StorageValueConverter.ToPenaltyValue(solve.Penalty));
        command.Parameters.AddWithValue("$scramble", StorageValueConverter.ToDbValue(solve.Scramble));
        command.Parameters.AddWithValue("$comment", StorageValueConverter.ToDbValue(solve.Comment));
        command.Parameters.AddWithValue("$createdAt", StorageValueConverter.ToStorageText(solve.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", StorageValueConverter.ToStorageText(now));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Solve>> ListBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, session_id, duration_ms, penalty, scramble, comment, created_at
            FROM solves
            WHERE session_id = $sessionId
            ORDER BY created_at ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", StorageValueConverter.ToStorageText(sessionId));

        var solves = new List<Solve>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            solves.Add(ReadSolve(reader));
        }

        return solves;
    }

    public async Task UpdatePenaltyAsync(
        Guid solveId,
        Penalty penalty,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE solves
            SET penalty = $penalty,
                updated_at = $updatedAt
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", StorageValueConverter.ToStorageText(solveId));
        command.Parameters.AddWithValue("$penalty", StorageValueConverter.ToPenaltyValue(penalty));
        command.Parameters.AddWithValue("$updatedAt", StorageValueConverter.ToStorageText(DateTimeOffset.UtcNow));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid solveId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM solves WHERE id = $id;";
        command.Parameters.AddWithValue("$id", StorageValueConverter.ToStorageText(solveId));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Solve ReadSolve(SqliteDataReader reader)
    {
        return new Solve
        {
            Id = StorageValueConverter.ToGuid(reader.GetString(0)),
            SessionId = StorageValueConverter.ToGuid(reader.GetString(1)),
            Duration = StorageValueConverter.ToDuration(reader.GetInt64(2)),
            Penalty = StorageValueConverter.ToPenalty(reader.GetInt32(3)),
            Scramble = reader.IsDBNull(4) ? null : reader.GetString(4),
            Comment = reader.IsDBNull(5) ? null : reader.GetString(5),
            CreatedAt = StorageValueConverter.ToDateTimeOffset(reader.GetString(6))
        };
    }
}
