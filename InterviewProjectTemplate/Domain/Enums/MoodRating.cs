namespace InterviewProjectTemplate.Domain.Enums
{
    /// <summary>
    /// The four moods a user may report. Values are explicit and persisted as integers so that
    /// renaming a member never silently re-maps existing rows, and so the ordering (worst to best)
    /// is meaningful for reporting.
    /// </summary>
    public enum MoodRating
    {
        NotGoodAtAll = 1,
        ABitMeh = 2,
        PrettyGood = 3,
        FeelingGreat = 4
    }
}