using System.ComponentModel.DataAnnotations;

namespace InterviewProjectTemplate.Application.Options
{
    /// <summary>
    /// Admin authentication settings, bound from the "AdminAuth" configuration section.
    /// <para>
    /// The signing key and seed password are supplied as environment variables by docker-compose so
    /// that no credential is committed to source control. Startup validation fails fast if they are
    /// missing or too weak, which is preferable to silently running with a guessable default.
    /// </para>
    /// </summary>
    public class AdminAuthOptions
    {
        public const string SectionName = "AdminAuth";

        /// <summary>
        /// HMAC-SHA256 signing key. 32 bytes is the minimum for the algorithm to be used safely, and
        /// .NET's handler rejects shorter keys outright.
        /// </summary>
        [Required]
        [MinLength(32, ErrorMessage = "AdminAuth:JwtSigningKey must be at least 32 characters.")]
        public string JwtSigningKey { get; set; } = string.Empty;

        [Required]
        public string Issuer { get; set; } = "MoodTracker";

        [Required]
        public string Audience { get; set; } = "MoodTrackerAdmin";

        [Range(1, 1440)]
        public int TokenLifetimeMinutes { get; set; } = 60;

        /// <summary>Username of the admin account created on first startup if absent.</summary>
        [Required]
        public string SeedUsername { get; set; } = "admin";

        /// <summary>
        /// Password for the seeded admin account. Only ever used when the account does not yet
        /// exist; changing it later does not silently reset an existing password.
        /// </summary>
        [Required]
        [MinLength(8, ErrorMessage = "AdminAuth:SeedPassword must be at least 8 characters.")]
        public string SeedPassword { get; set; } = string.Empty;
    }
}