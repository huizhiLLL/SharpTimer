using Microsoft.Data.Sqlite;
using SharpTimer.Core.Models;

namespace SharpTimer.Storage.Repositories;

public sealed class SqliteSessionRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteSessionRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveAsync(Session session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var connection = _connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO sessions (
                id,
                name,
                puzzle,
                created_at,
                updated_at,
                is_archived,
                sort_order
            )
            VALUES (
                $id,
                $name,
                $puzzle,
                $createdAt,
                $updatedAt,
                $isArchived,
                $sortOrder
            )
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                puzzle = excluded.puzzle,
                updated_at = excluded.updated_at,
                is_archived = excluded.is_archived,
                sort_order = excluded.sort_order;
            """;

        command.Parameters.AddWithValue("$id", StorageValueConverter.ToStorageText(session.Id));
        command.Parameters.AddWithValue("$name", session.Name);
        command.Parameters.AddWithValue("$puzzle", session.Puzzle);
        command.Parameters.AddWithValue("$createdAt", StorageValueConverter.ToStorageText(session.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", StorageValueConverter.ToStorageText(session.UpdatedAt));
        command.Parameters.AddWithValue("$isArchived", session.IsArchived ? 1 : 0);
        command.Parameters.AddWithValue("$sortOrder", session.SortOrder);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, puzzle, created_at, updated_at, is_archived, sort_order
            FROM sessions
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", StorageValueConverter.ToStorageText(id));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSession(reader) : null;
    }

    public async Task<IReadOnlyList<Session>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, puzzle, created_at, updated_at, is_archived, sort_order
            FROM sessions
            WHERE is_archived = 0
            ORDER BY sort_order ASC, created_at ASC;
            """;

        var sessions = new List<Session>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sessions.Add(ReadSession(reader));
        }

        return sessions;
    }

    private static Session ReadSession(SqliteDataReader reader)
    {
        return new Session
        {
            Id = StorageValueConverter.ToGuid(reader.GetString(0)),
            Name = reader.GetString(1),
            Puzzle = reader.GetString(2),
            CreatedAt = StorageValueConverter.ToDateTimeOffset(reader.GetString(3)),
            UpdatedAt = StorageValueConverter.ToDateTimeOffset(reader.GetString(4)),
            IsArchived = reader.GetInt32(5) != 0,
            SortOrder = reader.GetInt32(6)
        };
    }
}
