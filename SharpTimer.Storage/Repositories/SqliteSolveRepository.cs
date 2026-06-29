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
                source,
                scramble,
                comment,
                move_sequence,
                move_count,
                tps,
                reconstruction_method,
                solve_meta_json,
                created_at,
                updated_at
            )
            VALUES (
                $id,
                $sessionId,
                $durationMs,
                $source,
                $scramble,
                $comment,
                $moveSequence,
                $moveCount,
                $tps,
                $reconstructionMethod,
                $solveMetaJson,
                $createdAt,
                $updatedAt
            )
            ON CONFLICT(id) DO UPDATE SET
                session_id = excluded.session_id,
                duration_ms = excluded.duration_ms,
                source = excluded.source,
                scramble = excluded.scramble,
                comment = excluded.comment,
                move_sequence = excluded.move_sequence,
                move_count = excluded.move_count,
                tps = excluded.tps,
                reconstruction_method = excluded.reconstruction_method,
                solve_meta_json = excluded.solve_meta_json,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue("$id", StorageValueConverter.ToStorageText(solve.Id));
        command.Parameters.AddWithValue("$sessionId", StorageValueConverter.ToStorageText(solve.SessionId));
        command.Parameters.AddWithValue("$durationMs", StorageValueConverter.ToDurationMilliseconds(solve.Duration));
        command.Parameters.AddWithValue("$source", StorageValueConverter.ToSolveSourceValue(solve.Source));
        command.Parameters.AddWithValue("$scramble", StorageValueConverter.ToDbValue(solve.Scramble));
        command.Parameters.AddWithValue("$comment", StorageValueConverter.ToDbValue(solve.Comment));
        command.Parameters.AddWithValue("$moveSequence", StorageValueConverter.ToDbValue(solve.MoveSequence));
        command.Parameters.AddWithValue("$moveCount", StorageValueConverter.ToDbValue(solve.MoveCount));
        command.Parameters.AddWithValue("$tps", StorageValueConverter.ToDbValue(solve.Tps));
        command.Parameters.AddWithValue("$reconstructionMethod", StorageValueConverter.ToDbValue(solve.ReconstructionMethod));
        command.Parameters.AddWithValue("$solveMetaJson", StorageValueConverter.ToDbValue(solve.SolveMetaJson));
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
            SELECT
                id,
                session_id,
                duration_ms,
                scramble,
                comment,
                created_at,
                source,
                move_sequence,
                move_count,
                tps,
                reconstruction_method,
                solve_meta_json
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

    public async Task UpdateCommentAsync(
        Guid solveId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE solves
            SET comment = $comment,
                updated_at = $updatedAt
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", StorageValueConverter.ToStorageText(solveId));
        command.Parameters.AddWithValue("$comment", StorageValueConverter.ToDbValue(NormalizeComment(comment)));
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
            Scramble = reader.IsDBNull(3) ? null : reader.GetString(3),
            Comment = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt = StorageValueConverter.ToDateTimeOffset(reader.GetString(5)),
            Source = StorageValueConverter.ToSolveSource(reader.GetInt32(6)),
            MoveSequence = reader.IsDBNull(7) ? null : reader.GetString(7),
            MoveCount = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            Tps = reader.IsDBNull(9) ? null : reader.GetDouble(9),
            ReconstructionMethod = reader.IsDBNull(10) ? null : reader.GetString(10),
            SolveMetaJson = reader.IsDBNull(11) ? null : reader.GetString(11)
        };
    }

    private static string? NormalizeComment(string? comment)
    {
        var normalizedComment = comment?.Trim();
        return string.IsNullOrWhiteSpace(normalizedComment) ? null : normalizedComment;
    }
}
