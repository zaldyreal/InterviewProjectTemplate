using InterviewProjectTemplate.Application.Abstractions;

namespace InterviewProjectTemplate.Tests.TestSupport
{
    /// <summary>
    /// A controllable clock. The once-per-day rule is entirely date-dependent, so tests must be able
    /// to state exactly what "today" is; otherwise a suite that passes at 11pm could fail at midnight.
    /// </summary>
    public class FixedDateTimeProvider : IDateTimeProvider
    {
        public FixedDateTimeProvider(DateOnly today)
        {
            Today = today;
            UtcNow = today.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        }

        public DateTime UtcNow { get; set; }

        public DateOnly Today { get; set; }

        /// <summary>Moves the clock forward by whole days, as if the user returned another day.</summary>
        public void AdvanceDays(int days)
        {
            Today = Today.AddDays(days);
            UtcNow = UtcNow.AddDays(days);
        }
    }
}
