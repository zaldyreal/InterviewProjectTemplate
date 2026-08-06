using System.ComponentModel.DataAnnotations;

namespace InterviewProjectTemplate.Application.Options
{
    /// <summary>
    /// Application behaviour that a deployer may reasonably want to change without a rebuild.
    /// Bound from the "MoodTracker" configuration section and validated at startup.
    /// </summary>
    public class MoodTrackerOptions
    {
        public const string SectionName = "MoodTracker";

        /// <summary>
        /// IANA or Windows time zone identifier defining which calendar day a submission belongs to.
        /// Defaults to the team's zone from the brief (Hawthorn East, Victoria).
        /// </summary>
        [Required]
        public string TimeZone { get; set; } = "Australia/Melbourne";

        /// <summary>Name of the HttpOnly cookie carrying the anonymous user key.</summary>
        [Required]
        public string UserKeyCookieName { get; set; } = "mood_tracker_user";

        /// <summary>
        /// How long the anonymous identity cookie survives. A year keeps a returning team member
        /// recognisable without ever creating an account for them.
        /// </summary>
        [Range(1, 3650)]
        public int UserKeyCookieDays { get; set; } = 365;

        /// <summary>
        /// Whether the identity cookie is marked Secure. Disabled by default because the assessment
        /// runs over plain HTTP on localhost via Docker; a real deployment behind TLS should enable it.
        /// </summary>
        public bool UseSecureCookies { get; set; }
    }
}