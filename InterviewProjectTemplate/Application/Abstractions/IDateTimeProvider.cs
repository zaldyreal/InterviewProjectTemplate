namespace InterviewProjectTemplate.Application.Abstractions
{
    /// <summary>
    /// Abstracts the system clock so that date-dependent behaviour — specifically the
    /// once-per-calendar-day rule — can be tested deterministically instead of depending on when
    /// the test suite happens to run.
    /// </summary>
    public interface IDateTimeProvider
    {
        DateTime UtcNow { get; }

        /// <summary>
        /// "Today" in the application's configured time zone. The rule is a calendar-day rule, so it
        /// must be evaluated in the team's local zone rather than UTC; otherwise a submission at
        /// 9am Melbourne time would fall on the previous UTC day.
        /// </summary>
        DateOnly Today { get; }
    }
}