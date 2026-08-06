using InterviewProjectTemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InterviewProjectTemplate.Infrastructure.Persistence
{
    public class MoodTrackerDbContext : DbContext
    {
        public MoodTrackerDbContext(DbContextOptions<MoodTrackerDbContext> options)
            : base(options)
        {
        }

        public DbSet<MoodEntry> MoodEntries => Set<MoodEntry>();

        public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Picks up every IEntityTypeConfiguration in this assembly, so adding a new entity
            // means adding one configuration class rather than editing this method.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(MoodTrackerDbContext).Assembly);
        }
    }
}