using InterviewProjectTemplate.Domain.Enums;

namespace InterviewProjectTemplate.Domain.Entities
{
    /// <summary>
    /// A single mood submission. At most one row may exist per (UserKey, MoodDate) pair; that
    /// invariant is enforced by a unique index in the database rather than by application code
    /// alone, so concurrent requests cannot both pass a "has the user submitted?" check.
    /// </summary>
    public class MoodEntry
    {
        public int Id { get; set; }

        /// <summary>
        /// Opaque, server-issued identifier for an anonymous user, delivered via an HttpOnly cookie.
        /// This is deliberately not a user account: the brief requires the once-per-day rule to work
        /// without authentication.
        /// </summary>
        public string UserKey { get; set; } = string.Empty;

        /// <summary>
        /// The calendar day the mood applies to, in the application's configured time zone.
        /// Stored as a date (not a timestamp) because "once per day" is a calendar-day rule.
        /// </summary>
        public DateOnly MoodDate { get; set; }

        public MoodRating Rating { get; set; }

        /// <summary>Optional free-text comment supplied with the rating.</summary>
        public string? Comment { get; set; }

        /// <summary>Audit timestamp of when the row was written, always UTC.</summary>
        public DateTime CreatedAtUtc { get; set; }
    }
}