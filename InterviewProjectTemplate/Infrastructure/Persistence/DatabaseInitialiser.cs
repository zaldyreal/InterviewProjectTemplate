using InterviewProjectTemplate.Application.Abstractions;
using InterviewProjectTemplate.Application.Options;
using InterviewProjectTemplate.Application.Security;
using InterviewProjectTemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InterviewProjectTemplate.Infrastructure.Persistence
{
    /// <summary>
    /// Applies pending migrations and seeds the admin account at startup.
    /// <para>
    /// Running migrations from the application is a deliberate trade-off for this assessment: the
    /// brief requires the whole stack to come up from `docker compose up` alone, with no migration
    /// step for the reviewer to run. A production system would apply migrations from a release
    /// pipeline instead, so that a rolling deployment does not have several instances racing to
    /// alter the same schema — noted in the README.
    /// </para>
    /// </summary>
    public class DatabaseInitialiser
    {
        /// <summary>
        /// MySQL inside Docker Compose usually accepts connections a few seconds after the API
        /// starts. `depends_on` waits for the container, not for the database to be ready, so the
        /// first connection attempts are expected to fail and are retried.
        /// </summary>
        private const int MaxAttempts = 12;

        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

        private readonly MoodTrackerDbContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly AdminAuthOptions _adminAuthOptions;
        private readonly ILogger<DatabaseInitialiser> _logger;

        public DatabaseInitialiser(
            MoodTrackerDbContext dbContext,
            IPasswordHasher passwordHasher,
            IDateTimeProvider dateTimeProvider,
            IOptions<AdminAuthOptions> adminAuthOptions,
            ILogger<DatabaseInitialiser> logger)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _dateTimeProvider = dateTimeProvider;
            _adminAuthOptions = adminAuthOptions.Value;
            _logger = logger;
        }

        public async Task InitialiseAsync(CancellationToken cancellationToken = default)
        {
            await MigrateWithRetryAsync(cancellationToken);
            await SeedAdminUserAsync(cancellationToken);
        }

        private async Task MigrateWithRetryAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await _dbContext.Database.MigrateAsync(cancellationToken);

                    _logger.LogInformation("Database migrations applied successfully.");
                    return;
                }
                catch (Exception exception) when (attempt < MaxAttempts)
                {
                    _logger.LogWarning(
                        exception,
                        "Database not ready (attempt {Attempt} of {MaxAttempts}). Retrying in {Delay}s.",
                        attempt,
                        MaxAttempts,
                        RetryDelay.TotalSeconds);

                    await Task.Delay(RetryDelay, cancellationToken);
                }
            }

            // Final attempt outside the catch so a persistent failure surfaces as a startup crash
            // rather than an application that runs with no schema.
            await _dbContext.Database.MigrateAsync(cancellationToken);
        }

        private async Task SeedAdminUserAsync(CancellationToken cancellationToken)
        {
            var username = _adminAuthOptions.SeedUsername;

            var exists = await _dbContext.AdminUsers
                .AnyAsync(user => user.Username == username, cancellationToken);

            if (exists)
            {
                // Never overwrite an existing password: a redeploy must not silently reset a
                // credential that an administrator has since changed.
                _logger.LogInformation(
                    "Admin user '{Username}' already exists; leaving it unchanged.",
                    username);

                return;
            }

            _dbContext.AdminUsers.Add(new AdminUser
            {
                Username = username,
                PasswordHash = _passwordHasher.Hash(_adminAuthOptions.SeedPassword),
                CreatedAtUtc = _dateTimeProvider.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Seeded admin user '{Username}'.", username);
        }
    }
}
