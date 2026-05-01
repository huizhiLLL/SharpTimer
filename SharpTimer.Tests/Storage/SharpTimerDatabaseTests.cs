using Microsoft.Data.Sqlite;
using SharpTimer.Core.Models;
using SharpTimer.Storage;
using SharpTimer.Storage.Repositories;

namespace SharpTimer.Tests.Storage;

public sealed class SharpTimerDatabaseTests
{
    [Fact]
    public async Task EnsureCreatedAsync_CreatesSchemaAndRecordsVersion()
    {
        using var database = TestDatabase.Create();

        await database.EnsureCreatedAsync();

        await using var connection = database.ConnectionFactory.CreateOpenConnection();
        Assert.True(await TableExistsAsync(connection, "sessions"));
        Assert.True(await TableExistsAsync(connection, "solves"));
        Assert.Equal(SharpTimerDatabase.CurrentSchemaVersion, await GetSchemaVersionAsync(connection));
    }

    [Fact]
    public async Task SessionRepository_SavesAndListsActiveSessions()
    {
        using var database = TestDatabase.Create();
        await database.EnsureCreatedAsync();
        var repository = new SqliteSessionRepository(database.ConnectionFactory);
        var activeSession = new Session
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Main",
            Puzzle = "333",
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            SortOrder = 2
        };
        var archivedSession = activeSession with
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "Old",
            IsArchived = true,
            SortOrder = 1
        };

        await repository.SaveAsync(activeSession);
        await repository.SaveAsync(archivedSession);

        var loaded = await repository.GetAsync(activeSession.Id);
        var activeSessions = await repository.ListActiveAsync();

        Assert.NotNull(loaded);
        Assert.Equal("Main", loaded.Name);
        Assert.Equal("333", loaded.Puzzle);
        Assert.Single(activeSessions);
        Assert.Equal(activeSession.Id, activeSessions[0].Id);
    }

    [Fact]
    public async Task SolveRepository_SavesListsAndUpdatesPenalty()
    {
        using var database = TestDatabase.Create();
        await database.EnsureCreatedAsync();
        var sessionRepository = new SqliteSessionRepository(database.ConnectionFactory);
        var solveRepository = new SqliteSolveRepository(database.ConnectionFactory);
        var session = new Session
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Name = "Session",
            Puzzle = "333"
        };
        var solve = new Solve
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            SessionId = session.Id,
            Duration = TimeSpan.FromMilliseconds(12345),
            Penalty = Penalty.None,
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:10Z"),
            Scramble = "R U R'",
            Comment = "clean"
        };

        await sessionRepository.SaveAsync(session);
        await solveRepository.SaveAsync(solve);
        await solveRepository.UpdatePenaltyAsync(solve.Id, Penalty.PlusTwo);

        var solves = await solveRepository.ListBySessionAsync(session.Id);

        Assert.Single(solves);
        Assert.Equal(solve.Id, solves[0].Id);
        Assert.Equal(TimeSpan.FromMilliseconds(12345), solves[0].Duration);
        Assert.Equal(Penalty.PlusTwo, solves[0].Penalty);
        Assert.Equal(TimeSpan.FromMilliseconds(14345), solves[0].EffectiveDuration);
        Assert.Equal("R U R'", solves[0].Scramble);
        Assert.Equal("clean", solves[0].Comment);
    }

    [Fact]
    public async Task SolveRepository_DeletesSolves_WhenSessionIsDeleted()
    {
        using var database = TestDatabase.Create();
        await database.EnsureCreatedAsync();
        var sessionRepository = new SqliteSessionRepository(database.ConnectionFactory);
        var solveRepository = new SqliteSolveRepository(database.ConnectionFactory);
        var session = new Session
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            Name = "Session",
            Puzzle = "333"
        };
        var solve = new Solve
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            SessionId = session.Id,
            Duration = TimeSpan.FromSeconds(9),
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:10Z")
        };

        await sessionRepository.SaveAsync(session);
        await solveRepository.SaveAsync(solve);
        await DeleteSessionAsync(database.ConnectionFactory, session.Id);

        var solves = await solveRepository.ListBySessionAsync(session.Id);

        Assert.Empty(solves);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = $tableName;
            """;
        command.Parameters.AddWithValue("$tableName", tableName);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) == 1;
    }

    private static async Task<int> GetSchemaVersionAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version) FROM schema_migrations;";

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private static async Task DeleteSessionAsync(SqliteConnectionFactory connectionFactory, Guid sessionId)
    {
        await using var connection = connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sessions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", sessionId.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestDatabase : IDisposable
    {
        private readonly string _databasePath;

        private TestDatabase(string databasePath)
        {
            _databasePath = databasePath;
            ConnectionFactory = new SqliteConnectionFactory(databasePath);
            Instance = new SharpTimerDatabase(ConnectionFactory);
        }

        public SqliteConnectionFactory ConnectionFactory { get; }

        public SharpTimerDatabase Instance { get; }

        public static TestDatabase Create()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
            return new TestDatabase(databasePath);
        }

        public Task EnsureCreatedAsync()
        {
            return Instance.EnsureCreatedAsync();
        }

        public void Dispose()
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}
