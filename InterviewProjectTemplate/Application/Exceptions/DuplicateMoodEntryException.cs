namespace InterviewProjectTemplate.Application.Exceptions
{
    /// <summary>
    /// Raised when a user attempts a second mood submission on a day they have already recorded.
    /// <para>
    /// Modelled as a distinct exception type rather than a boolean result because it is the single
    /// rule the brief calls out by name, and because it must be reported identically whether it was
    /// caught by the pre-check or by the database's unique index losing a race.
    /// </para>
    /// </summary>
    public class DuplicateMoodEntryException : Exception
    {
        public DuplicateMoodEntryException(DateOnly moodDate)
            : base($"A mood entry has already been recorded for {moodDate:yyyy-MM-dd}.")
        {
            MoodDate = moodDate;
        }

        public DateOnly MoodDate { get; }
    }
}