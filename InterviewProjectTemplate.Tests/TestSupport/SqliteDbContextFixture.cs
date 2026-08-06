using InterviewProjectTemplate.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InterviewProjectTemplate.Tests.TestSupport
{
    /// <summary>
    /// Provides a real relational database for tests, backed by SQLite in memory.
    /// <para>
    /// EF Core's InMemory provider is deliberately avoided: it does not enforce unique indexes, so it
    /// would happily accept two mood entries for the same user and day — exactly the rule these tests
    /// exist to prove. SQLite enforces constraints, so the invariant is genuinely verified.
    /// </para>
    /// <para>
    /// The connection is held open for the fixture's lifetime because an in-memory SQLite database is
    /// destroyed when its last connection closes.
    /// </para>
    /// </summary>
    public sealed class SqliteDbContextFixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<MoodTrackerDbContext> _options;

        public SqliteDbContextFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<MoodTrackerDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        /// <summary>
        /// Creates a fresh context over the same database. Separate instances are used per operation
        /// so tests cannot pass because of change-tracker caching rather than persisted state.
        /// </summary>
        public MoodTrackerDbContext CreateContext() => new(_options);

        public void Dispose() => _connection.Dispose();
    }
}
