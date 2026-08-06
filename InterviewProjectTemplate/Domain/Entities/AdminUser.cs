namespace InterviewProjectTemplate.Domain.Entities
{
    /// <summary>
    /// An account permitted to view the admin mood report. Passwords are never stored in plain
    /// text; only a salted hash produced by <see cref="Application.Security.IPasswordHasher"/> is persisted.
    /// </summary>
    public class AdminUser
    {
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
    }
}