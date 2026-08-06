using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InterviewProjectTemplate.Infrastructure.Persistence
{
    /// <summary>
    /// Builds a <see cref="MoodTrackerDbContext"/> for `dotnet ef` commands.
    /// <para>
    /// Without this, the EF tooling boots the application's host to locate the context, which would
    /// run the startup migration and admin seeding — and therefore require a live database just to
    /// scaffold a migration. The connection string here is only ever used to determine the provider's
    /// SQL dialect; no connection is opened when generating a migration.
    /// </para>
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MoodTrackerDbContext>
    {
        public MoodTrackerDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable(
                    "ConnectionStrings__MySQLConnectionString")
                ?? "Server=localhost;Port=3306;Database=moodtrackerdb;Uid=app;Pwd=password";

            var options = new DbContextOptionsBuilder<MoodTrackerDbContext>()
                .UseMySQL(connectionString)
                .Options;

            return new MoodTrackerDbContext(options);
        }
    }
}
