using InterviewProjectTemplate.Domain.Entities;
using InterviewProjectTemplate.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewProjectTemplate.Infrastructure.Persistence.Configurations
{
    public class MoodEntryConfiguration : IEntityTypeConfiguration<MoodEntry>
    {
        /// <summary>
        /// Chosen to comfortably hold a thoughtful comment while still bounding the column, so a
        /// client cannot write unbounded data. The API validates against the same limit.
        /// </summary>
        public const int CommentMaxLength = 1000;

        public const int UserKeyMaxLength = 64;

        public void Configure(EntityTypeBuilder<MoodEntry> builder)
        {
            builder.ToTable("MoodEntries");

            builder.HasKey(entry => entry.Id);

            builder.Property(entry => entry.UserKey)
                .IsRequired()
                .HasMaxLength(UserKeyMaxLength);

            builder.Property(entry => entry.MoodDate)
                .IsRequired()
                .HasColumnType("date")
                .HasConversion(new DateOnlyConverter(), new DateOnlyComparer());

            builder.Property(entry => entry.Rating)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(entry => entry.Comment)
                .HasMaxLength(CommentMaxLength);

            builder.Property(entry => entry.CreatedAtUtc)
                .IsRequired();

            // The core business rule: one mood per user per calendar day. Enforced in the database
            // so that two concurrent submissions cannot both pass an application-level check.
            builder.HasIndex(entry => new { entry.UserKey, entry.MoodDate })
                .IsUnique()
                .HasDatabaseName("IX_MoodEntries_UserKey_MoodDate");

            // The admin report is always ordered by most recent first; this index serves that sort.
            builder.HasIndex(entry => entry.CreatedAtUtc)
                .HasDatabaseName("IX_MoodEntries_CreatedAtUtc");
        }
    }
}