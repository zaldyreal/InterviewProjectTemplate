using InterviewProjectTemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewProjectTemplate.Infrastructure.Persistence.Configurations
{
    public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
    {
        public const int UsernameMaxLength = 256;

        public void Configure(EntityTypeBuilder<AdminUser> builder)
        {
            builder.ToTable("AdminUsers");

            builder.HasKey(user => user.Id);

            builder.Property(user => user.Username)
                .IsRequired()
                .HasMaxLength(UsernameMaxLength);

            builder.Property(user => user.PasswordHash)
                .IsRequired()
                .HasMaxLength(512);

            builder.Property(user => user.CreatedAtUtc)
                .IsRequired();

            builder.HasIndex(user => user.Username)
                .IsUnique()
                .HasDatabaseName("IX_AdminUsers_Username");
        }
    }
}